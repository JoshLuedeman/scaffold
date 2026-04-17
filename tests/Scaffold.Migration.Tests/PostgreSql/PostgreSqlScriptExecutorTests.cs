using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlScriptExecutorTests
{
    #region ClampTimeout

    [Theory]
    [InlineData(null, 300, 300)]    // null -> default
    [InlineData(60, 300, 60)]       // explicit value within range
    [InlineData(10, 300, 30)]       // below min -> clamped to 30
    [InlineData(9999, 300, 3600)]   // above max -> clamped to 3600
    [InlineData(30, 300, 30)]       // exactly min
    [InlineData(3600, 300, 3600)]   // exactly max
    public void ClampTimeout_ReturnsExpected(int? input, int defaultVal, int expected)
    {
        var result = PostgreSqlScriptExecutor.ClampTimeout(input, defaultVal);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, 100, 50, 500, 100)]  // null -> default within custom bounds
    [InlineData(25, 100, 50, 500, 50)]     // below custom min -> clamped
    [InlineData(600, 100, 50, 500, 500)]   // above custom max -> clamped
    [InlineData(75, 100, 50, 500, 75)]     // within custom bounds
    public void ClampTimeout_CustomBounds_ReturnsExpected(int? input, int defaultVal, int min, int max, int expected)
    {
        var result = PostgreSqlScriptExecutor.ClampTimeout(input, defaultVal, min, max);
        Assert.Equal(expected, result);
    }

    #endregion
}