using Scaffold.Assessment.SqlServer;

namespace Scaffold.Integration.Tests;

/// <summary>
/// Integration tests for PerformanceProfiler against a real SQL Server instance.
/// </summary>
[Collection("SqlServer")]
public class PerformanceProfilerIntegrationTests
{
    private readonly SqlServerFixture _fixture;

    public PerformanceProfilerIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CollectAsync_ReturnsProfile()
    {
        var profile = await PerformanceProfiler.CollectAsync(_fixture.Connection);

        Assert.NotNull(profile);
    }

    [Fact]
    public async Task CollectAsync_CpuPercentIsNonNegative()
    {
        var profile = await PerformanceProfiler.CollectAsync(_fixture.Connection);

        Assert.True(profile.AvgCpuPercent >= 0, $"CPU % should be >= 0, got {profile.AvgCpuPercent}");
    }

    [Fact]
    public async Task CollectAsync_DatabaseSizeIsPositive()
    {
        var profile = await PerformanceProfiler.CollectAsync(_fixture.Connection);

        Assert.True(profile.MaxDatabaseSizeMb > 0, $"DB size should be > 0, got {profile.MaxDatabaseSizeMb}");
    }
}
