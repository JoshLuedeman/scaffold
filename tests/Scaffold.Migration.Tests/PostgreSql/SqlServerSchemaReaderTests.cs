using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class SqlServerSchemaReaderTests
{
    #region IsTableIncluded

    [Theory]
    [InlineData("dbo", "Users", new[] { "Users" }, true)]
    [InlineData("dbo", "Users", new[] { "users" }, true)]       // case-insensitive
    [InlineData("dbo", "Users", new[] { "Orders" }, false)]
    [InlineData("dbo", "Users", new[] { "dbo.Users" }, true)]   // schema-qualified
    [InlineData("dbo", "Users", new[] { "DBO.USERS" }, true)]   // case-insensitive qualified
    [InlineData("Sales", "Orders", new[] { "Sales.Orders" }, true)]
    [InlineData("Sales", "Orders", new[] { "dbo.Orders" }, false)] // wrong schema
    [InlineData("dbo", "Users", new[] { "Orders", "Users" }, true)] // second match
    public void IsTableIncluded_MatchesCorrectly(string schema, string tableName, string[] included, bool expected)
    {
        var result = SqlServerSchemaReader.IsTableIncluded(schema, tableName, included);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTableIncluded_EmptyList_ReturnsFalse()
    {
        var result = SqlServerSchemaReader.IsTableIncluded("dbo", "Users", Array.Empty<string>());
        Assert.False(result);
    }

    [Fact]
    public void IsTableIncluded_MixedFormats_MatchesCorrectly()
    {
        var included = new[] { "Products", "Sales.Orders", "dbo.Customers" };

        Assert.True(SqlServerSchemaReader.IsTableIncluded("dbo", "Products", included));
        Assert.True(SqlServerSchemaReader.IsTableIncluded("Sales", "Orders", included));
        Assert.True(SqlServerSchemaReader.IsTableIncluded("dbo", "Customers", included));
        Assert.False(SqlServerSchemaReader.IsTableIncluded("dbo", "Unknown", included));
        // "Products" matches by name alone, regardless of schema
        Assert.True(SqlServerSchemaReader.IsTableIncluded("Sales", "Products", included));
    }

    #endregion
}