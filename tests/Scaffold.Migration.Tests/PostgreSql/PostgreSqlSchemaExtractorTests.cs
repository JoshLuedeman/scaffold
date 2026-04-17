using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.PostgreSql.Models;
using Scaffold.Migration.Shared;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlSchemaExtractorTests
{
    #region IsObjectIncluded

    [Fact]
    public void IsObjectIncluded_QualifiedMatch_ReturnsTrue()
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(
            "public", "users", ["public.users"]);
        Assert.True(result);
    }

    [Fact]
    public void IsObjectIncluded_QualifiedMatch_CaseInsensitive()
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(
            "Public", "Users", ["public.users"]);
        Assert.True(result);
    }

    [Fact]
    public void IsObjectIncluded_UnqualifiedMatch_ReturnsTrue()
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(
            "public", "users", ["users"]);
        Assert.True(result);
    }

    [Fact]
    public void IsObjectIncluded_NoMatch_ReturnsFalse()
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(
            "public", "users", ["public.orders"]);
        Assert.False(result);
    }

    [Fact]
    public void IsObjectIncluded_EmptyList_ReturnsFalse()
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(
            "public", "users", []);
        Assert.False(result);
    }

    [Theory]
    [InlineData("sales", "orders", "sales.orders", true)]
    [InlineData("sales", "orders", "public.orders", false)]
    [InlineData("sales", "orders", "orders", true)]
    [InlineData("public", "items", "items", true)]
    [InlineData("public", "items", "public.items", true)]
    [InlineData("public", "items", "sales.items", false)]
    public void IsObjectIncluded_VariousInputs(string schema, string table, string filter, bool expected)
    {
        var result = PostgreSqlSchemaExtractor.IsObjectIncluded(schema, table, [filter]);
        Assert.Equal(expected, result);
    }

    #endregion

    #region BuildFullType

    [Theory]
    [InlineData("integer", null, null, null, null, "integer")]
    [InlineData("text", null, null, null, null, "text")]
    [InlineData("boolean", null, null, null, null, "boolean")]
    [InlineData("timestamp with time zone", null, null, null, null, "timestamp with time zone")]
    public void BuildFullType_SimpleTypes_ReturnsTypeAsIs(
        string dataType, string? udtName, int? maxLen, int? precision, int? scale, string expected)
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType(dataType, udtName, maxLen, precision, scale);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFullType_CharacterVaryingWithLength_IncludesLength()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("character varying", null, 255, null, null);
        Assert.Equal("character varying(255)", result);
    }

    [Fact]
    public void BuildFullType_CharacterWithLength_IncludesLength()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("character", null, 10, null, null);
        Assert.Equal("character(10)", result);
    }

    [Fact]
    public void BuildFullType_NumericWithPrecisionAndScale_IncludesBoth()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("numeric", null, null, 18, 2);
        Assert.Equal("numeric(18,2)", result);
    }

    [Fact]
    public void BuildFullType_NumericWithPrecisionOnly_IncludesPrecision()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("numeric", null, null, 10, 0);
        Assert.Equal("numeric(10)", result);
    }

    [Fact]
    public void BuildFullType_UserDefinedEnum_PublicSchema_ReturnsQuotedUdtName()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("USER-DEFINED", "mood_type", null, null, null, "public");
        Assert.Equal("\"mood_type\"", result);
    }

    [Fact]
    public void BuildFullType_UserDefinedEnum_NullSchema_DefaultsToPublic()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("USER-DEFINED", "mood_type", null, null, null);
        Assert.Equal("\"mood_type\"", result);
    }

    [Fact]
    public void BuildFullType_UserDefinedEnum_CustomSchema_SchemaQualified()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("USER-DEFINED", "mood_type", null, null, null, "custom");
        Assert.Equal("\"custom\".\"mood_type\"", result);
    }

    [Fact]
    public void BuildFullType_Array_ReturnsArrayNotation()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("ARRAY", "_int4", null, null, null);
        Assert.Equal("int4[]", result);
    }

    [Fact]
    public void BuildFullType_BitWithLength_IncludesLength()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("bit", null, 8, null, null);
        Assert.Equal("bit(8)", result);
    }

    [Fact]
    public void BuildFullType_BitVaryingWithLength_IncludesLength()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("bit varying", null, 64, null, null);
        Assert.Equal("bit varying(64)", result);
    }

    #endregion

    #region MapFkAction

    [Theory]
    [InlineData('a', "NO ACTION")]
    [InlineData('r', "RESTRICT")]
    [InlineData('c', "CASCADE")]
    [InlineData('n', "SET NULL")]
    [InlineData('d', "SET DEFAULT")]
    [InlineData('x', "NO ACTION")] // unknown defaults to NO ACTION
    public void MapFkAction_VariousChars_MapsCorrectly(char action, string expected)
    {
        Assert.Equal(expected, PostgreSqlSchemaExtractor.MapFkAction(action));
    }

    #endregion

    #region ParseIndexDefinition

    [Fact]
    public void ParseIndexDefinition_SimpleIndex_ExtractsColumns()
    {
        var def = "CREATE INDEX idx_users_email ON public.users USING btree (email)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_users_email", def);

        Assert.Equal("idx_users_email", result.Name);
        Assert.False(result.IsUnique);
        Assert.Equal("btree", result.AccessMethod);
        Assert.Single(result.Columns);
        Assert.Equal("email", result.Columns[0]);
        Assert.Null(result.FilterExpression);
        Assert.Equal(def, result.RawDdl);
    }

    [Fact]
    public void ParseIndexDefinition_UniqueIndex_SetsIsUnique()
    {
        var def = "CREATE UNIQUE INDEX idx_users_email ON public.users USING btree (email)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_users_email", def);

        Assert.True(result.IsUnique);
    }

    [Fact]
    public void ParseIndexDefinition_GinIndex_ExtractsAccessMethod()
    {
        var def = "CREATE INDEX idx_docs_content ON public.documents USING gin (content)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_docs_content", def);

        Assert.Equal("gin", result.AccessMethod);
    }

    [Fact]
    public void ParseIndexDefinition_MultiColumnIndex_ExtractsAllColumns()
    {
        var def = "CREATE INDEX idx_orders_user_date ON public.orders USING btree (user_id, created_at DESC)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_orders_user_date", def);

        Assert.Equal(2, result.Columns.Count);
        Assert.Equal("user_id", result.Columns[0]);
        Assert.Equal("created_at DESC", result.Columns[1]);
    }

    [Fact]
    public void ParseIndexDefinition_PartialIndex_ExtractsFilter()
    {
        var def = "CREATE INDEX idx_active_users ON public.users USING btree (email) WHERE (is_active = true)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_active_users", def);

        Assert.Equal("(is_active = true)", result.FilterExpression);
    }

    [Fact]
    public void ParseIndexDefinition_GistIndex_ExtractsAccessMethod()
    {
        var def = "CREATE INDEX idx_geo ON public.locations USING gist (coordinates)";
        var result = PostgreSqlSchemaExtractor.ParseIndexDefinition("idx_geo", def);

        Assert.Equal("gist", result.AccessMethod);
    }

    #endregion

    #region TopologicalSort with PgTableDefinition

    [Fact]
    public void TopologicalSort_TablesWithFkDependencies_ParentsBeforeChildren()
    {
        var users = new PgTableDefinition { Schema = "public", TableName = "users" };
        var orders = new PgTableDefinition
        {
            Schema = "public",
            TableName = "orders",
            ForeignKeys =
            [
                new PgForeignKeyDefinition
                {
                    Name = "fk_orders_users",
                    ReferencedSchema = "public",
                    ReferencedTable = "users",
                    Columns = ["user_id"],
                    ReferencedColumns = ["id"]
                }
            ]
        };
        var orderItems = new PgTableDefinition
        {
            Schema = "public",
            TableName = "order_items",
            ForeignKeys =
            [
                new PgForeignKeyDefinition
                {
                    Name = "fk_items_orders",
                    ReferencedSchema = "public",
                    ReferencedTable = "orders",
                    Columns = ["order_id"],
                    ReferencedColumns = ["id"]
                }
            ]
        };

        // Input in reverse order to ensure sorting works
        var tables = new List<PgTableDefinition> { orderItems, orders, users };

        var sorted = TopologicalSorter.Sort(
            tables,
            t => t.QualifiedName,
            t => t.ForeignKeys.Select(fk => $"{fk.ReferencedSchema}.{fk.ReferencedTable}"));

        Assert.Equal("public.users", sorted[0].QualifiedName);
        Assert.Equal("public.orders", sorted[1].QualifiedName);
        Assert.Equal("public.order_items", sorted[2].QualifiedName);
    }

    [Fact]
    public void TopologicalSort_NoDependencies_PreservesOriginalOrder()
    {
        var a = new PgTableDefinition { Schema = "public", TableName = "alpha" };
        var b = new PgTableDefinition { Schema = "public", TableName = "beta" };
        var c = new PgTableDefinition { Schema = "public", TableName = "gamma" };

        var tables = new List<PgTableDefinition> { a, b, c };

        var sorted = TopologicalSorter.Sort(
            tables,
            t => t.QualifiedName,
            t => t.ForeignKeys.Select(fk => $"{fk.ReferencedSchema}.{fk.ReferencedTable}"));

        Assert.Equal("public.alpha", sorted[0].QualifiedName);
        Assert.Equal("public.beta", sorted[1].QualifiedName);
        Assert.Equal("public.gamma", sorted[2].QualifiedName);
    }

    #endregion

    #region Column Type Edge Cases

    [Fact]
    public void BuildFullType_DecimalAlias_HandlesLikeNumeric()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("decimal", null, null, 10, 4);
        // "decimal" is treated same as "numeric" in BuildFullType
        Assert.Equal("numeric(10,4)", result);
    }

    [Fact]
    public void BuildFullType_CharacterVaryingNoLength_ReturnsTypeOnly()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("character varying", null, null, null, null);
        Assert.Equal("character varying", result);
    }

    [Fact]
    public void BuildFullType_NumericNoPrecision_ReturnsTypeOnly()
    {
        var result = PostgreSqlSchemaExtractor.BuildFullType("numeric", null, null, null, null);
        Assert.Equal("numeric", result);
    }

    #endregion
}