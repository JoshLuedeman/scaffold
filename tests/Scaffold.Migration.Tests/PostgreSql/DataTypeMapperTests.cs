using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class DataTypeMapperTests
{
    #region Integer type mappings

    [Theory]
    [InlineData("int", "integer")]
    [InlineData("INT", "integer")]
    [InlineData("bigint", "bigint")]
    [InlineData("smallint", "smallint")]
    [InlineData("tinyint", "smallint")]
    [InlineData("bit", "boolean")]
    public void MapType_IntegerTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region String type mappings

    [Theory]
    [InlineData("varchar(50)", "varchar(50)")]
    [InlineData("nvarchar(100)", "varchar(100)")]
    [InlineData("char(10)", "char(10)")]
    [InlineData("nchar(10)", "char(10)")]
    [InlineData("varchar(MAX)", "text")]
    [InlineData("nvarchar(max)", "text")]
    [InlineData("varchar(MAX) ", "text")]
    [InlineData("text", "text")]
    [InlineData("ntext", "text")]
    public void MapType_StringTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapType_VarcharWithMaxLengthParameter_ReturnsText()
    {
        // Using the explicit maxLength parameter with -1 for MAX
        var result = DataTypeMapper.MapType("varchar", maxLength: -1);
        Assert.Equal("text", result);
    }

    [Fact]
    public void MapType_NvarcharWithExplicitLength_ReturnsVarchar()
    {
        var result = DataTypeMapper.MapType("nvarchar", maxLength: 255);
        Assert.Equal("varchar(255)", result);
    }

    #endregion

    #region Date/time type mappings

    [Theory]
    [InlineData("datetime", "timestamp")]
    [InlineData("datetime2", "timestamp")]
    [InlineData("datetime2(3)", "timestamp(3)")]
    [InlineData("datetime2(7)", "timestamp(7)")]
    [InlineData("datetimeoffset", "timestamptz")]
    [InlineData("smalldatetime", "timestamp(0)")]
    [InlineData("date", "date")]
    [InlineData("time", "time")]
    [InlineData("time(3)", "time(3)")]
    public void MapType_DateTimeTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Numeric type mappings

    [Theory]
    [InlineData("decimal(18,2)", "numeric(18,2)")]
    [InlineData("numeric(10,4)", "numeric(10,4)")]
    [InlineData("decimal(5)", "numeric(5)")]
    [InlineData("money", "numeric(19,4)")]
    [InlineData("smallmoney", "numeric(10,4)")]
    [InlineData("float", "double precision")]
    [InlineData("real", "real")]
    public void MapType_NumericTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapType_DecimalWithExplicitPrecisionScale_ReturnsNumeric()
    {
        var result = DataTypeMapper.MapType("decimal", precision: 18, scale: 2);
        Assert.Equal("numeric(18,2)", result);
    }

    [Theory]
    [InlineData("float(24)", "real")]
    [InlineData("float(25)", "double precision")]
    [InlineData("float(53)", "double precision")]
    [InlineData("float(1)", "real")]
    public void MapType_FloatWithPrecision_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Binary type mappings

    [Theory]
    [InlineData("varbinary(100)", "bytea")]
    [InlineData("varbinary(MAX)", "bytea")]
    [InlineData("binary(16)", "bytea")]
    [InlineData("image", "bytea")]
    public void MapType_BinaryTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Special type mappings

    [Theory]
    [InlineData("uniqueidentifier", "uuid")]
    [InlineData("xml", "xml")]
    [InlineData("sql_variant", "text")]
    [InlineData("geography", "text")]
    [InlineData("geometry", "text")]
    [InlineData("hierarchyid", "text")]
    [InlineData("rowversion", "bytea")]
    [InlineData("timestamp", "bytea")]
    public void MapType_SpecialTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Case insensitivity

    [Theory]
    [InlineData("INT", "integer")]
    [InlineData("BigInt", "bigint")]
    [InlineData("VARCHAR(50)", "varchar(50)")]
    [InlineData("NVARCHAR(MAX)", "text")]
    [InlineData("DateTime2(3)", "timestamp(3)")]
    [InlineData("UniqueIdentifier", "uuid")]
    public void MapType_CaseInsensitive(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Whitespace handling

    [Theory]
    [InlineData("  int  ", "integer")]
    [InlineData(" varchar(50) ", "varchar(50)")]
    [InlineData("  decimal( 18 , 2 )  ", "numeric(18,2)")]
    public void MapType_HandlesWhitespace(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapType(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Unknown types

    [Fact]
    public void MapType_UnknownType_ReturnsAsIs()
    {
        var result = DataTypeMapper.MapType("customtype");
        Assert.Equal("customtype", result);
    }

    #endregion

    #region HasWarning

    [Theory]
    [InlineData("sql_variant", true)]
    [InlineData("geography", true)]
    [InlineData("geometry", true)]
    [InlineData("hierarchyid", true)]
    [InlineData("rowversion", true)]
    [InlineData("timestamp", true)]
    [InlineData("int", false)]
    [InlineData("varchar", false)]
    [InlineData("datetime", false)]
    public void HasWarning_ReturnsCorrectly(string sqlType, bool expectedHasWarning)
    {
        var result = DataTypeMapper.HasWarning(sqlType, out var warning);
        Assert.Equal(expectedHasWarning, result);

        if (expectedHasWarning)
            Assert.NotEmpty(warning);
        else
            Assert.Empty(warning);
    }

    [Fact]
    public void HasWarning_Geography_MentionsPostGIS()
    {
        DataTypeMapper.HasWarning("geography", out var warning);
        Assert.Contains("PostGIS", warning);
    }

    [Fact]
    public void HasWarning_Hierarchyid_MentionsNoEquivalent()
    {
        DataTypeMapper.HasWarning("hierarchyid", out var warning);
        Assert.Contains("No PostgreSQL equivalent", warning);
    }

    [Fact]
    public void HasWarning_Rowversion_MentionsAutoIncrementing()
    {
        DataTypeMapper.HasWarning("rowversion", out var warning);
        Assert.Contains("auto-incrementing", warning);
    }

    #endregion

    #region MapIdentity

    [Theory]
    [InlineData("int", "integer GENERATED BY DEFAULT AS IDENTITY")]
    [InlineData("bigint", "bigint GENERATED BY DEFAULT AS IDENTITY")]
    [InlineData("smallint", "smallint GENERATED BY DEFAULT AS IDENTITY")]
    [InlineData("tinyint", "smallint GENERATED BY DEFAULT AS IDENTITY")]
    public void MapIdentity_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapIdentity(sqlType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapIdentity_UnknownType_FallsBackToMapTypeWithIdentity()
    {
        var result = DataTypeMapper.MapIdentity("decimal");
        Assert.Contains("GENERATED BY DEFAULT AS IDENTITY", result);
    }

    #endregion

    #region MapIdentitySerial

    [Theory]
    [InlineData("int", "SERIAL")]
    [InlineData("bigint", "BIGSERIAL")]
    [InlineData("smallint", "SMALLSERIAL")]
    [InlineData("tinyint", "SMALLSERIAL")]
    public void MapIdentitySerial_MapsCorrectly(string sqlType, string expected)
    {
        var result = DataTypeMapper.MapIdentitySerial(sqlType);
        Assert.Equal(expected, result);
    }

    #endregion

    #region MapDefaultExpression

    [Fact]
    public void MapDefaultExpression_Null_ReturnsNull()
    {
        Assert.Null(DataTypeMapper.MapDefaultExpression(null));
    }

    [Fact]
    public void MapDefaultExpression_Empty_ReturnsNull()
    {
        Assert.Null(DataTypeMapper.MapDefaultExpression("  "));
    }

    [Theory]
    [InlineData("(getdate())", "CURRENT_TIMESTAMP")]
    [InlineData("((getdate()))", "CURRENT_TIMESTAMP")]
    [InlineData("(getutcdate())", "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')")]
    [InlineData("(sysdatetime())", "CURRENT_TIMESTAMP")]
    [InlineData("(sysutcdatetime())", "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')")]
    [InlineData("(sysdatetimeoffset())", "CURRENT_TIMESTAMP")]
    [InlineData("(newid())", "gen_random_uuid()")]
    [InlineData("(newsequentialid())", "gen_random_uuid()")]
    [InlineData("(user_name())", "CURRENT_USER")]
    [InlineData("(suser_sname())", "CURRENT_USER")]
    [InlineData("(host_name())", "inet_client_addr()")]
    public void MapDefaultExpression_Functions_MapsCorrectly(string sqlDefault, string expected)
    {
        var result = DataTypeMapper.MapDefaultExpression(sqlDefault);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("((0))", "0")]
    [InlineData("((1))", "1")]
    [InlineData("((100))", "100")]
    public void MapDefaultExpression_NumericLiterals_Preserved(string sqlDefault, string expected)
    {
        var result = DataTypeMapper.MapDefaultExpression(sqlDefault);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("(N'default')", "'default'")]
    [InlineData("('hello')", "'hello'")]
    public void MapDefaultExpression_StringLiterals_MapsCorrectly(string sqlDefault, string expected)
    {
        var result = DataTypeMapper.MapDefaultExpression(sqlDefault);
        Assert.Equal(expected, result);
    }

    #endregion
}