using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests;

public class TimeoutClampingTests
{
    #region BulkDataCopier.ClampTimeout

    [Theory]
    [InlineData(null, 600, 600)]    // null → default
    [InlineData(120, 600, 120)]     // explicit value within range
    [InlineData(10, 600, 30)]       // below min → clamped to 30
    [InlineData(5000, 600, 3600)]   // above max → clamped to 3600
    [InlineData(30, 600, 30)]       // exactly min
    [InlineData(3600, 600, 3600)]   // exactly max
    public void BulkDataCopier_ClampTimeout_ReturnsExpected(int? input, int defaultVal, int expected)
    {
        var result = BulkDataCopier.ClampTimeout(input, defaultVal);
        Assert.Equal(expected, result);
    }

    #endregion

    #region ScriptExecutor.ClampTimeout

    [Theory]
    [InlineData(null, 300, 300)]    // null → default
    [InlineData(60, 300, 60)]       // explicit value within range
    [InlineData(10, 300, 30)]       // below min → clamped to 30
    [InlineData(9999, 300, 3600)]   // above max → clamped to 3600
    [InlineData(30, 300, 30)]       // exactly min
    [InlineData(3600, 300, 3600)]   // exactly max
    public void ScriptExecutor_ClampTimeout_ReturnsExpected(int? input, int defaultVal, int expected)
    {
        var result = ScriptExecutor.ClampTimeout(input, defaultVal);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Custom min/max bounds

    [Theory]
    [InlineData(null, 100, 50, 500, 100)]  // null → default within custom bounds
    [InlineData(25, 100, 50, 500, 50)]     // below custom min → clamped
    [InlineData(600, 100, 50, 500, 500)]   // above custom max → clamped
    public void ClampTimeout_CustomBounds_ReturnsExpected(int? input, int defaultVal, int min, int max, int expected)
    {
        var result = BulkDataCopier.ClampTimeout(input, defaultVal, min, max);
        Assert.Equal(expected, result);
    }

    #endregion
}