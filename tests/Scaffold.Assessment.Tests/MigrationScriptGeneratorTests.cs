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

    private static SchemaObject ForeignKeyFull(
        string name, string parentTable, string columns, string referencedTable,
        string referencedColumns, string deleteAction = "NO_ACTION", string updateAction = "NO_ACTION",
        string schema = "dbo", string referencedSchema = "dbo")
        => new()
        {
            Name = name, Schema = schema, ObjectType = "Constraint", SubType = "FOREIGN KEY",
            ParentObjectName = parentTable, Columns = columns, ReferencedTable = referencedTable,
            ReferencedColumns = referencedColumns, ReferencedSchema = referencedSchema,
            DeleteAction = deleteAction, UpdateAction = updateAction
        };

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
    public void GetAvailableScripts_Returns12Scripts()
    {
        var schema = BuildSchema(Table("T1"));

        var scripts = MigrationScriptGenerator.GetAvailableScripts(schema);

        Assert.Equal(12, scripts.Count);
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

    #region FK Creation — SQL Server syntax

    [Fact]
    public void GenerateApplyForeignKeys_WithFullMetadata_ProducesAlterTableAddConstraint()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("ALTER TABLE [dbo].[Orders] ADD CONSTRAINT [FK_Orders_Customer]", result);
        Assert.Contains("FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])", result);
        Assert.Contains("ON DELETE NO ACTION ON UPDATE NO ACTION", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_CascadeDelete_IncludesAction()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id", deleteAction: "CASCADE"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("ON DELETE CASCADE", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_SetNull_IncludesAction()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id",
                deleteAction: "SET_NULL", updateAction: "SET_NULL"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("ON DELETE SET NULL ON UPDATE SET NULL", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_MultipleColumns_FormatsCorrectly()
    {
        var schema = BuildSchema(
            Table("OrderItems"),
            Table("Orders"),
            ForeignKeyFull("FK_OrderItems_Orders", "OrderItems", "OrderId,LineNumber", "Orders", "Id,LineNumber"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("FOREIGN KEY ([OrderId], [LineNumber]) REFERENCES [dbo].[Orders] ([Id], [LineNumber])", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_MissingMetadata_FallsBackToCheckCheck()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKey("FK_Orders_Customer", "Orders"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("WITH CHECK CHECK CONSTRAINT [FK_Orders_Customer]", result);
    }

    [Fact]
    public void GenerateApplyForeignKeys_Empty_ReturnsNoForeignKeys()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = MigrationScriptGenerator.GenerateApplyForeignKeys(schema);

        Assert.Contains("No foreign keys to restore", result);
    }

    #endregion

    #region FK Creation — PostgreSQL syntax

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_ProducesQuotedIdentifiers()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\" ADD CONSTRAINT \"FK_Orders_Customer\"", result);
        Assert.Contains("FOREIGN KEY (\"CustomerId\") REFERENCES \"public\".\"Customers\" (\"Id\")", result);
        Assert.Contains("ON DELETE NO ACTION ON UPDATE NO ACTION", result);
    }

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_CascadeDelete()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id",
                deleteAction: "CASCADE", updateAction: "SET_NULL"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("ON DELETE CASCADE ON UPDATE SET NULL", result);
    }

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_MapsDboToPublic()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("\"public\".\"Orders\"", result);
    }

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_NonDboSchema_Preserved()
    {
        var schema = BuildSchema(
            Table("Orders", "Sales"),
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1",
                schema: "Sales", referencedSchema: "Warehouse"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("\"Sales\".\"Orders\"", result);
        Assert.Contains("\"Warehouse\".\"RefTable\"", result);
    }

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_MissingMetadata_ProducesWarning()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKey("FK_Missing", "Orders"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("WARNING", result);
        Assert.Contains("FK_Missing", result);
    }

    [Fact]
    public void GenerateCreateForeignKeysPostgreSql_Empty_ReturnsNoForeignKeys()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = MigrationScriptGenerator.GenerateCreateForeignKeysPostgreSql(schema);

        Assert.Contains("No foreign keys to create", result);
    }

    #endregion

    #region Generate lookup — new script IDs

    [Fact]
    public void Generate_CreateForeignKeys_ReturnsNonNull()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1"));

        var result = MigrationScriptGenerator.Generate("create-foreign-keys", schema);

        Assert.NotNull(result);
        Assert.Contains("FK_Test", result);
    }

    [Fact]
    public void Generate_CreateForeignKeysPg_ReturnsNonNull()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1"));

        var result = MigrationScriptGenerator.Generate("create-foreign-keys-pg", schema);

        Assert.NotNull(result);
        Assert.Contains("FK_Test", result);
    }

    [Fact]
    public void GetAvailableScripts_ContainsNewFkScripts()
    {
        var schema = BuildSchema(
            Table("T1"),
            ForeignKeyFull("FK1", "T1", "C1", "T2", "C2"));

        var scripts = MigrationScriptGenerator.GetAvailableScripts(schema);

        Assert.Contains(scripts, s => s.ScriptId == "create-foreign-keys");
        Assert.Contains(scripts, s => s.ScriptId == "create-foreign-keys-pg");
    }

    #endregion
}
