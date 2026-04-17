using Microsoft.Extensions.Logging;
using Moq;
using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class PostgreSqlAssessorTests
{
    private readonly PostgreSqlAssessor _assessor;

    public PostgreSqlAssessorTests()
    {
        var mockFactory = new Mock<PostgreSqlConnectionFactory>();
        var mockLogger = new Mock<ILogger<PostgreSqlAssessor>>();
        _assessor = new PostgreSqlAssessor(mockFactory.Object, mockLogger.Object);
    }

    // ── SourcePlatform ──────────────────────────────────────────────

    [Fact]
    public void SourcePlatform_ReturnsPostgreSql()
    {
        Assert.Equal("PostgreSql", _assessor.SourcePlatform);
    }

    // ── Constructor ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithoutPricingService_Succeeds()
    {
        var mockFactory = new Mock<PostgreSqlConnectionFactory>();
        var mockLogger = new Mock<ILogger<PostgreSqlAssessor>>();

        var assessor = new PostgreSqlAssessor(mockFactory.Object, mockLogger.Object);

        Assert.NotNull(assessor);
        Assert.Equal("PostgreSql", assessor.SourcePlatform);
    }

    [Fact]
    public void Constructor_WithPricingService_Succeeds()
    {
        var mockFactory = new Mock<PostgreSqlConnectionFactory>();
        var mockLogger = new Mock<ILogger<PostgreSqlAssessor>>();
        var mockPricing = new Mock<IAzurePricingService>();

        var assessor = new PostgreSqlAssessor(mockFactory.Object, mockLogger.Object, mockPricing.Object);

        Assert.NotNull(assessor);
        Assert.Equal("PostgreSql", assessor.SourcePlatform);
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

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", recommendation.ServiceTier);
    }

    [Fact]
    public async Task RecommendTierAsync_ReturnsVm_WhenUnsupportedExtensions()
    {
        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 50, AvgIoMbPerSecond = 50 },
            DataProfile = new DataProfile { TotalSizeBytes = 10L * 1_073_741_824L },
            CompatibilityScore = 80,
            CompatibilityIssues =
            [
                new CompatibilityIssue { IssueType = "Custom C Extensions", IsBlocking = true, Severity = CompatibilitySeverity.Unsupported },
                new CompatibilityIssue { IssueType = "Custom C Extensions", IsBlocking = true, Severity = CompatibilitySeverity.Unsupported },
                new CompatibilityIssue { IssueType = "Custom C Extensions", IsBlocking = true, Severity = CompatibilitySeverity.Unsupported }
            ]
        };

        var recommendation = await _assessor.RecommendTierAsync(report);

        Assert.Equal("PostgreSQL on Azure VM", recommendation.ServiceTier);
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
    public async Task RecommendTierAsync_WithPricingService_IncludesRegionalPricing()
    {
        var mockFactory = new Mock<PostgreSqlConnectionFactory>();
        var mockLogger = new Mock<ILogger<PostgreSqlAssessor>>();
        var mockPricing = new Mock<IAzurePricingService>();

        mockPricing
            .Setup(p => p.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "eastus", EstimatedMonthlyCostUsd = 100m }
            });

        var assessor = new PostgreSqlAssessor(mockFactory.Object, mockLogger.Object, mockPricing.Object);

        var report = new AssessmentReport
        {
            Performance = new PerformanceProfile { AvgCpuPercent = 5, AvgIoMbPerSecond = 0.5 },
            DataProfile = new DataProfile { TotalSizeBytes = 1_073_741_824L },
            CompatibilityScore = 100
        };

        var rec = await assessor.RecommendTierAsync(report);

        Assert.Equal("eastus", rec.RecommendedRegion);
        Assert.Equal(100m, rec.EstimatedMonthlyCostUsd);
        Assert.NotNull(rec.RegionalPricing);
        Assert.Single(rec.RegionalPricing);
    }
}