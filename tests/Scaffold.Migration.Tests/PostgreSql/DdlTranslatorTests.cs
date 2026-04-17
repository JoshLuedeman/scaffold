using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.Tests.PostgreSql;

public class DdlTranslatorTests
{
    private readonly DdlTranslator _translator = new();

    #region Helpers

    private static TableDefinition SimpleTable(string name = "Users", string schema = "dbo") => new()
    {
        Schema = schema,
        TableName = name,
        Columns =
        [
            new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, IsIdentity = true, OrdinalPosition = 1 },
            new ColumnDefinition { Name = "Name", DataType = "nvarchar", MaxLength = 100, IsNullable = false, OrdinalPosition = 2 },
            new ColumnDefinition { Name = "Email", DataType = "nvarchar", MaxLength = 255, IsNullable = true, OrdinalPosition = 3 }
        ],
        PrimaryKey = new PrimaryKeyDefinition { Name = "PK_Users", Columns = ["Id"], IsClustered = true }
    };

    private static TableDefinition OrdersTable() => new()
    {
        Schema = "dbo",
        TableName = "Orders",
        Columns =
        [
            new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, IsIdentity = true, OrdinalPosition = 1 },
            new ColumnDefinition { Name = "UserId", DataType = "int", IsNullable = false, OrdinalPosition = 2 },
            new ColumnDefinition { Name = "Amount", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false, OrdinalPosition = 3 },
            new ColumnDefinition { Name = "CreatedAt", DataType = "datetime2", IsNullable = false, DefaultExpression = "(getdate())", OrdinalPosition = 4 }
        ],
        PrimaryKey = new PrimaryKeyDefinition { Name = "PK_Orders", Columns = ["Id"], IsClustered = true },
        ForeignKeys =
        [
            new ForeignKeyDefinition
            {
                Name = "FK_Orders_Users",
                ReferencedSchema = "dbo",
                ReferencedTable = "Users",
                Columns = ["UserId"],
                ReferencedColumns = ["Id"],
                DeleteAction = "CASCADE",
                UpdateAction = "NO ACTION"
            }
        ]
    };

    #endregion

    #region GenerateCreateTable — Basic

    [Fact]
    public void GenerateCreateTable_SimpleTable_ContainsColumnsAndPK()
    {
        var table = SimpleTable();

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("CREATE TABLE \"public\".\"Users\"", result);
        Assert.Contains("\"Id\" integer GENERATED ALWAYS AS IDENTITY NOT NULL", result);
        Assert.Contains("\"Name\" varchar(100) NOT NULL", result);
        Assert.Contains("\"Email\" varchar(255)", result);
        Assert.Contains("CONSTRAINT \"PK_Users\" PRIMARY KEY (\"Id\")", result);
    }

    [Fact]
    public void GenerateCreateTable_NullableColumn_DoesNotIncludeNotNull()
    {
        var table = SimpleTable();

        var result = _translator.GenerateCreateTable(table);

        // Email is nullable, so no NOT NULL
        Assert.DoesNotContain("\"Email\" varchar(255) NOT NULL", result);
        // But Name is NOT NULL
        Assert.Contains("\"Name\" varchar(100) NOT NULL", result);
    }

    #endregion

    #region GenerateCreateTable — Identity

    [Theory]
    [InlineData("int", "integer GENERATED ALWAYS AS IDENTITY")]
    [InlineData("bigint", "bigint GENERATED ALWAYS AS IDENTITY")]
    [InlineData("smallint", "smallint GENERATED ALWAYS AS IDENTITY")]
    public void GenerateCreateTable_IdentityColumn_UsesGeneratedAlways(string dataType, string expectedFragment)
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns = [new ColumnDefinition { Name = "Id", DataType = dataType, IsIdentity = true, IsNullable = false, OrdinalPosition = 1 }]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains(expectedFragment, result);
    }

    [Fact]
    public void GenerateCreateTable_IdentityColumn_DoesNotIncludeDefault()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "Id", DataType = "int", IsIdentity = true, IsNullable = false,
                    DefaultExpression = "((1))", OrdinalPosition = 1
                }
            ]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.DoesNotContain("DEFAULT", result);
    }

    #endregion

    #region GenerateCreateTable — Defaults

    [Theory]
    [InlineData("(getdate())", "DEFAULT CURRENT_TIMESTAMP")]
    [InlineData("((0))", "DEFAULT 0")]
    [InlineData("(newid())", "DEFAULT gen_random_uuid()")]
    [InlineData("(N'pending')", "DEFAULT 'pending'")]
    public void GenerateCreateTable_DefaultExpression_TranslatesCorrectly(string sqlDefault, string expectedFragment)
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "Val", DataType = "nvarchar", MaxLength = 50,
                    IsNullable = true, DefaultExpression = sqlDefault, OrdinalPosition = 1
                }
            ]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains(expectedFragment, result);
    }

    #endregion

    #region GenerateCreateTable — Computed Columns

    [Fact]
    public void GenerateCreateTable_ComputedColumn_SkipsWithComment()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns =
            [
                new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new ColumnDefinition
                {
                    Name = "FullName", DataType = "nvarchar", MaxLength = 200,
                    IsComputed = true, ComputedExpression = "([FirstName]+' '+[LastName])",
                    OrdinalPosition = 2
                }
            ]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("COMPUTED COLUMN", result);
        Assert.Contains("\"FullName\"", result);
        Assert.Contains("manual translation", result);
        // Computed columns should not appear as normal column definitions
        Assert.DoesNotContain("varchar(200)", result);
    }

    #endregion

    #region GenerateCreateTable — Schema Mapping

    [Fact]
    public void GenerateCreateTable_DboSchema_MapsToPublic()
    {
        var table = SimpleTable("Users", "dbo");

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("\"public\".\"Users\"", result);
    }

    [Fact]
    public void GenerateCreateTable_CustomSchema_Preserved()
    {
        var table = SimpleTable("Users", "Sales");

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("\"Sales\".\"Users\"", result);
    }

    #endregion

    #region GenerateCreateTable — Unique Constraints

    [Fact]
    public void GenerateCreateTable_UniqueConstraint_IncludesInline()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Users",
            Columns = [new ColumnDefinition { Name = "Email", DataType = "nvarchar", MaxLength = 255, IsNullable = false, OrdinalPosition = 1 }],
            UniqueConstraints = [new UniqueConstraintDefinition { Name = "UQ_Users_Email", Columns = ["Email"] }]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("CONSTRAINT \"UQ_Users_Email\" UNIQUE (\"Email\")", result);
    }

    [Fact]
    public void GenerateCreateTable_MultiColumnUnique_FormatsAllColumns()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "ProductVariant",
            Columns =
            [
                new ColumnDefinition { Name = "ProductId", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new ColumnDefinition { Name = "Color", DataType = "nvarchar", MaxLength = 50, IsNullable = false, OrdinalPosition = 2 }
            ],
            UniqueConstraints = [new UniqueConstraintDefinition { Name = "UQ_PV_Combo", Columns = ["ProductId", "Color"] }]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("CONSTRAINT \"UQ_PV_Combo\" UNIQUE (\"ProductId\", \"Color\")", result);
    }

    #endregion

    #region GenerateCreateTable — Check Constraints

    [Fact]
    public void GenerateCreateTable_CheckConstraint_TranslatesBrackets()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Products",
            Columns = [new ColumnDefinition { Name = "Price", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false, OrdinalPosition = 1 }],
            CheckConstraints = [new CheckConstraintDefinition { Name = "CK_Price_Positive", Expression = "([Price]>(0))" }]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("CONSTRAINT \"CK_Price_Positive\" CHECK (\"Price\">(0))", result);
    }

    #endregion

    #region GenerateCreateTable — Warning Types

    [Fact]
    public void GenerateCreateTable_WarningType_IncludesInlineComment()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns = [new ColumnDefinition { Name = "Data", DataType = "sql_variant", IsNullable = true, OrdinalPosition = 1 }]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains("WARNING", result);
        Assert.Contains("sql_variant", result);
    }

    #endregion

    #region GenerateAddForeignKey

    [Fact]
    public void GenerateAddForeignKey_SimpleFK_ProducesAlterTable()
    {
        var table = OrdersTable();
        var fk = table.ForeignKeys[0];

        var result = _translator.GenerateAddForeignKey(table, fk);

        Assert.Contains("ALTER TABLE \"public\".\"Orders\"", result);
        Assert.Contains("ADD CONSTRAINT \"FK_Orders_Users\"", result);
        Assert.Contains("FOREIGN KEY (\"UserId\") REFERENCES \"public\".\"Users\" (\"Id\")", result);
        Assert.Contains("ON DELETE CASCADE ON UPDATE NO ACTION", result);
    }

    [Fact]
    public void GenerateAddForeignKey_CrossSchema_MapsCorrectly()
    {
        var table = new TableDefinition { Schema = "Sales", TableName = "Orders" };
        var fk = new ForeignKeyDefinition
        {
            Name = "FK_Test",
            ReferencedSchema = "dbo",
            ReferencedTable = "Customers",
            Columns = ["CustomerId"],
            ReferencedColumns = ["Id"],
            DeleteAction = "SET NULL",
            UpdateAction = "CASCADE"
        };

        var result = _translator.GenerateAddForeignKey(table, fk);

        Assert.Contains("\"Sales\".\"Orders\"", result);
        Assert.Contains("\"public\".\"Customers\"", result);
        Assert.Contains("ON DELETE SET NULL ON UPDATE CASCADE", result);
    }

    [Fact]
    public void GenerateAddForeignKey_MultiColumn_FormatsAllColumns()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "OrderItems" };
        var fk = new ForeignKeyDefinition
        {
            Name = "FK_OI_Orders",
            ReferencedSchema = "dbo",
            ReferencedTable = "Orders",
            Columns = ["OrderId", "LineNumber"],
            ReferencedColumns = ["Id", "LineNumber"],
            DeleteAction = "NO ACTION",
            UpdateAction = "NO ACTION"
        };

        var result = _translator.GenerateAddForeignKey(table, fk);

        Assert.Contains("FOREIGN KEY (\"OrderId\", \"LineNumber\") REFERENCES \"public\".\"Orders\" (\"Id\", \"LineNumber\")", result);
    }

    #endregion

    #region GenerateCreateIndex

    [Fact]
    public void GenerateCreateIndex_SimpleIndex_ProducesCreateIndex()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "Orders" };
        var idx = new IndexDefinition { Name = "IX_Orders_Date", Columns = ["CreatedAt"] };

        var result = _translator.GenerateCreateIndex(table, idx);

        Assert.Equal("CREATE INDEX \"IX_Orders_Date\" ON \"public\".\"Orders\" (\"CreatedAt\");", result);
    }

    [Fact]
    public void GenerateCreateIndex_UniqueIndex_IncludesUniqueKeyword()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "Users" };
        var idx = new IndexDefinition { Name = "IX_Users_Email", Columns = ["Email"], IsUnique = true };

        var result = _translator.GenerateCreateIndex(table, idx);

        Assert.StartsWith("CREATE UNIQUE INDEX", result);
    }

    [Fact]
    public void GenerateCreateIndex_WithIncludedColumns_IncludesInclude()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "Orders" };
        var idx = new IndexDefinition
        {
            Name = "IX_Orders_UserId",
            Columns = ["UserId"],
            IncludedColumns = ["Amount", "CreatedAt"]
        };

        var result = _translator.GenerateCreateIndex(table, idx);

        Assert.Contains("INCLUDE (\"Amount\", \"CreatedAt\")", result);
    }

    [Fact]
    public void GenerateCreateIndex_WithFilter_TranslatesAndIncludes()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "Orders" };
        var idx = new IndexDefinition
        {
            Name = "IX_Orders_Active",
            Columns = ["Status"],
            FilterExpression = "([IsActive]=(1))"
        };

        var result = _translator.GenerateCreateIndex(table, idx);

        Assert.Contains("WHERE \"IsActive\"=(1)", result);
    }

    [Fact]
    public void GenerateCreateIndex_MultiColumn_FormatsAllColumns()
    {
        var table = new TableDefinition { Schema = "dbo", TableName = "Orders" };
        var idx = new IndexDefinition { Name = "IX_Orders_Composite", Columns = ["UserId", "CreatedAt"] };

        var result = _translator.GenerateCreateIndex(table, idx);

        Assert.Contains("(\"UserId\", \"CreatedAt\")", result);
    }

    #endregion

    #region TranslateSchema — Dependency Order

    [Fact]
    public void TranslateSchema_DependencyOrder_ReferencedTableFirst()
    {
        var users = SimpleTable();
        var orders = OrdersTable();

        // Pass orders first — translator should still put Users first
        var result = _translator.TranslateSchema([orders, users]);

        var usersIdx = result.FindIndex(s => s.Contains("\"Users\"") && s.StartsWith("CREATE TABLE"));
        var ordersIdx = result.FindIndex(s => s.Contains("\"Orders\"") && s.StartsWith("CREATE TABLE"));

        Assert.True(usersIdx >= 0, "Users CREATE TABLE not found");
        Assert.True(ordersIdx >= 0, "Orders CREATE TABLE not found");
        Assert.True(usersIdx < ordersIdx, "Users should come before Orders due to FK dependency");
    }

    [Fact]
    public void TranslateSchema_FKsAfterAllCreateTables()
    {
        var users = SimpleTable();
        var orders = OrdersTable();

        var result = _translator.TranslateSchema([users, orders]);

        var lastCreateTable = result.FindLastIndex(s => s.StartsWith("CREATE TABLE"));
        var firstAlterTable = result.FindIndex(s => s.Contains("ADD CONSTRAINT"));

        Assert.True(firstAlterTable > lastCreateTable, "FK ALTER TABLE should come after all CREATE TABLE");
    }

    [Fact]
    public void TranslateSchema_IndexesAfterFKs()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Orders",
            Columns = [new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, OrdinalPosition = 1 }],
            Indexes = [new IndexDefinition { Name = "IX_Test", Columns = ["Id"] }],
            ForeignKeys =
            [
                new ForeignKeyDefinition
                {
                    Name = "FK_Test", ReferencedSchema = "dbo", ReferencedTable = "Other",
                    Columns = ["Id"], ReferencedColumns = ["Id"]
                }
            ]
        };

        var other = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Other",
            Columns = [new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, OrdinalPosition = 1 }]
        };

        var result = _translator.TranslateSchema([table, other]);

        var fkIdx = result.FindIndex(s => s.Contains("ADD CONSTRAINT"));
        var indexIdx = result.FindIndex(s => s.StartsWith("CREATE INDEX") || s.StartsWith("CREATE UNIQUE INDEX"));

        Assert.True(fkIdx >= 0, "FK statement not found");
        Assert.True(indexIdx >= 0, "Index statement not found");
        Assert.True(indexIdx > fkIdx, "Index should come after FK");
    }

    [Fact]
    public void TranslateSchema_SelfReference_HandledWithoutCycle()
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Employees",
            Columns =
            [
                new ColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, OrdinalPosition = 1 },
                new ColumnDefinition { Name = "ManagerId", DataType = "int", IsNullable = true, OrdinalPosition = 2 }
            ],
            ForeignKeys =
            [
                new ForeignKeyDefinition
                {
                    Name = "FK_Employees_Manager",
                    ReferencedSchema = "dbo",
                    ReferencedTable = "Employees",
                    Columns = ["ManagerId"],
                    ReferencedColumns = ["Id"]
                }
            ]
        };

        var result = _translator.TranslateSchema([table]);

        Assert.Single(result.Where(s => s.StartsWith("CREATE TABLE")));
        Assert.Single(result.Where(s => s.Contains("ADD CONSTRAINT")));
    }

    #endregion

    #region TranslateSchema — Full Schema

    [Fact]
    public void TranslateSchema_ReturnsCorrectStatementCount()
    {
        var users = SimpleTable();
        var orders = OrdersTable();
        orders.Indexes.Add(new IndexDefinition { Name = "IX_Orders_UserId", Columns = ["UserId"] });

        var result = _translator.TranslateSchema([users, orders]);

        // 2 CREATE TABLE + 1 FK ALTER TABLE + 1 CREATE INDEX = 4
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void TranslateSchema_EmptyInput_ReturnsEmptyList()
    {
        var result = _translator.TranslateSchema([]);

        Assert.Empty(result);
    }

    #endregion

    #region MapSchema

    [Theory]
    [InlineData("dbo", "public")]
    [InlineData("DBO", "public")]
    [InlineData("Dbo", "public")]
    [InlineData("Sales", "Sales")]
    [InlineData("custom", "custom")]
    public void MapSchema_MapsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, DdlTranslator.MapSchema(input));
    }

    #endregion

    #region QuoteIdentifier

    [Theory]
    [InlineData("Users", "\"Users\"")]
    [InlineData("my table", "\"my table\"")]
    [InlineData("Id", "\"Id\"")]
    public void QuoteIdentifier_WrapsInDoubleQuotes(string input, string expected)
    {
        Assert.Equal(expected, DdlTranslator.QuoteIdentifier(input));
    }

    #endregion

    #region TranslateCheckExpression

    [Theory]
    [InlineData("[Price]>(0)", "\"Price\">(0)")]
    [InlineData("([Status] IN ('Active','Inactive'))", "\"Status\" IN ('Active','Inactive')")]
    [InlineData("[Qty]>=(1) AND [Qty]<=(1000)", "\"Qty\">=(1) AND \"Qty\"<=(1000)")]
    public void TranslateCheckExpression_ReplacesBrackets(string input, string expected)
    {
        Assert.Equal(expected, DdlTranslator.TranslateCheckExpression(input));
    }

    #endregion

    #region TranslateFilterExpression

    [Theory]
    [InlineData("([IsActive]=(1))", "\"IsActive\"=(1)")]
    [InlineData("[Status]='Active'", "\"Status\"='Active'")]
    public void TranslateFilterExpression_TranslatesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, DdlTranslator.TranslateFilterExpression(input));
    }

    #endregion

    #region TopologicalSort

    [Fact]
    public void TopologicalSort_PutsReferencedTablesFirst()
    {
        var users = SimpleTable();
        var orders = OrdersTable();

        var result = DdlTranslator.TopologicalSort([orders, users]);

        Assert.Equal("Users", result[0].TableName);
        Assert.Equal("Orders", result[1].TableName);
    }

    [Fact]
    public void TopologicalSort_NoFKs_PreservesOrder()
    {
        var a = new TableDefinition { Schema = "dbo", TableName = "A" };
        var b = new TableDefinition { Schema = "dbo", TableName = "B" };

        var result = DdlTranslator.TopologicalSort([a, b]);

        Assert.Equal("A", result[0].TableName);
        Assert.Equal("B", result[1].TableName);
    }

    [Fact]
    public void TopologicalSort_CircularDependency_StillReturnsAllTables()
    {
        var a = new TableDefinition
        {
            Schema = "dbo", TableName = "A",
            ForeignKeys = [new ForeignKeyDefinition { Name = "FK_A_B", ReferencedSchema = "dbo", ReferencedTable = "B", Columns = ["X"], ReferencedColumns = ["X"] }]
        };
        var b = new TableDefinition
        {
            Schema = "dbo", TableName = "B",
            ForeignKeys = [new ForeignKeyDefinition { Name = "FK_B_A", ReferencedSchema = "dbo", ReferencedTable = "A", Columns = ["Y"], ReferencedColumns = ["Y"] }]
        };

        var result = DdlTranslator.TopologicalSort([a, b]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.TableName == "A");
        Assert.Contains(result, t => t.TableName == "B");
    }

    #endregion

    #region Data Type Integration

    [Theory]
    [InlineData("int", null, null, null, "integer")]
    [InlineData("bigint", null, null, null, "bigint")]
    [InlineData("nvarchar", 100, null, null, "varchar(100)")]
    [InlineData("decimal", null, 18, 2, "numeric(18,2)")]
    [InlineData("datetime2", null, null, null, "timestamp")]
    [InlineData("uniqueidentifier", null, null, null, "uuid")]
    [InlineData("bit", null, null, null, "boolean")]
    public void GenerateCreateTable_DataTypeMapping_UsesDataTypeMapper(
        string sqlType, int? maxLen, int? precision, int? scale, string expectedPgType)
    {
        var table = new TableDefinition
        {
            Schema = "dbo",
            TableName = "Test",
            Columns =
            [
                new ColumnDefinition
                {
                    Name = "Col", DataType = sqlType, MaxLength = maxLen,
                    Precision = precision, Scale = scale, IsNullable = true, OrdinalPosition = 1
                }
            ]
        };

        var result = _translator.GenerateCreateTable(table);

        Assert.Contains($"\"Col\" {expectedPgType}", result);
    }

    #endregion
}