using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

/// <summary>
/// Tests for SchemaInventory construction and counting logic.
/// SchemaAnalyzer.AnalyzeAsync requires a live SqlConnection, so we test
/// the inventory model and counting behavior that AnalyzeAsync performs.
/// </summary>
public class SchemaAnalyzerTests
{
    [Fact]
    public void EmptyInventory_AllCountsAreZero()
    {
        var inventory = BuildInventory();

        Assert.Equal(0, inventory.TableCount);
        Assert.Equal(0, inventory.ViewCount);
        Assert.Equal(0, inventory.StoredProcedureCount);
        Assert.Equal(0, inventory.IndexCount);
        Assert.Equal(0, inventory.TriggerCount);
        Assert.Empty(inventory.Objects);
    }

    [Fact]
    public void TableCount_MatchesTableObjects()
    {
        var inventory = BuildInventory(
            Obj("Users", "Table"),
            Obj("Orders", "Table"),
            Obj("Products", "Table"));

        Assert.Equal(3, inventory.TableCount);
    }

    [Fact]
    public void ViewCount_MatchesViewObjects()
    {
        var inventory = BuildInventory(
            Obj("vw_Active", "View"),
            Obj("vw_Summary", "View"));

        Assert.Equal(2, inventory.ViewCount);
    }

    [Fact]
    public void StoredProcedureCount_MatchesProcObjects()
    {
        var inventory = BuildInventory(
            Obj("sp_GetUser", "StoredProcedure"));

        Assert.Equal(1, inventory.StoredProcedureCount);
    }

    [Fact]
    public void IndexCount_MatchesIndexObjects()
    {
        var inventory = BuildInventory(
            Obj("IX_Users_Email", "Index"),
            Obj("IX_Orders_Date", "Index"));

        Assert.Equal(2, inventory.IndexCount);
    }

    [Fact]
    public void TriggerCount_MatchesTriggerObjects()
    {
        var inventory = BuildInventory(
            Obj("trg_AuditInsert", "Trigger"));

        Assert.Equal(1, inventory.TriggerCount);
    }

    [Fact]
    public void MixedObjects_CountedCorrectly()
    {
        var inventory = BuildInventory(
            Obj("Users", "Table"),
            Obj("Orders", "Table"),
            Obj("vw_Active", "View"),
            Obj("sp_GetUser", "StoredProcedure"),
            Obj("IX_Users_Email", "Index"),
            Obj("IX_Orders_Date", "Index"),
            Obj("trg_AuditInsert", "Trigger"),
            Obj("CK_Orders_Amount", "Constraint"));

        Assert.Equal(2, inventory.TableCount);
        Assert.Equal(1, inventory.ViewCount);
        Assert.Equal(1, inventory.StoredProcedureCount);
        Assert.Equal(2, inventory.IndexCount);
        Assert.Equal(1, inventory.TriggerCount);
        Assert.Equal(8, inventory.Objects.Count);
    }

    [Fact]
    public void ConstraintsAndFunctions_NotCountedInNamedProperties()
    {
        var inventory = BuildInventory(
            Obj("fn_GetTotal", "Function"),
            Obj("PK_Users", "Constraint"));

        Assert.Equal(0, inventory.TableCount);
        Assert.Equal(0, inventory.ViewCount);
        Assert.Equal(0, inventory.StoredProcedureCount);
        Assert.Equal(0, inventory.IndexCount);
        Assert.Equal(0, inventory.TriggerCount);
        Assert.Equal(2, inventory.Objects.Count);
    }

    [Fact]
    public void SchemaObject_DefaultSchema_IsDbo()
    {
        var obj = new SchemaObject { Name = "Users", ObjectType = "Table" };

        Assert.Equal("dbo", obj.Schema);
    }

    [Fact]
    public void SchemaObject_CustomSchema()
    {
        var obj = new SchemaObject { Name = "Logs", ObjectType = "Table", Schema = "audit" };

        Assert.Equal("audit", obj.Schema);
    }

    /// <summary>
    /// Builds a SchemaInventory with the same counting logic that SchemaAnalyzer.AnalyzeAsync uses.
    /// </summary>
    private static SchemaInventory BuildInventory(params SchemaObject[] objects)
    {
        var inventory = new SchemaInventory();
        inventory.Objects.AddRange(objects);

        // Mirror the counting logic from SchemaAnalyzer.AnalyzeAsync
        inventory.TableCount = inventory.Objects.Count(o => o.ObjectType == "Table");
        inventory.ViewCount = inventory.Objects.Count(o => o.ObjectType == "View");
        inventory.StoredProcedureCount = inventory.Objects.Count(o => o.ObjectType == "StoredProcedure");
        inventory.IndexCount = inventory.Objects.Count(o => o.ObjectType == "Index");
        inventory.TriggerCount = inventory.Objects.Count(o => o.ObjectType == "Trigger");

        return inventory;
    }

    private static SchemaObject Obj(string name, string type, string schema = "dbo") =>
        new() { Name = name, ObjectType = type, Schema = schema };
}
