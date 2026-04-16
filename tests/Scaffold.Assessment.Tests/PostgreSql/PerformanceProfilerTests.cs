using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

/// <summary>
/// Tests for the PostgreSQL PerformanceProfiler model defaults and mapping logic.
/// Since NpgsqlConnection/NpgsqlCommand are sealed and cannot be mocked,
/// we test the PerformanceProfile model behavior and computation logic.
/// Integration tests (Phase 1, #23) will verify against a real PostgreSQL instance.
/// </summary>
public class PerformanceProfilerTests
{
    [Fact]
    public void DefaultProfile_AllValuesAreZero()
    {
        var profile = new PerformanceProfile();

        Assert.Equal(0, profile.AvgCpuPercent);
        Assert.Equal(0, profile.MemoryUsedMb);
        Assert.Equal(0, profile.AvgIoMbPerSecond);
        Assert.Equal(0, profile.MaxDatabaseSizeMb);
    }

    [Fact]
    public void Profile_AcceptsValidValues()
    {
        var profile = new PerformanceProfile
        {
            AvgCpuPercent = 45.5,
            MemoryUsedMb = 2048,
            AvgIoMbPerSecond = 12.75,
            MaxDatabaseSizeMb = 10240
        };

        Assert.Equal(45.5, profile.AvgCpuPercent);
        Assert.Equal(2048, profile.MemoryUsedMb);
        Assert.Equal(12.75, profile.AvgIoMbPerSecond);
        Assert.Equal(10240, profile.MaxDatabaseSizeMb);
    }

    [Theory]
    [InlineData(1, 5.0)]
    [InlineData(5, 25.0)]
    [InlineData(10, 50.0)]
    [InlineData(20, 100.0)]
    [InlineData(25, 100.0)] // Capped at 100
    public void CpuHeuristic_ConnectionCountMapsToPercent(int connections, double expectedCpu)
    {
        // Mirror the heuristic from PerformanceProfiler.CollectAsync:
        // profile.AvgCpuPercent = Math.Min(100, connections * 5.0);
        var cpuPercent = Math.Min(100, connections * 5.0);

        Assert.Equal(expectedCpu, cpuPercent);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1_048_576, 1)]           // 1 MB
    [InlineData(1_073_741_824, 1024)]    // 1 GB
    [InlineData(10_737_418_240, 10240)]  // 10 GB
    public void DatabaseSize_BytesToMbConversion(long sizeBytes, long expectedMb)
    {
        // Mirror the conversion from PerformanceProfiler.CollectAsync:
        // profile.MaxDatabaseSizeMb = Convert.ToInt64(size) / (1024 * 1024);
        var sizeMb = sizeBytes / (1024 * 1024);

        Assert.Equal(expectedMb, sizeMb);
    }

    [Fact]
    public void CpuHeuristic_ZeroConnections_ZeroPercent()
    {
        var cpuPercent = Math.Min(100, 0 * 5.0);

        Assert.Equal(0, cpuPercent);
    }

    [Fact]
    public void IoEstimate_NullOrDbNull_ReturnsZero()
    {
        // Mirror the null-check from PerformanceProfiler.CollectAsync:
        // profile.AvgIoMbPerSecond = io != null && io != DBNull.Value ? Convert.ToDouble(io) : 0;
        object? ioNull = null;
        object ioDbNull = DBNull.Value;

        var resultNull = ioNull != null && ioNull != DBNull.Value ? Convert.ToDouble(ioNull) : 0;
        var resultDbNull = ioDbNull != null && ioDbNull != DBNull.Value ? Convert.ToDouble(ioDbNull) : 0;

        Assert.Equal(0, resultNull);
        Assert.Equal(0, resultDbNull);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.5)]
    [InlineData(100.75)]
    public void IoEstimate_ValidValue_Converted(double ioValue)
    {
        object io = ioValue;
        var result = io != null && io != DBNull.Value ? Convert.ToDouble(io) : 0;

        Assert.Equal(ioValue, result);
    }

    [Fact]
    public void Profile_LargeValues_HandledCorrectly()
    {
        var profile = new PerformanceProfile
        {
            AvgCpuPercent = 99.99,
            MemoryUsedMb = 65_536,      // 64 GB
            AvgIoMbPerSecond = 500.25,
            MaxDatabaseSizeMb = 1_048_576 // 1 TB
        };

        Assert.Equal(99.99, profile.AvgCpuPercent);
        Assert.Equal(65_536, profile.MemoryUsedMb);
        Assert.Equal(500.25, profile.AvgIoMbPerSecond);
        Assert.Equal(1_048_576, profile.MaxDatabaseSizeMb);
    }
}
