using Scaffold.Assessment;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class MigrationStrategyRecommenderTests
{
    private static DataProfile CreateDataProfile(long sizeBytes, long totalRows = 1000, List<TableProfile>? tables = null) => new()
    {
        TotalSizeBytes = sizeBytes,
        TotalRowCount = totalRows,
        Tables = tables ?? [new TableProfile { TableName = "Users", RowCount = totalRows, SizeBytes = sizeBytes }]
    };

    private static SchemaInventory CreateSchema(int tableCount = 10, int triggerCount = 0, List<SchemaObject>? objects = null) => new()
    {
        TableCount = tableCount,
        ViewCount = 0,
        StoredProcedureCount = 0,
        IndexCount = tableCount * 2,
        TriggerCount = triggerCount,
        Objects = objects ?? []
    };

    private static PerformanceProfile CreatePerformance(double avgIoMbPerSec = 5.0) => new()
    {
        AvgCpuPercent = 10,
        MemoryUsedMb = 1024,
        AvgIoMbPerSecond = avgIoMbPerSec,
        MaxDatabaseSizeMb = 10240
    };

    #region Small database → Cutover

    [Fact]
    public void SmallDb_SamePlatform_RecommendsCutover()
    {
        // 1 GB, 10 tables → Cutover
        var data = CreateDataProfile(1L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 10);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.Cutover, result.RecommendedStrategy);
        Assert.Contains("small", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1L * 1024 * 1024 * 1024)]  // 1 GB
    [InlineData(5L * 1024 * 1024 * 1024)]  // 5 GB
    public void SmallDb_VariousSizes_RecommendsCutover(long sizeBytes)
    {
        var data = CreateDataProfile(sizeBytes);
        var schema = CreateSchema(tableCount: 10);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.Cutover, result.RecommendedStrategy);
    }

    #endregion

    #region Large database → ContinuousSync (same-platform)

    [Fact]
    public void LargeDb_SamePlatform_RecommendsContinuousSync()
    {
        // 200 GB → ContinuousSync
        var data = CreateDataProfile(200L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 500);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.ContinuousSync, result.RecommendedStrategy);
        Assert.Contains("large", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LargeDb_At100GB_Boundary_RecommendsContinuousSync()
    {
        // Exactly 100 GB → ContinuousSync
        var data = CreateDataProfile(100L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 200);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.ContinuousSync, result.RecommendedStrategy);
    }

    #endregion

    #region Cross-platform → always Cutover

    [Theory]
    [InlineData(0)]
    [InlineData(1L * 1024 * 1024 * 1024)]     // 1 GB
    [InlineData(50L * 1024 * 1024 * 1024)]     // 50 GB
    [InlineData(200L * 1024 * 1024 * 1024)]    // 200 GB
    public void CrossPlatform_PostgreSql_AlwaysCutover(long sizeBytes)
    {
        var data = CreateDataProfile(sizeBytes);
        var schema = CreateSchema(tableCount: 200);
        var perf = CreatePerformance(avgIoMbPerSec: 100);

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.PostgreSql);

        Assert.Equal(MigrationStrategy.Cutover, result.RecommendedStrategy);
        Assert.Contains("cross-platform", result.Reasoning, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.EstimatedDowntimeContinuousSync);
    }

    [Fact]
    public void CrossPlatform_ContinuousSyncNotAvailable_InConsiderations()
    {
        var data = CreateDataProfile(50L * 1024 * 1024 * 1024);
        var schema = CreateSchema();
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.PostgreSql);

        Assert.Contains(result.Considerations, c => c.Contains("ContinuousSync") && c.Contains("not available"));
    }

    #endregion

    #region Medium database — activity-based

    [Fact]
    public void MediumDb_HighActivity_SamePlatform_RecommendsContinuousSync()
    {
        // 50 GB, high IO
        var data = CreateDataProfile(50L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 200);
        var perf = CreatePerformance(avgIoMbPerSec: 75);

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.ContinuousSync, result.RecommendedStrategy);
        Assert.Contains("high activity", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MediumDb_LowActivity_SamePlatform_RecommendsCutover()
    {
        // 50 GB, low IO
        var data = CreateDataProfile(50L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 200);
        var perf = CreatePerformance(avgIoMbPerSec: 10);

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.Cutover, result.RecommendedStrategy);
        Assert.Contains("low activity", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Downtime estimation

    [Fact]
    public void DowntimeEstimation_SmallDb_ReasonableTime()
    {
        // 1 GB = ~1024 MB, at 100 MB/min ≈ 10.24 min + schema overhead
        var data = CreateDataProfile(1L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 10);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.True(result.EstimatedDowntimeCutover > TimeSpan.Zero);
        Assert.True(result.EstimatedDowntimeCutover < TimeSpan.FromMinutes(30), "1 GB should take < 30 min");
    }

    [Fact]
    public void DowntimeEstimation_LargeDb_LongerTime()
    {
        // 100 GB ≈ 102400 MB, at 100 MB/min ≈ 1024 min + schema
        var data = CreateDataProfile(100L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 500);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.True(result.EstimatedDowntimeCutover > TimeSpan.FromHours(1), "100 GB should take > 1 hour");
    }

    [Fact]
    public void DowntimeEstimation_SamePlatform_ContinuousSyncHasValue()
    {
        var data = CreateDataProfile(50L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 200);
        var perf = CreatePerformance(avgIoMbPerSec: 75);

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.NotNull(result.EstimatedDowntimeContinuousSync);
        Assert.True(result.EstimatedDowntimeContinuousSync < result.EstimatedDowntimeCutover,
            "ContinuousSync downtime should be less than Cutover for medium/large DBs");
    }

    [Fact]
    public void DowntimeEstimation_ZeroBytes_MinimalTime()
    {
        var data = CreateDataProfile(0);
        var schema = CreateSchema(tableCount: 0);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.True(result.EstimatedDowntimeCutover > TimeSpan.Zero, "Should still have some overhead even for empty DB");
        Assert.True(result.EstimatedDowntimeCutover < TimeSpan.FromMinutes(5), "Empty DB should finish very quickly");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void ZeroTables_ZeroBytes_RecommendsCutover()
    {
        var data = CreateDataProfile(0, totalRows: 0, tables: []);
        var schema = CreateSchema(tableCount: 0);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Equal(MigrationStrategy.Cutover, result.RecommendedStrategy);
    }

    [Fact]
    public void VeryLargeDb_Over500GB_AddsSpaceConsideration()
    {
        var data = CreateDataProfile(600L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 1000);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Contains(result.Considerations, c => c.Contains("500 GB"));
    }

    #endregion

    #region Considerations

    [Fact]
    public void Triggers_AddedToConsiderations()
    {
        var data = CreateDataProfile(1L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 10, triggerCount: 5);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Contains(result.Considerations, c => c.Contains("trigger") && c.Contains("5"));
    }

    [Fact]
    public void ManyForeignKeys_AddedToConsiderations()
    {
        var fkObjects = Enumerable.Range(0, 25)
            .Select(i => new SchemaObject
            {
                Name = $"FK_{i}",
                ObjectType = "FOREIGN_KEY_CONSTRAINT"
            })
            .ToList();

        var data = CreateDataProfile(1L * 1024 * 1024 * 1024);
        var schema = CreateSchema(tableCount: 10, objects: fkObjects);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Contains(result.Considerations, c => c.Contains("foreign key") && c.Contains("25"));
    }

    [Fact]
    public void LargeTable_AddedToConsiderations()
    {
        var tables = new List<TableProfile>
        {
            new() { TableName = "BigTable", RowCount = 50_000_000, SizeBytes = 5L * 1024 * 1024 * 1024 }
        };
        var data = CreateDataProfile(5L * 1024 * 1024 * 1024, totalRows: 50_000_000, tables: tables);
        var schema = CreateSchema(tableCount: 10);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.Contains(result.Considerations, c => c.Contains("10 million rows"));
    }

    [Fact]
    public void NoSpecialConditions_EmptyConsiderations()
    {
        var data = CreateDataProfile(1L * 1024 * 1024 * 1024, totalRows: 1000, tables:
            [new TableProfile { TableName = "Small", RowCount = 1000, SizeBytes = 1024 }]);
        var schema = CreateSchema(tableCount: 10, triggerCount: 0, objects: []);
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        // No triggers, no FKs over 20, no large tables, not over 500 GB
        Assert.Empty(result.Considerations);
    }

    [Fact]
    public void ReasoningIsNotEmpty()
    {
        var data = CreateDataProfile(1L * 1024 * 1024 * 1024);
        var schema = CreateSchema();
        var perf = CreatePerformance();

        var result = MigrationStrategyRecommender.Recommend(data, schema, perf, DatabasePlatform.SqlServer);

        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    #endregion
}