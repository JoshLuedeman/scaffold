using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class PostgreSqlScriptGeneratorTests
{
    #region Helpers

    private static SchemaInventory BuildSchema(params SchemaObject[] objects)
        => new() { Objects = objects.ToList() };

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

    private static SchemaObject NonClusteredIndex(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Index", SubType = "NONCLUSTERED", ParentObjectName = parentTable };

    private static SchemaObject Trigger(string name, string parentTable, string schema = "dbo")
        => new() { Name = name, Schema = schema, ObjectType = "Trigger", ParentObjectName = parentTable };

    #endregion

    #region DropForeignKeys

    [Fact]
    public void GenerateDropForeignKeys_ProducesAlterTableDropConstraint()
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKey("FK_Orders_Customer", "Orders"),
            ForeignKey("FK_Orders_Product", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\" DROP CONSTRAINT IF EXISTS \"FK_Orders_Customer\"", result);
        Assert.Contains("ALTER TABLE \"public\".\"Orders\" DROP CONSTRAINT IF EXISTS \"FK_Orders_Product\"", result);
    }

    [Fact]
    public void GenerateDropForeignKeys_MapsDboToPublic()
    {
        var schema = BuildSchema(ForeignKey("FK_Test", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("\"public\"", result);
    }

    [Fact]
    public void GenerateDropForeignKeys_CustomSchema_Preserved()
    {
        var schema = BuildSchema(ForeignKey("FK_Test", "Orders", "Sales"));

        var result = PostgreSqlScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("\"Sales\"", result);
    }

    [Fact]
    public void GenerateDropForeignKeys_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDropForeignKeys(schema);

        Assert.Contains("No foreign keys found", result);
    }

    #endregion

    #region DropIndexes

    [Fact]
    public void GenerateDropIndexes_ProducesDropIndexIfExists()
    {
        var schema = BuildSchema(
            Table("Orders"),
            NonClusteredIndex("IX_Orders_Date", "Orders"),
            NonClusteredIndex("IX_Orders_Status", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDropIndexes(schema);

        Assert.Contains("DROP INDEX IF EXISTS \"public\".\"IX_Orders_Date\"", result);
        Assert.Contains("DROP INDEX IF EXISTS \"public\".\"IX_Orders_Status\"", result);
    }

    [Fact]
    public void GenerateDropIndexes_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDropIndexes(schema);

        Assert.Contains("No indexes found", result);
    }

    #endregion

    #region DisableTriggers

    [Fact]
    public void GenerateDisableTriggers_ProducesDisableTriggerAll()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Trigger("TR_Orders_Audit", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDisableTriggers(schema);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\" DISABLE TRIGGER ALL", result);
    }

    [Fact]
    public void GenerateDisableTriggers_MultipleTriggersOnSameTable_SingleStatement()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Trigger("TR_Audit1", "Orders"),
            Trigger("TR_Audit2", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDisableTriggers(schema);

        // Should only have one DISABLE TRIGGER ALL per table
        var count = result.Split("DISABLE TRIGGER ALL").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void GenerateDisableTriggers_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateDisableTriggers(schema);

        Assert.Contains("No triggers found", result);
    }

    #endregion

    #region CreateForeignKeys

    [Fact]
    public void GenerateCreateForeignKeys_WithFullMetadata_ProducesAlterTableAddConstraint()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Table("Customers"),
            ForeignKeyFull("FK_Orders_Customer", "Orders", "CustomerId", "Customers", "Id"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\" ADD CONSTRAINT \"FK_Orders_Customer\"", result);
        Assert.Contains("FOREIGN KEY (\"CustomerId\") REFERENCES \"public\".\"Customers\" (\"Id\")", result);
        Assert.Contains("ON DELETE NO ACTION ON UPDATE NO ACTION", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_CascadeDelete()
    {
        var schema = BuildSchema(
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1", deleteAction: "CASCADE"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("ON DELETE CASCADE", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_MultiColumn()
    {
        var schema = BuildSchema(
            ForeignKeyFull("FK_OI", "OrderItems", "OrderId,LineNum", "Orders", "Id,LineNum"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("FOREIGN KEY (\"OrderId\", \"LineNum\") REFERENCES \"public\".\"Orders\" (\"Id\", \"LineNum\")", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_MissingMetadata_ProducesWarning()
    {
        var schema = BuildSchema(ForeignKey("FK_Missing", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("WARNING", result);
        Assert.Contains("FK_Missing", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_MapsDboToPublic()
    {
        var schema = BuildSchema(
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("\"public\".\"Orders\"", result);
        Assert.Contains("\"public\".\"RefTable\"", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_CrossSchema()
    {
        var schema = BuildSchema(
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1",
                schema: "Sales", referencedSchema: "Warehouse"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("\"Sales\".\"Orders\"", result);
        Assert.Contains("\"Warehouse\".\"RefTable\"", result);
    }

    [Fact]
    public void GenerateCreateForeignKeys_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateCreateForeignKeys(schema);

        Assert.Contains("No foreign keys to create", result);
    }

    #endregion

    #region CreateIndexes

    [Fact]
    public void GenerateCreateIndexes_ProducesStubComments()
    {
        var schema = BuildSchema(
            Table("Orders"),
            NonClusteredIndex("IX_Orders_Date", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateCreateIndexes(schema);

        Assert.Contains("IX_Orders_Date", result);
        Assert.Contains("<columns>", result);
        Assert.Contains("NOTE", result);
    }

    [Fact]
    public void GenerateCreateIndexes_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateCreateIndexes(schema);

        Assert.Contains("No indexes to create", result);
    }

    #endregion

    #region EnableTriggers

    [Fact]
    public void GenerateEnableTriggers_ProducesEnableTriggerAll()
    {
        var schema = BuildSchema(
            Table("Orders"),
            Trigger("TR_Orders_Audit", "Orders"));

        var result = PostgreSqlScriptGenerator.GenerateEnableTriggers(schema);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\" ENABLE TRIGGER ALL", result);
    }

    [Fact]
    public void GenerateEnableTriggers_Empty_ReturnsComment()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.GenerateEnableTriggers(schema);

        Assert.Contains("No triggers found", result);
    }

    #endregion

    #region AnalyzeTables

    [Fact]
    public void GenerateAnalyzeTables_ProducesAnalyzeStatements()
    {
        var schema = BuildSchema(Table("Orders"), Table("Customers"), Table("Products"));

        var result = PostgreSqlScriptGenerator.GenerateAnalyzeTables(schema);

        Assert.Contains("ANALYZE \"public\".\"Orders\"", result);
        Assert.Contains("ANALYZE \"public\".\"Customers\"", result);
        Assert.Contains("ANALYZE \"public\".\"Products\"", result);
    }

    [Fact]
    public void GenerateAnalyzeTables_CustomSchema()
    {
        var schema = BuildSchema(Table("Orders", "Sales"));

        var result = PostgreSqlScriptGenerator.GenerateAnalyzeTables(schema);

        Assert.Contains("ANALYZE \"Sales\".\"Orders\"", result);
    }

    [Fact]
    public void GenerateAnalyzeTables_Empty_ReturnsComment()
    {
        var schema = BuildSchema();

        var result = PostgreSqlScriptGenerator.GenerateAnalyzeTables(schema);

        Assert.Contains("No tables found", result);
    }

    #endregion

    #region ValidateRowCounts

    [Fact]
    public void GenerateValidateRowCounts_ProducesSelectCountQueries()
    {
        var schema = BuildSchema(Table("Orders"), Table("Customers"));

        var result = PostgreSqlScriptGenerator.GenerateValidateRowCounts(schema);

        Assert.Contains("COUNT(*)", result);
        Assert.Contains("\"public\".\"Orders\"", result);
        Assert.Contains("\"public\".\"Customers\"", result);
        Assert.Contains("table_name", result);
    }

    [Fact]
    public void GenerateValidateRowCounts_Empty_ReturnsComment()
    {
        var schema = BuildSchema();

        var result = PostgreSqlScriptGenerator.GenerateValidateRowCounts(schema);

        Assert.Contains("No tables found", result);
    }

    #endregion

    #region Generate Lookup

    [Theory]
    [InlineData("pg-drop-foreign-keys")]
    [InlineData("pg-drop-indexes")]
    [InlineData("pg-disable-triggers")]
    [InlineData("pg-create-foreign-keys")]
    [InlineData("pg-create-indexes")]
    [InlineData("pg-enable-triggers")]
    [InlineData("pg-analyze-tables")]
    [InlineData("pg-validate-row-counts")]
    public void Generate_ValidScriptId_ReturnsNonNull(string scriptId)
    {
        var schema = BuildSchema(
            Table("Orders"),
            ForeignKeyFull("FK_Test", "Orders", "Col1", "RefTable", "Col1"),
            NonClusteredIndex("IX_Test", "Orders"),
            Trigger("TR_Test", "Orders"));

        var result = PostgreSqlScriptGenerator.Generate(scriptId, schema);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_UnknownScriptId_ReturnsNull()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.Generate("nonexistent", schema);

        Assert.Null(result);
    }

    [Fact]
    public void Generate_SqlServerScriptId_ReturnsNull()
    {
        var schema = BuildSchema(Table("Orders"));

        var result = PostgreSqlScriptGenerator.Generate("drop-foreign-keys", schema);

        Assert.Null(result);
    }

    #endregion

    #region GetAvailableScripts

    [Fact]
    public void GetAvailableScripts_Returns8Scripts()
    {
        var schema = BuildSchema(Table("T1"));

        var scripts = PostgreSqlScriptGenerator.GetAvailableScripts(schema);

        Assert.Equal(8, scripts.Count);
    }

    [Fact]
    public void GetAvailableScripts_ReturnsCorrectCounts()
    {
        var schema = BuildSchema(
            Table("T1"), Table("T2"), Table("T3"),
            ForeignKey("FK1", "T1"), ForeignKey("FK2", "T2"),
            NonClusteredIndex("IX1", "T1"),
            Trigger("TR1", "T1"));

        var scripts = PostgreSqlScriptGenerator.GetAvailableScripts(schema);

        var dropFk = scripts.Single(s => s.ScriptId == "pg-drop-foreign-keys");
        var dropIdx = scripts.Single(s => s.ScriptId == "pg-drop-indexes");
        var disableTrg = scripts.Single(s => s.ScriptId == "pg-disable-triggers");
        var analyzeTbl = scripts.Single(s => s.ScriptId == "pg-analyze-tables");
        var validateRc = scripts.Single(s => s.ScriptId == "pg-validate-row-counts");

        Assert.Equal(2, dropFk.ObjectCount);
        Assert.Equal(1, dropIdx.ObjectCount);
        Assert.Equal(1, disableTrg.ObjectCount);
        Assert.Equal(3, analyzeTbl.ObjectCount);
        Assert.Equal(3, validateRc.ObjectCount);
    }

    [Fact]
    public void GetAvailableScripts_AllScriptIdsStartWithPg()
    {
        var schema = BuildSchema(Table("T1"));

        var scripts = PostgreSqlScriptGenerator.GetAvailableScripts(schema);

        Assert.All(scripts, s => Assert.StartsWith("pg-", s.ScriptId));
    }

    [Fact]
    public void GetAvailableScripts_HasPreAndPostScripts()
    {
        var schema = BuildSchema(Table("T1"));

        var scripts = PostgreSqlScriptGenerator.GetAvailableScripts(schema);

        Assert.Contains(scripts, s => s.Phase == "Pre");
        Assert.Contains(scripts, s => s.Phase == "Post");
    }

    #endregion

    #region Schema Mapping

    [Theory]
    [InlineData("dbo", "public")]
    [InlineData("DBO", "public")]
    [InlineData("Sales", "Sales")]
    [InlineData("custom", "custom")]
    public void MapSchema_MapsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, PostgreSqlScriptGenerator.MapSchema(input));
    }

    #endregion
}