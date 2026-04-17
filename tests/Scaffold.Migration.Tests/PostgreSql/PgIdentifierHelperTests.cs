using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PgIdentifierHelperTests
{
    #region QuoteIdentifier

    [Fact]
    public void QuoteIdentifier_NormalName_WrapsInDoubleQuotes()
    {
        var result = PgIdentifierHelper.QuoteIdentifier("Users");
        Assert.Equal("\"Users\"", result);
    }

    [Fact]
    public void QuoteIdentifier_NameWithDoubleQuotes_EscapesEmbeddedQuotes()
    {
        var result = PgIdentifierHelper.QuoteIdentifier("my\"table");
        Assert.Equal("\"my\"\"table\"", result);
    }

    [Fact]
    public void QuoteIdentifier_EmptyString_ReturnsEmptyQuotedIdentifier()
    {
        var result = PgIdentifierHelper.QuoteIdentifier("");
        Assert.Equal("\"\"", result);
    }

    [Theory]
    [InlineData("Id", "\"Id\"")]
    [InlineData("column_name", "\"column_name\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    public void QuoteIdentifier_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.QuoteIdentifier(input));
    }

    #endregion

    #region MapSchema

    [Fact]
    public void MapSchema_Dbo_ReturnsPublic()
    {
        Assert.Equal("public", PgIdentifierHelper.MapSchema("dbo"));
    }

    [Fact]
    public void MapSchema_DboUpperCase_ReturnsPublic()
    {
        Assert.Equal("public", PgIdentifierHelper.MapSchema("DBO"));
    }

    [Fact]
    public void MapSchema_Public_ReturnsPublic()
    {
        Assert.Equal("public", PgIdentifierHelper.MapSchema("public"));
    }

    [Fact]
    public void MapSchema_CustomSchema_PreservesName()
    {
        Assert.Equal("custom_schema", PgIdentifierHelper.MapSchema("custom_schema"));
    }

    [Theory]
    [InlineData("dbo", "public")]
    [InlineData("Dbo", "public")]
    [InlineData("DBO", "public")]
    [InlineData("public", "public")]
    [InlineData("sales", "sales")]
    [InlineData("hr", "hr")]
    public void MapSchema_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.MapSchema(input));
    }

    #endregion

    #region QuotePgName

    [Fact]
    public void QuotePgName_DboUsers_MapsToPublicAndQuotes()
    {
        var result = PgIdentifierHelper.QuotePgName("dbo.Users");
        Assert.Equal("\"public\".\"Users\"", result);
    }

    [Fact]
    public void QuotePgName_PublicOrders_QuotesWithoutMapping()
    {
        var result = PgIdentifierHelper.QuotePgName("public.orders");
        Assert.Equal("\"public\".\"orders\"", result);
    }

    [Fact]
    public void QuotePgName_CustomSchemaWithQuotedName_StripsQuotesAndRequotes()
    {
        var result = PgIdentifierHelper.QuotePgName("custom.\"quoted\"");
        Assert.Equal("\"custom\".\"quoted\"", result);
    }

    [Fact]
    public void QuotePgName_BracketNotation_StripsBracketsAndQuotes()
    {
        var result = PgIdentifierHelper.QuotePgName("[dbo].[Users]");
        Assert.Equal("\"public\".\"Users\"", result);
    }

    [Fact]
    public void QuotePgName_SinglePartName_QuotesWithoutSchemaMapping()
    {
        var result = PgIdentifierHelper.QuotePgName("Users");
        Assert.Equal("\"Users\"", result);
    }

    [Theory]
    [InlineData("dbo.Users", "\"public\".\"Users\"")]
    [InlineData("DBO.Orders", "\"public\".\"Orders\"")]
    [InlineData("[dbo].[Products]", "\"public\".\"Products\"")]
    [InlineData("sales.Invoices", "\"sales\".\"Invoices\"")]
    public void QuotePgName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.QuotePgName(input));
    }

    #endregion

    #region QuoteSqlName

    [Fact]
    public void QuoteSqlName_DboUsers_WrapsBothPartsInBrackets()
    {
        var result = PgIdentifierHelper.QuoteSqlName("dbo.Users");
        Assert.Equal("[dbo].[Users]", result);
    }

    [Fact]
    public void QuoteSqlName_NameWithEmbeddedBracket_EscapesByDoubling()
    {
        var result = PgIdentifierHelper.QuoteSqlName("dbo.my]table");
        Assert.Equal("[dbo].[my]]table]", result);
    }

    [Fact]
    public void QuoteSqlName_AlreadyBracketed_StripsAndReapplies()
    {
        var result = PgIdentifierHelper.QuoteSqlName("[dbo].[Users]");
        Assert.Equal("[dbo].[Users]", result);
    }

    [Theory]
    [InlineData("dbo.Users", "[dbo].[Users]")]
    [InlineData("schema.table", "[schema].[table]")]
    [InlineData("single", "[single]")]
    public void QuoteSqlName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.QuoteSqlName(input));
    }

    #endregion

    #region ClampTimeout

    [Fact]
    public void ClampTimeout_NullValue_UsesDefault()
    {
        var result = PgIdentifierHelper.ClampTimeout(null, 600);
        Assert.Equal(600, result);
    }

    [Fact]
    public void ClampTimeout_BelowMinimum_ClampsTo30()
    {
        var result = PgIdentifierHelper.ClampTimeout(5, 600);
        Assert.Equal(30, result);
    }

    [Fact]
    public void ClampTimeout_AboveMaximum_ClampsTo3600()
    {
        var result = PgIdentifierHelper.ClampTimeout(9999, 600);
        Assert.Equal(3600, result);
    }

    [Fact]
    public void ClampTimeout_InRange_ReturnsValue()
    {
        var result = PgIdentifierHelper.ClampTimeout(120, 600);
        Assert.Equal(120, result);
    }

    [Fact]
    public void ClampTimeout_NullWithDefaultBelowMin_ClampsTo30()
    {
        var result = PgIdentifierHelper.ClampTimeout(null, 10);
        Assert.Equal(30, result);
    }

    [Theory]
    [InlineData(null, 600, 600)]
    [InlineData(30, 600, 30)]
    [InlineData(3600, 600, 3600)]
    [InlineData(29, 600, 30)]
    [InlineData(3601, 600, 3600)]
    [InlineData(100, 200, 100)]
    public void ClampTimeout_VariousInputs_ReturnsExpected(int? value, int defaultValue, int expected)
    {
        Assert.Equal(expected, PgIdentifierHelper.ClampTimeout(value, defaultValue));
    }

    #endregion
}
