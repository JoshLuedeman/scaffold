using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

/// <summary>
/// Tests for the PostgreSQL SchemaAnalyzer counting and mapping logic.
/// Since NpgsqlConnection/NpgsqlCommand are sealed and cannot be mocked,
/// we test the inventory model, counting semantics, and PostgreSQL-specific
/// categorization logic. Integration tests (Phase 1, #23) will verify
/// against a real PostgreSQL instance.
/// </summary>
public class SchemaAnalyzerTests
{
    [Fact]
    public void EmptyInventory_AllCountsAreZero()
    {
        var inventory = BuildPostgreSqlInventory();

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
        var inventory = BuildPostgreSqlInventory(
            Obj("users", "Table", "public"),
            Obj("orders", "Table", "public"),
            Obj("products", "Table", "sales"));

        Assert.Equal(3, inventory.TableCount);
    }

    [Fact]
    public void ViewCount_IncludesViewsAndMaterializedViews()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("vw_active_users", "View", "public"),
            Obj("mv_sales_summary", "MaterializedView", "analytics"));

        Assert.Equal(2, inventory.ViewCount);
    }

    [Fact]
    public void MaterializedViews_CountTowardViewCount()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("mv_report", "MaterializedView", "public"),
            Obj("mv_metrics", "MaterializedView", "analytics"));

        Assert.Equal(2, inventory.ViewCount);
        Assert.Equal(0, inventory.TableCount);
    }

    [Fact]
    public void StoredProcedureCount_CountsFunctionObjects()
    {
        // In PostgreSQL, functions and procedures are both stored as "Function" ObjectType
        var inventory = BuildPostgreSqlInventory(
            Obj("get_user", "Function", "public"),
            Obj("update_stats", "Function", "public"));

        Assert.Equal(2, inventory.StoredProcedureCount);
    }

    [Fact]
    public void IndexCount_MatchesIndexObjects()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("idx_users_email", "Index", "public"),
            Obj("idx_orders_date", "Index", "sales"));

        Assert.Equal(2, inventory.IndexCount);
    }

    [Fact]
    public void TriggerCount_MatchesTriggerObjects()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("trg_audit_insert", "Trigger", "public"));

        Assert.Equal(1, inventory.TriggerCount);
    }

    [Fact]
    public void MixedObjects_CountedCorrectly()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("users", "Table", "public"),
            Obj("orders", "Table", "public"),
            Obj("vw_active", "View", "public"),
            Obj("mv_summary", "MaterializedView", "analytics"),
            Obj("get_user", "Function", "public"),
            Obj("idx_users_email", "Index", "public"),
            Obj("idx_orders_date", "Index", "public"),
            Obj("trg_audit", "Trigger", "public"),
            Obj("pk_users", "Constraint", "public"),
            Obj("users_id_seq", "Sequence", "public"),
            Obj("pgcrypto", "Extension", "public"));

        Assert.Equal(2, inventory.TableCount);
        Assert.Equal(2, inventory.ViewCount);   // 1 View + 1 MaterializedView
        Assert.Equal(1, inventory.StoredProcedureCount);
        Assert.Equal(2, inventory.IndexCount);
        Assert.Equal(1, inventory.TriggerCount);
        Assert.Equal(11, inventory.Objects.Count);
    }

    [Fact]
    public void ConstraintsSequencesExtensions_NotCountedInNamedProperties()
    {
        var inventory = BuildPostgreSqlInventory(
            Obj("pk_users", "Constraint", "public"),
            Obj("users_id_seq", "Sequence", "public"),
            Obj("pgcrypto", "Extension", "public"));

        Assert.Equal(0, inventory.TableCount);
        Assert.Equal(0, inventory.ViewCount);
        Assert.Equal(0, inventory.StoredProcedureCount);
        Assert.Equal(0, inventory.IndexCount);
        Assert.Equal(0, inventory.TriggerCount);
        Assert.Equal(3, inventory.Objects.Count);
    }

    [Fact]
    public void SchemaObject_PostgreSqlSchema_NotDbo()
    {
        // PostgreSQL uses actual schema names, not "dbo"
        var obj = new SchemaObject { Name = "users", ObjectType = "Table", Schema = "public" };

        Assert.Equal("public", obj.Schema);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("sales")]
    [InlineData("analytics")]
    [InlineData("custom_schema")]
    public void SchemaObject_PreservesPostgreSqlSchema(string schema)
    {
        var obj = new SchemaObject { Name = "test_table", ObjectType = "Table", Schema = schema };

        Assert.Equal(schema, obj.Schema);
    }

    [Fact]
    public void FunctionSubType_DistinguishesFunctionFromProcedure()
    {
        var func = new SchemaObject
        {
            Name = "get_user",
            Schema = "public",
            ObjectType = "Function",
            SubType = "Function"
        };

        var proc = new SchemaObject
        {
            Name = "update_stats",
            Schema = "public",
            ObjectType = "Function",
            SubType = "Procedure"
        };

        // Both count as "Function" ObjectType for StoredProcedureCount
        Assert.Equal("Function", func.ObjectType);
        Assert.Equal("Function", proc.ObjectType);
        // SubType differentiates them
        Assert.Equal("Function", func.SubType);
        Assert.Equal("Procedure", proc.SubType);
    }

    [Theory]
    [InlineData("Unique")]
    [InlineData("NonUnique")]
    public void IndexSubType_CapturesUniqueness(string subType)
    {
        var idx = new SchemaObject
        {
            Name = "idx_test",
            Schema = "public",
            ObjectType = "Index",
            ParentObjectName = "users",
            SubType = subType
        };

        Assert.Equal("Index", idx.ObjectType);
        Assert.Equal(subType, idx.SubType);
        Assert.Equal("users", idx.ParentObjectName);
    }

    /// <summary>
    /// Builds a SchemaInventory using the same counting logic as
    /// <see cref="Scaffold.Assessment.PostgreSql.SchemaAnalyzer.AnalyzeAsync"/>.
    /// </summary>
    private static SchemaInventory BuildPostgreSqlInventory(params SchemaObject[] objects)
    {
        var inventory = new SchemaInventory();
        inventory.Objects.AddRange(objects);

        // Mirror the counting logic from PostgreSql SchemaAnalyzer.AnalyzeAsync
        inventory.TableCount = inventory.Objects.Count(o => o.ObjectType == "Table");
        inventory.ViewCount = inventory.Objects.Count(o => o.ObjectType == "View" || o.ObjectType == "MaterializedView");
        inventory.StoredProcedureCount = inventory.Objects.Count(o => o.ObjectType == "Function");
        inventory.IndexCount = inventory.Objects.Count(o => o.ObjectType == "Index");
        inventory.TriggerCount = inventory.Objects.Count(o => o.ObjectType == "Trigger");

        return inventory;
    }

    private static SchemaObject Obj(string name, string type, string schema = "public") =>
        new() { Name = name, ObjectType = type, Schema = schema };
}
