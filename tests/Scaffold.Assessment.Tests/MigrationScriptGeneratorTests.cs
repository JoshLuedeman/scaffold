using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class MigrationScriptGeneratorTests
{
    private static SchemaInventory BuildSchema(params SchemaObject[] objects)
    {
        return new SchemaInventory { Objects = objects.ToList() };
    }

    private static SchemaObject Table(string name, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Table" };

    private static SchemaObject ForeignKey(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Constraint", SubType = "FOREIGN KEY", ParentObjectName = parentTable };

    private static SchemaObject CheckConstraint(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Constraint", SubType = "CHECK", ParentObjectName = parentTable };

    private static SchemaObject NonClusteredIndex(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Index", SubType = "NONCLUSTERED", ParentObjectName = parentTable };

    private static SchemaObject Trigger(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Trigger", ParentObjectName = parentTable };

    [Fact]
    public void GenerateDropForeignKeys_ProducesAlterTableDrop()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKey("FK_Orders_Customer", "Orders"),
            ForeignKey("FK_Orders_Product", "Orders"));

        var result = MigrationScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Customer]", result);
        Assert.Contains("ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Product]", result);
    }

    [Fact]
    public void GenerateDropForeignKeys_EmptySchema_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = MigrationScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("No foreign keys found", result);
    }

    [Fact]
    public void GenerateDropNonClusteredIndexes_ProducesDropIndex()
    {
        var schema = BuildSchema(
            Table("Orders"),
            NonClusteredIndex("IX_Orders_Date", "Orders"),
            NonClusteredIndex("IX_Orders_Status", "Orders"));

        var result = MigrationScriptGenerator.GenerateDropNonClusteredIndexes(schema);

        Assert.Contains("DROP INDEX [IX_Orders_Date] ON [dbo].[Orders]", result);
        Assert.Contains("DROP INDEX [IX_Orders_Status] ON [dbo].[Orders]", result);
    }

    [Fact]
    public void GenerateDropNonClusteredIndexes_IgnoresClusteredIndexes()
    {
        var schema = BuildSchema(
            Table("Orders"),
            new SchemaObject
            {
                Name = "PK_Orders", Schema = "dbo", ObjectType = "Index",
                SubType = "CLUSTERED", ParentObjectName = "Orders"
            });

        var result = MigrationScriptGenerator.GenerateDropNonClusteredIndexes(schema);

        Assert.DoesNotContain("PK_Orders", result);
    }

    [Fact]
    public void GenerateDropTriggers_ProducesDropTrigger()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Trigger("TR_Orders_Audit", "Orders"));

        var result = MigrationScriptGenerator.GenerateDropTriggers(schema);

        Assert.Contains("DROP TRIGGER [dbo].[TR_Orders_Audit]", result);
    }

    [Fact]
    public void GenerateDisableCheckConstraints_ProducesNocheck()
    {
        var schema = BuildSchema(
            Table("Orders"),
            CheckConstraint("CK_Orders_Amount", "Orders"));

        var result = MigrationScriptGenerator.GenerateDisableCheckConstraints(schema);

        Assert.Contains("NOCHECK CONSTRAINT [CK_Orders_Amount]", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_ProducesCheckCheck()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKey("FK_Orders_Customer", "Orders"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("WITH CHECK CHECK CONSTRAINT [FK_Orders_Customer]", result);
    }

    [Fact]
    public void GenerateApplyNonClusteredIndexes_ProducesRebuild()
    {
        var schema = BuildSchema(
            Table("Orders"),
            NonClusteredIndex("IX_Orders_Date", "Orders"));

        var result = MigrationScriptGenerator.GenerateApplyNonClusteredIndexes(schema);

        Assert.Contains("ALTER INDEX [IX_Orders_Date] ON [dbo].[Orders] REBUILD", result);
    }

    [Fact]
    public void GenerateEnableCheckConstraints_ProducesCheck()
    {
        var schema = BuildSchema(
            Table("Orders"),
            CheckConstraint("CK_Orders_Amount", "Orders"));

        var result = MigrationScriptGenerator.GenerateEnableCheckConstraints(schema);

        Assert.Contains("WITH CHECK CHECK CONSTRAINT [CK_Orders_Amount]", result);
    }

    [Fact]
    public void GenerateUpdateStatistics_ProducesUpdateForEachTable()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            Table("Products"));

        var result = MigrationScriptGenerator.GenerateUpdateStatistics(schema);

        Assert.Contains("UPDATE STATISTICS [dbo].[Orders]", result);
        Assert.Contains("UPDATE STATISTICS [dbo].[Customers]", result);
        Assert.Contains("UPDATE STATISTICS [dbo].[Products]", result);
    }

    [Fact]
    public void GenerateValidateRowCounts_ProducesSelectCount()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"));

        var result = MigrationScriptGenerator.GenerateValidateRowCounts(schema);

        Assert.Contains("COUNT(*)", result);
        Assert.Contains("[dbo].[Orders]", result);
        Assert.Contains("[dbo].[Customers]", result);
    }

    [Fact]
    public void Generate_UnknownScriptId_ReturnsNull()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = MigrationScriptGenerator.Generate("nonexistent", schema);

        Assert.Null(result);
    }

    [Fact]
    public void GetAvailableScripts_ReturnsCorrectCounts()
    {
        var schema = BuildSchema(
            Table("T1"), Table("T2"), Table("T3"), Table("T4"), Table("T5"),
            ForeignKey("FK1", "T1"), ForeignKey("FK2", "T2"),
            NonClusteredIndex("IX1", "T1"), NonClusteredIndex("IX2", "T2"), NonClusteredIndex("IX3", "T3"),
            Trigger("TR1", "T1"),
            CheckConstraint("CK1", "T1"));

        var scripts = MigrationScriptGenerator.GetAvailableScripts(schema);

        var dropFk = scripts.Single(s => s.ScriptId == "drop-foreign-keys");
        var dropIdx = scripts.Single(s => s.ScriptId == "drop-nonclustered-indexes");
        var dropTrg = scripts.Single(s => s.ScriptId == "drop-triggers");
        var disableChk = scripts.Single(s => s.ScriptId == "disable-check-constraints");
        var updateStats = scripts.Single(s => s.ScriptId == "update-statistics");

        Assert.Equal(2, dropFk.ObjectCount);
        Assert.Equal(3, dropIdx.ObjectCount);
        Assert.Equal(1, dropTrg.ObjectCount);
        Assert.Equal(1, disableChk.ObjectCount);
        Assert.Equal(5, updateStats.ObjectCount);
    }

    [Fact]
    public void GetAvailableScripts_Returns10Scripts()
    {
        var schema = BuildSchema(Table("T1"));

        var scripts = MigrationScriptGenerator.GetAvailableScripts(schema);

        Assert.Equal(10, scripts.Count);
    }

    [Fact]
    public void GenerateDropForeignKeys_HandlesMultipleSchemas()
    {
        var schema = BuildSchema(
            Table("Orders", "Sales"),
            ForeignKey("FK_Orders_Customer", "Orders", "Sales"));

        var result = MigrationScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("ALTER TABLE [Sales].[Orders] DROP CONSTRAINT [FK_Orders_Customer]", result);
    }
}
