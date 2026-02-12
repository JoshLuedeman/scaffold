using Microsoft.Extensions.Logging;
using Moq;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class SqlServerAssessorTests
{
    private readonly SqlServerAssessor _assessor;

    public SqlServerAssessorTests()
    {
        var mockFactory = new Mock<SqlServerConnectionFactory>();
        var mockLogger = new Mock<ILogger<SqlServerAssessor>>();
        _assessor = new SqlServerAssessor(mockFactory.Object, mockLogger.Object);
    }

    // ── SourcePlatform ──────────────────────────────────────────────

    [Fact]
    public void SourcePlatform_ReturnsSqlServer()
    {
        Assert.Equal("SqlServer", _assessor.SourcePlatform);
    }

    // ── RecommendTierAsync ──────────────────────────────────────────

    [Fact]
    public async Task RecommendTierAsync_DelegatesToTierRecommender()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 5, AvgIoMbPerSecond = 0.5 },
            DataProfile = new DataProfile { TotalSizeBytes = 1_073_741_824L },
            CompatibilityScore = 100
        };

        var recommendation = await _assessor.RecommendTierAsync(report);

        Assert.Equal("Azure SQL Database", recommendation.ServiceTier);
    }

    [Fact]
    public async Task RecommendTierAsync_ReturnsHyperscale_ForLargeDb()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 50, AvgIoMbPerSecond = 50 },
            DataProfile = new DataProfile { TotalSizeBytes = 2L * 1_073_741_824L * 1024 }, // 2 TB
            CompatibilityScore = 100
        };

        var recommendation = await _assessor.RecommendTierAsync(report);

        Assert.Equal("Azure SQL Database Hyperscale", recommendation.ServiceTier);
    }

    [Fact]
    public async Task RecommendTierAsync_ReturnsNonNullRecommendation()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile(),
            DataProfile = new DataProfile(),
            CompatibilityScore = 100
        };

        var recommendation = await _assessor.RecommendTierAsync(report);

        Assert.NotNull(recommendation);
        Assert.False(string.IsNullOrEmpty(recommendation.ServiceTier));
    }

    // ── AssessmentReport model integration ──────────────────────────

    [Fact]
    public void AssessmentReport_DefaultValues()
    {
        var report = new AssessmentReport();

        Assert.NotEqual(default, report.GeneratedAt);
        Assert.NotNull(report.Schema);
        Assert.NotNull(report.DataProfile);
        Assert.NotNull(report.Performance);
        Assert.NotNull(report.CompatibilityIssues);
        Assert.NotNull(report.Recommendation);
        Assert.Empty(report.CompatibilityIssues);
    }

    [Fact]
    public void AssessmentReport_CanHoldFullAssessment()
    {
        var report = new AssessmentReport
        {
            Id = Guid.NewGuid(),
            Schema = new SchemaInventory { TableCount = 10 },
            Performance = new PerformanceProfile { AvgCpuPercent = 45 },
            DataProfile = new DataProfile { TotalSizeBytes = 50_000_000_000 },
            CompatibilityIssues = [new CompatibilityIssue { IsBlocking = true }],
            CompatibilityScore = 80.0,
            Risk = RiskRating.High,
            Recommendation = new TierRecommendation { ServiceTier = "Azure SQL Database" }
        };

        Assert.Equal(10, report.Schema.TableCount);
        Assert.Equal(45, report.Performance.AvgCpuPercent);
        Assert.Single(report.CompatibilityIssues);
        Assert.Equal(RiskRating.High, report.Risk);
        Assert.Equal("Azure SQL Database", report.Recommendation.ServiceTier);
    }

    // ── End-to-end orchestration (without DB) ───────────────────────

    [Fact]
    public async Task RecommendTierAsync_SetsAllRecommendationFields()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 20, AvgIoMbPerSecond = 10 },
            DataProfile = new DataProfile { TotalSizeBytes = 10L * 1_073_741_824L },
            CompatibilityScore = 100
        };

        var rec = await _assessor.RecommendTierAsync(report);

        Assert.NotNull(rec.ServiceTier);
        Assert.NotNull(rec.ComputeSize);
        Assert.True(rec.StorageGb > 0);
        Assert.True(rec.EstimatedMonthlyCostUsd > 0);
        Assert.NotNull(rec.Reasoning);
    }

    [Fact]
    public async Task RecommendTierAsync_CompatibilityAndTier_IndependentConcerns()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 85, AvgIoMbPerSecond = 50 },
            DataProfile = new DataProfile { TotalSizeBytes = 50L * 1_073_741_824L },
            CompatibilityIssues = [new CompatibilityIssue { IsBlocking = true }],
            CompatibilityScore = 80.0,
            Risk = RiskRating.High
        };

        // Tier recommendation is based on perf/data, not compatibility
        var rec = await _assessor.RecommendTierAsync(report);

        Assert.Equal("Azure SQL Database", rec.ServiceTier);
    }
}
