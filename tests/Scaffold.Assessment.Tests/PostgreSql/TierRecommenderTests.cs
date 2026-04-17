using Moq;
using Scaffold.Assessment.PostgreSql;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests.PostgreSql;

public class TierRecommenderTests
{
    private const long OneGb = 1_073_741_824L;

    private static PerformanceProfile Perf(double cpu = 0, double io = 0) =>
        new() { AvgCpuPercent = cpu, AvgIoMbPerSecond = io };

    private static DataProfile Data(long sizeBytes = 0) =>
        new() { TotalSizeBytes = sizeBytes };

    // ── Burstable tier ──────────────────────────────────────────────

    [Fact]
    public void LowCpuLowIo_RecommendsBurstable()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 10, io: 5), Data(5 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("B_Standard_B2ms", result.ComputeSize);
        Assert.Equal(2, result.VCores);
        Assert.Null(result.Dtus);
        Assert.Equal(50m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void VeryLowCpu_RecommendsBurstable()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 3, io: 1), Data(1 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("B_Standard_B2ms", result.ComputeSize);
    }

    // ── General Purpose tier ────────────────────────────────────────

    [Fact]
    public void ModerateCpu_RecommendsGeneralPurpose_D2s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 15), Data(10 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("GP_Standard_D2s_v3", result.ComputeSize);
        Assert.Equal(2, result.VCores);
        Assert.Equal(150m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void ModerateHighCpu_RecommendsGeneralPurpose_D4s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 35, io: 30), Data(20 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("GP_Standard_D4s_v3", result.ComputeSize);
        Assert.Equal(4, result.VCores);
        Assert.Equal(300m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void HigherCpu_RecommendsGeneralPurpose_D8s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 45, io: 60), Data(50 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("GP_Standard_D8s_v3", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(600m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void HighCpu_RecommendsGeneralPurpose_D16s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 60, io: 80), Data(100 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("GP_Standard_D16s_v3", result.ComputeSize);
        Assert.Equal(16, result.VCores);
        Assert.Equal(1200m, result.EstimatedMonthlyCostUsd);
    }

    // ── Memory Optimized tier ───────────────────────────────────────

    [Fact]
    public void VeryHighCpu_RecommendsMemoryOptimized_E4s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 75, io: 100), Data(100 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("MO_Standard_E4s_v3", result.ComputeSize);
        Assert.Equal(4, result.VCores);
        Assert.Equal(500m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void ExtremeResources_RecommendsMemoryOptimized_E8s()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 90, io: 200), Data(200 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
        Assert.Equal("MO_Standard_E8s_v3", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(900m, result.EstimatedMonthlyCostUsd);
    }

    // ── VM fallback ─────────────────────────────────────────────────

    [Fact]
    public void UnsupportedIssues_FallsBackToVm()
    {
        // "Superuser Access" is Unsupported on Flexible Server but Supported on VM
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "Superuser Access", ObjectName = "role1", Description = "Needs superuser" },
            new() { IssueType = "Custom C Extensions", ObjectName = "ext1", Description = "Custom C extension" },
            new() { IssueType = "Tablespace (Custom)", ObjectName = "ts1", Description = "Custom tablespace" },
        };

        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb), 70, issues);

        Assert.Equal("PostgreSQL on Azure VM", result.ServiceTier);
        Assert.Contains("Standard_D", result.ComputeSize);
    }

    [Fact]
    public void VmFallback_LowCpu_2vCores()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "Superuser Access", ObjectName = "role1", Description = "Needs superuser" },
            new() { IssueType = "Custom C Extensions", ObjectName = "ext1", Description = "Custom C extension" },
        };

        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb), 70, issues);

        Assert.Equal("PostgreSQL on Azure VM", result.ServiceTier);
        Assert.Equal("Standard_D2s_v5", result.ComputeSize);
        Assert.Equal(2, result.VCores);
        Assert.Equal(200m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void VmFallback_ModerateCpu_4vCores()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "Superuser Access", ObjectName = "role1", Description = "Needs superuser" },
            new() { IssueType = "Custom C Extensions", ObjectName = "ext1", Description = "Custom C extension" },
        };

        var result = TierRecommender.Recommend(Perf(cpu: 50, io: 10), Data(10 * OneGb), 70, issues);

        Assert.Equal("PostgreSQL on Azure VM", result.ServiceTier);
        Assert.Equal("Standard_D4s_v5", result.ComputeSize);
        Assert.Equal(4, result.VCores);
        Assert.Equal(350m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void VmFallback_HighCpu_8vCores()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "Superuser Access", ObjectName = "role1", Description = "Needs superuser" },
            new() { IssueType = "Custom C Extensions", ObjectName = "ext1", Description = "Custom C extension" },
        };

        var result = TierRecommender.Recommend(Perf(cpu: 70, io: 10), Data(10 * OneGb), 70, issues);

        Assert.Equal("PostgreSQL on Azure VM", result.ServiceTier);
        Assert.Equal("Standard_D8s_v5", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(600m, result.EstimatedMonthlyCostUsd);
    }

    // ── Storage headroom (20% buffer) ───────────────────────────────

    [Fact]
    public void StorageIncludesTwentyPercentHeadroom()
    {
        // 10 GB data → 12 GB with 20% headroom
        var result = TierRecommender.Recommend(Perf(cpu: 10, io: 5), Data(10 * OneGb));

        Assert.Equal(12, result.StorageGb);
    }

    [Fact]
    public void StorageHeadroom_RoundsUp()
    {
        // 7 GB → 8.4 → rounds up to 9
        var result = TierRecommender.Recommend(Perf(cpu: 10, io: 5), Data(7 * OneGb));

        Assert.Equal(9, result.StorageGb);
    }

    [Fact]
    public void StorageHeadroom_MinimumOneGb()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 3, io: 1), Data(0));

        Assert.True(result.StorageGb >= 1, "Storage should be at least 1 GB");
    }

    // ── Reasoning is populated ──────────────────────────────────────

    [Fact]
    public void Recommendation_AlwaysIncludesReasoning()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 15), Data(10 * OneGb));

        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    // ── Cost increases with tier ────────────────────────────────────

    [Fact]
    public void CostIncreasesWithTier()
    {
        var burstable = TierRecommender.Recommend(Perf(cpu: 5, io: 2), Data(1 * OneGb));
        var gpSmall = TierRecommender.Recommend(Perf(cpu: 20, io: 15), Data(10 * OneGb));
        var gpLarge = TierRecommender.Recommend(Perf(cpu: 35, io: 30), Data(20 * OneGb));
        var mo = TierRecommender.Recommend(Perf(cpu: 90, io: 200), Data(200 * OneGb));

        Assert.True(burstable.EstimatedMonthlyCostUsd < gpSmall.EstimatedMonthlyCostUsd);
        Assert.True(gpSmall.EstimatedMonthlyCostUsd < gpLarge.EstimatedMonthlyCostUsd);
        Assert.True(gpLarge.EstimatedMonthlyCostUsd < mo.EstimatedMonthlyCostUsd);
    }

    // ── Valid service tiers ─────────────────────────────────────────

    [Fact]
    public void Recommend_AlwaysReturnsValidServiceTier()
    {
        string[] validTiers =
        [
            "Azure Database for PostgreSQL - Flexible Server",
            "PostgreSQL on Azure VM"
        ];

        var profiles = new[]
        {
            (Perf(cpu: 3, io: 1), Data(1 * OneGb)),
            (Perf(cpu: 50, io: 50), Data(50 * OneGb)),
            (Perf(cpu: 90, io: 200), Data(200 * OneGb)),
            (Perf(cpu: 5, io: 0.5), Data(0)),
        };

        foreach (var (perf, data) in profiles)
        {
            var result = TierRecommender.Recommend(perf, data);
            Assert.Contains(result.ServiceTier, validTiers);
            Assert.False(string.IsNullOrWhiteSpace(result.ComputeSize), "ComputeSize should never be empty");
        }
    }

    // ── Pricing integration (RecommendAsync) ────────────────────────

    [Fact]
    public async Task RecommendAsync_WithNullPricingService_UsesHardcodedCost()
    {
        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 3, io: 1), Data(1 * OneGb), 100, null, pricingService: null);

        Assert.Equal(50m, result.EstimatedMonthlyCostUsd);
        Assert.Null(result.RecommendedRegion);
        Assert.Empty(result.RegionalPricing);
    }

    [Fact]
    public async Task RecommendAsync_WithPricing_UsesCheapestRegion()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DatabasePlatform>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
                new() { ArmRegionName = "westus", DisplayName = "West US", EstimatedMonthlyCostUsd = 60m },
                new() { ArmRegionName = "westeurope", DisplayName = "West Europe", EstimatedMonthlyCostUsd = 120m },
            });

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 3, io: 1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.Equal(60m, result.EstimatedMonthlyCostUsd);
        Assert.Equal("westus", result.RecommendedRegion);
    }

    [Fact]
    public async Task RecommendAsync_WithPricing_PopulatesRegionalPricing()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DatabasePlatform>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
                new() { ArmRegionName = "westus", DisplayName = "West US", EstimatedMonthlyCostUsd = 60m },
            });

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 3, io: 1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.NotNull(result.RegionalPricing);
        Assert.Equal(2, result.RegionalPricing.Count);
    }

    [Fact]
    public async Task RecommendAsync_WithEmptyPricing_KeepsHardcodedCost()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DatabasePlatform>()))
            .ReturnsAsync(new List<RegionPricing>());

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 3, io: 1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.Equal(50m, result.EstimatedMonthlyCostUsd);
        Assert.Null(result.RecommendedRegion);
    }

    [Fact]
    public async Task RecommendAsync_CallsPricingWithCorrectArgs()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DatabasePlatform>()))
            .ReturnsAsync(new List<RegionPricing>());

        await TierRecommender.RecommendAsync(
            Perf(cpu: 3, io: 1), Data(1 * OneGb), 100, null, mock.Object);

        // Burstable B2ms with 2 GB storage (1 GB * 1.2 = 1.2 → rounds to 2)
        mock.Verify(s => s.GetPricingForTierAsync(
            "Azure Database for PostgreSQL - Flexible Server", "B_Standard_B2ms", 2, It.IsAny<DatabasePlatform>()), Times.Once);
    }

    // ── Flexible Server preferred when no blockers ──────────────────

    [Fact]
    public void NoIssues_PrefersFlexibleServer()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 50, io: 50), Data(50 * OneGb));

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
    }

    [Fact]
    public void SupportedIssuesOnly_PrefersFlexibleServer()
    {
        // Issues that are Supported on both targets should not trigger VM fallback
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "PostGIS", ObjectName = "ext1", Description = "PostGIS extension" },
            new() { IssueType = "pg_cron", ObjectName = "ext2", Description = "pg_cron extension" },
        };

        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb), 100, issues);

        Assert.Equal("Azure Database for PostgreSQL - Flexible Server", result.ServiceTier);
    }

    // ── VM pricing is cheaper than SQL Server VM (Linux) ────────────

    [Fact]
    public void VmFallback_LinuxPricingIsCheaperThanSqlServerVm()
    {
        var issues = new List<CompatibilityIssue>
        {
            new() { IssueType = "Superuser Access", ObjectName = "role1", Description = "Needs superuser" },
            new() { IssueType = "Custom C Extensions", ObjectName = "ext1", Description = "Custom C extension" },
        };

        var pgResult = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb), 70, issues);

        // PG VM costs: 200/350/600 vs SQL VM costs: 300/500/800
        Assert.Equal(200m, pgResult.EstimatedMonthlyCostUsd);
    }
}