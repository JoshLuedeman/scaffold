using NpgsqlTypes;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class CrossPlatformBulkCopierTests
{
    #region MapToNpgsqlDbType — integer types

    [Theory]
    [InlineData("int", NpgsqlDbType.Integer)]
    [InlineData("integer", NpgsqlDbType.Integer)]
    [InlineData("INT", NpgsqlDbType.Integer)]
    [InlineData("bigint", NpgsqlDbType.Bigint)]
    [InlineData("smallint", NpgsqlDbType.Smallint)]
    [InlineData("tinyint", NpgsqlDbType.Smallint)]
    public void MapToNpgsqlDbType_IntegerTypes_MapsCorrectly(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    #endregion

    #region MapToNpgsqlDbType — boolean

    [Theory]
    [InlineData("bit", NpgsqlDbType.Boolean)]
    [InlineData("boolean", NpgsqlDbType.Boolean)]
    public void MapToNpgsqlDbType_BooleanTypes_MapsCorrectly(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    #endregion

    #region MapToNpgsqlDbType — string types

    [Theory]
    [InlineData("varchar", NpgsqlDbType.Text)]
    [InlineData("nvarchar", NpgsqlDbType.Text)]
    [InlineData("char", NpgsqlDbType.Text)]
    [InlineData("nchar", NpgsqlDbType.Text)]
    [InlineData("text", NpgsqlDbType.Text)]
    [InlineData("ntext", NpgsqlDbType.Text)]
    public void MapToNpgsqlDbType_StringTypes_MapsToText(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    #endregion

    #region MapToNpgsqlDbType — date/time types

    [Theory]
    [InlineData("datetime", NpgsqlDbType.Timestamp)]
    [InlineData("datetime2", NpgsqlDbType.Timestamp)]
    [InlineData("smalldatetime", NpgsqlDbType.Timestamp)]
    [InlineData("datetimeoffset", NpgsqlDbType.TimestampTz)]
    [InlineData("date", NpgsqlDbType.Date)]
    [InlineData("time", NpgsqlDbType.Time)]
    public void MapToNpgsqlDbType_DateTimeTypes_MapsCorrectly(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    #endregion

    #region MapToNpgsqlDbType — numeric types

    [Theory]
    [InlineData("decimal", NpgsqlDbType.Numeric)]
    [InlineData("numeric", NpgsqlDbType.Numeric)]
    [InlineData("money", NpgsqlDbType.Numeric)]
    [InlineData("smallmoney", NpgsqlDbType.Numeric)]
    [InlineData("float", NpgsqlDbType.Double)]
    [InlineData("real", NpgsqlDbType.Real)]
    public void MapToNpgsqlDbType_NumericTypes_MapsCorrectly(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    #endregion

    #region MapToNpgsqlDbType — binary and special types

    [Theory]
    [InlineData("uniqueidentifier", NpgsqlDbType.Uuid)]
    [InlineData("varbinary", NpgsqlDbType.Bytea)]
    [InlineData("binary", NpgsqlDbType.Bytea)]
    [InlineData("image", NpgsqlDbType.Bytea)]
    [InlineData("timestamp", NpgsqlDbType.Bytea)]
    [InlineData("rowversion", NpgsqlDbType.Bytea)]
    [InlineData("xml", NpgsqlDbType.Xml)]
    public void MapToNpgsqlDbType_SpecialTypes_MapsCorrectly(string sqlType, NpgsqlDbType expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.MapToNpgsqlDbType(sqlType));
    }

    [Fact]
    public void MapToNpgsqlDbType_UnknownType_FallsBackToText()
    {
        Assert.Equal(NpgsqlDbType.Text, CrossPlatformBulkCopier.MapToNpgsqlDbType("sql_variant"));
        Assert.Equal(NpgsqlDbType.Text, CrossPlatformBulkCopier.MapToNpgsqlDbType("geography"));
        Assert.Equal(NpgsqlDbType.Text, CrossPlatformBulkCopier.MapToNpgsqlDbType("unknown_type"));
    }

    #endregion

    #region ConvertValue — null handling

    [Fact]
    public void ConvertValue_Null_ReturnsNull()
    {
        Assert.Null(CrossPlatformBulkCopier.ConvertValue(null, "int"));
    }

    [Fact]
    public void ConvertValue_DBNull_ReturnsNull()
    {
        Assert.Null(CrossPlatformBulkCopier.ConvertValue(DBNull.Value, "varchar"));
    }

    #endregion

    #region ConvertValue — tinyint (byte → short)

    [Theory]
    [InlineData((byte)0, (short)0)]
    [InlineData((byte)1, (short)1)]
    [InlineData((byte)255, (short)255)]
    public void ConvertValue_Tinyint_ByteToShort(byte input, short expected)
    {
        var result = CrossPlatformBulkCopier.ConvertValue(input, "tinyint");
        Assert.IsType<short>(result);
        Assert.Equal(expected, (short)result!);
    }

    #endregion

    #region ConvertValue — bit (boolean passthrough)

    [Fact]
    public void ConvertValue_Bit_BoolPassesThrough()
    {
        var result = CrossPlatformBulkCopier.ConvertValue(true, "bit");
        Assert.IsType<bool>(result);
        Assert.True((bool)result!);
    }

    [Fact]
    public void ConvertValue_Bit_IntConvertsToBoolean()
    {
        Assert.True((bool)CrossPlatformBulkCopier.ConvertValue(1, "bit")!);
        Assert.False((bool)CrossPlatformBulkCopier.ConvertValue(0, "bit")!);
    }

    [Fact]
    public void ConvertValue_Bit_ByteConvertsToBoolean()
    {
        Assert.True((bool)CrossPlatformBulkCopier.ConvertValue((byte)1, "bit")!);
        Assert.False((bool)CrossPlatformBulkCopier.ConvertValue((byte)0, "bit")!);
    }

    #endregion

    #region ConvertValue — passthrough types

    [Theory]
    [InlineData(42, "int")]
    [InlineData(42L, "bigint")]
    [InlineData("hello", "varchar")]
    [InlineData(3.14, "float")]
    public void ConvertValue_StandardTypes_PassThrough(object input, string sqlType)
    {
        var result = CrossPlatformBulkCopier.ConvertValue(input, sqlType);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ConvertValue_Guid_PassesThrough()
    {
        var guid = Guid.NewGuid();
        var result = CrossPlatformBulkCopier.ConvertValue(guid, "uniqueidentifier");
        Assert.Equal(guid, result);
    }

    [Fact]
    public void ConvertValue_DateTime_PassesThrough()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);
        var result = CrossPlatformBulkCopier.ConvertValue(dt, "datetime");
        Assert.Equal(dt, result);
    }

    [Fact]
    public void ConvertValue_ByteArray_PassesThrough()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        var result = CrossPlatformBulkCopier.ConvertValue(bytes, "varbinary");
        Assert.Equal(bytes, result);
    }

    [Fact]
    public void ConvertValue_Decimal_PassesThrough()
    {
        var val = 123.45m;
        var result = CrossPlatformBulkCopier.ConvertValue(val, "decimal");
        Assert.Equal(val, result);
    }

    #endregion

    #region QuotePgName

    [Theory]
    [InlineData("dbo.Users", "\"public\".\"Users\"")]
    [InlineData("DBO.Users", "\"public\".\"Users\"")]
    [InlineData("[dbo].[Users]", "\"public\".\"Users\"")]
    [InlineData("Sales.Orders", "\"Sales\".\"Orders\"")]
    [InlineData("[Sales].[Orders]", "\"Sales\".\"Orders\"")]
    [InlineData("Users", "\"Users\"")]
    public void QuotePgName_FormatsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.QuotePgName(input));
    }

    [Fact]
    public void QuotePgName_DboSchema_MapsToPublic()
    {
        var result = CrossPlatformBulkCopier.QuotePgName("dbo.Customers");
        Assert.StartsWith("\"public\"", result);
        Assert.EndsWith("\"Customers\"", result);
    }

    [Fact]
    public void QuotePgName_CustomSchema_Preserved()
    {
        var result = CrossPlatformBulkCopier.QuotePgName("Reporting.Metrics");
        Assert.StartsWith("\"Reporting\"", result);
        Assert.EndsWith("\"Metrics\"", result);
    }

    #endregion

    #region QuoteSqlName

    [Theory]
    [InlineData("dbo.Users", "[dbo].[Users]")]
    [InlineData("Sales.Orders", "[Sales].[Orders]")]
    [InlineData("[dbo].[Users]", "[dbo].[Users]")]
    [InlineData("Users", "[Users]")]
    public void QuoteSqlName_FormatsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.QuoteSqlName(input));
    }

    #endregion

    #region ClampTimeout

    [Theory]
    [InlineData(null, 600, 600)]    // null → default
    [InlineData(100, 600, 100)]     // explicit value
    [InlineData(10, 600, 30)]       // below min → clamped to 30
    [InlineData(5000, 600, 3600)]   // above max → clamped to 3600
    [InlineData(30, 600, 30)]       // exact min
    [InlineData(3600, 600, 3600)]   // exact max
    public void ClampTimeout_ClampsCorrectly(int? value, int defaultValue, int expected)
    {
        Assert.Equal(expected, CrossPlatformBulkCopier.ClampTimeout(value, defaultValue));
    }

    #endregion

    #region TopologicalSort

    [Fact]
    public void TopologicalSort_NoDependencies_PreservesOrder()
    {
        var tables = new[] { "A", "B", "C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [],
            ["B"] = [],
            ["C"] = []
        };

        var result = CrossPlatformBulkCopier.TopologicalSort(tables, deps);
        Assert.Equal(3, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
    }

    [Fact]
    public void TopologicalSort_LinearDependency_ParentFirst()
    {
        // C depends on B, B depends on A → A, B, C
        var tables = new[] { "C", "B", "A" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [],
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { "A" },
            ["C"] = new(StringComparer.OrdinalIgnoreCase) { "B" }
        };

        var result = CrossPlatformBulkCopier.TopologicalSort(tables, deps);
        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("A") < result.IndexOf("B"), "A should come before B");
        Assert.True(result.IndexOf("B") < result.IndexOf("C"), "B should come before C");
    }

    [Fact]
    public void TopologicalSort_MultipleDependencies_AllParentsFirst()
    {
        // Orders depends on Users and Products
        var tables = new[] { "dbo.Orders", "dbo.Users", "dbo.Products" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.Users"] = [],
            ["dbo.Products"] = [],
            ["dbo.Orders"] = new(StringComparer.OrdinalIgnoreCase) { "dbo.Users", "dbo.Products" }
        };

        var result = CrossPlatformBulkCopier.TopologicalSort(tables, deps);
        Assert.Equal(3, result.Count);
        var ordersIdx = result.IndexOf("dbo.Orders");
        var usersIdx = result.IndexOf("dbo.Users");
        var productsIdx = result.IndexOf("dbo.Products");
        Assert.True(usersIdx < ordersIdx, "Users should come before Orders");
        Assert.True(productsIdx < ordersIdx, "Products should come before Orders");
    }

    [Fact]
    public void TopologicalSort_CircularDependency_AppendedAtEnd()
    {
        // A depends on B, B depends on A (cycle)
        var tables = new[] { "A", "B", "C" };
        var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { "B" },
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { "A" },
            ["C"] = []
        };

        var result = CrossPlatformBulkCopier.TopologicalSort(tables, deps);
        // All tables should be present
        Assert.Equal(3, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
        // C has no deps, so it should be first
        Assert.Equal("C", result[0]);
    }

    [Fact]
    public void TopologicalSort_EmptyList_ReturnsEmpty()
    {
        var result = CrossPlatformBulkCopier.TopologicalSort(
            Array.Empty<string>(),
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
        Assert.Empty(result);
    }

    #endregion
}