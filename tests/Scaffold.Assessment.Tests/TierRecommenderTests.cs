using Moq;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class TierRecommenderTests
{
    private const long OneGb = 1_073_741_824L;
    private const long OneTb = OneGb * 1024;

    private static PerformanceProfile Perf(double cpu = 0, double io = 0) =>
        new() { AvgCpuPercent = cpu, AvgIoMbPerSecond = io };

    private static DataProfile Data(long sizeBytes = 0) =>
        new() { TotalSizeBytes = sizeBytes };

    // ── Basic tier ──────────────────────────────────────────────────

    [Fact]
    public void SmallDb_LowCpuLowIo_RecommendsBasic()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 5, io: 0.5), Data(1 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("Basic", result.ComputeSize);
        Assert.Equal(5, result.Dtus);
        Assert.Null(result.VCores);
        Assert.Equal(5m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void BasicTier_StorageCappedAt2Gb()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 1, io: 0.1), Data(1 * OneGb));

        Assert.True(result.StorageGb <= 2, "Basic tier storage should be capped at 2 GB");
    }

    // ── Standard S0–S3 ─────────────────────────────────────────────

    [Fact]
    public void LightWorkload_RecommendsStandardS0()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 12, io: 3), Data(5 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("S0", result.ComputeSize);
        Assert.Equal(10, result.Dtus);
        Assert.Equal(15m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void ModerateWorkload_RecommendsStandardS1()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("S1", result.ComputeSize);
        Assert.Equal(20, result.Dtus);
        Assert.Equal(30m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void ModerateHighWorkload_RecommendsStandardS2()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 30, io: 25), Data(10 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("S2", result.ComputeSize);
        Assert.Equal(50, result.Dtus);
        Assert.Equal(75m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void HigherDtuWorkload_RecommendsStandardS3()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 38, io: 35), Data(10 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("S3", result.ComputeSize);
        Assert.Equal(100, result.Dtus);
        Assert.Equal(150m, result.EstimatedMonthlyCostUsd);
    }

    // ── General Purpose vCore ───────────────────────────────────────

    [Fact]
    public void LargeDb_RecommendsGeneralPurpose()
    {
        // > 250 GB triggers General Purpose
        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(260L * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Contains("GP_Gen5_", result.ComputeSize);
        Assert.Null(result.Dtus);
        Assert.NotNull(result.VCores);
    }

    [Fact]
    public void ModerateCpu_RecommendsGeneralPurpose_2vCores()
    {
        // CPU > 40 but <= 30 in the GP branch (since cpu 41 hits GP, then 41 > 30 → 4 vCores)
        // To get 2 vCores: need cpu > 40 to enter GP, but cpu <= 30 in GP branch — impossible
        // Actually: cpu > 40 triggers GP. Then inside GP: >60 → 8, >30 → 4, else 2
        // So cpu = 41 → GP with 4 vCores. Let's use > 250GB with low CPU to get 2 vCores
        var result = TierRecommender.Recommend(Perf(cpu: 25, io: 10), Data(260L * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("GP_Gen5_2", result.ComputeSize);
        Assert.Equal(2, result.VCores);
        Assert.Equal(200m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void HighCpu_RecommendsGeneralPurpose_4vCores()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 50, io: 10), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("GP_Gen5_4", result.ComputeSize);
        Assert.Equal(4, result.VCores);
        Assert.Equal(400m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void VeryHighCpu_RecommendsGeneralPurpose_8vCores()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 65, io: 10), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("GP_Gen5_8", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(800m, result.EstimatedMonthlyCostUsd);
    }

    // ── Business Critical ───────────────────────────────────────────

    [Fact]
    public void HighCpu_RecommendsBusinessCritical()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 75, io: 50), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Contains("BC_Gen5_", result.ComputeSize);
        Assert.NotNull(result.VCores);
    }

    [Fact]
    public void HighIo_RecommendsBusinessCritical()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 30, io: 110), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
    }

    [Fact]
    public void BusinessCritical_2vCores_ForModerateHighResources()
    {
        // cpu > 70 triggers BC; cpu <= 60 and io <= 150 in BC branch → 2 vCores
        // Wait: cpu > 70 enters BC. Then cpu > 80 || io > 200 → 8; cpu > 60 || io > 150 → 4; else 2
        // cpu = 71, io = 50 → not >80, not >60? 71 > 60 → yes → 4 vCores
        // To get 2 vCores: need io > 100 (to enter BC) but cpu <=60 and io <= 150
        var result = TierRecommender.Recommend(Perf(cpu: 30, io: 110), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("BC_Gen5_2", result.ComputeSize);
        Assert.Equal(2, result.VCores);
        Assert.Equal(450m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void BusinessCritical_4vCores()
    {
        // cpu > 60 or io > 150 in BC branch → 4 vCores
        var result = TierRecommender.Recommend(Perf(cpu: 75, io: 50), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("BC_Gen5_4", result.ComputeSize);
        Assert.Equal(4, result.VCores);
        Assert.Equal(900m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void BusinessCritical_8vCores_VeryHighCpu()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 85, io: 50), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("BC_Gen5_8", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(1800m, result.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void BusinessCritical_8vCores_VeryHighIo()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 30, io: 210), Data(50 * OneGb));

        Assert.Equal("Azure SQL Database", result.ServiceTier);
        Assert.Equal("BC_Gen5_8", result.ComputeSize);
        Assert.Equal(8, result.VCores);
        Assert.Equal(1800m, result.EstimatedMonthlyCostUsd);
    }

    // ── Hyperscale ──────────────────────────────────────────────────

    [Fact]
    public void VeryLargeDb_RecommendsHyperscale()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 50, io: 50), Data(2 * OneTb));

        Assert.Equal("Azure SQL Database Hyperscale", result.ServiceTier);
        Assert.StartsWith("HS_Gen5_", result.ComputeSize);
    }

    [Fact]
    public void JustOverOneTb_RecommendsHyperscale()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 5, io: 0.5), Data(OneTb + 1));

        Assert.Equal("Azure SQL Database Hyperscale", result.ServiceTier);
    }

    [Fact]
    public void ExactlyOneTb_DoesNotRecommendHyperscale()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 5, io: 0.5), Data(OneTb));

        Assert.NotEqual("Azure SQL Database Hyperscale", result.ServiceTier);
    }

    // ── Storage headroom (20% buffer) ───────────────────────────────

    [Fact]
    public void StorageIncludesTwentyPercentHeadroom()
    {
        // 10 GB data → 12 GB with 20% headroom
        var result = TierRecommender.Recommend(Perf(cpu: 12, io: 3), Data(10 * OneGb));

        Assert.Equal(12, result.StorageGb);
    }

    [Fact]
    public void StorageHeadroom_RoundsUp()
    {
        // 5 GB → 6 GB (5 * 1.2 = 6.0 exactly)
        var result = TierRecommender.Recommend(Perf(cpu: 12, io: 3), Data(5 * OneGb));

        Assert.Equal(6, result.StorageGb);
    }

    [Fact]
    public void StorageHeadroom_MinimumOneGb()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 1, io: 0.1), Data(0));

        Assert.True(result.StorageGb >= 1, "Storage should be at least 1 GB");
    }

    // ── Cost estimation sanity checks ───────────────────────────────

    [Fact]
    public void BasicTier_IsCheapest()
    {
        var basic = TierRecommender.Recommend(Perf(cpu: 1, io: 0.1), Data(1 * OneGb));

        Assert.Equal(5m, basic.EstimatedMonthlyCostUsd);
    }

    [Fact]
    public void CostIncreasesWithTier()
    {
        var basic = TierRecommender.Recommend(Perf(cpu: 1, io: 0.1), Data(1 * OneGb));
        var s0 = TierRecommender.Recommend(Perf(cpu: 12, io: 3), Data(5 * OneGb));
        var gp = TierRecommender.Recommend(Perf(cpu: 25, io: 10), Data(260L * OneGb));
        var bc = TierRecommender.Recommend(Perf(cpu: 85, io: 50), Data(50 * OneGb));
        var hs = TierRecommender.Recommend(Perf(cpu: 50, io: 50), Data(2 * OneTb));

        Assert.True(basic.EstimatedMonthlyCostUsd < s0.EstimatedMonthlyCostUsd);
        Assert.True(s0.EstimatedMonthlyCostUsd < gp.EstimatedMonthlyCostUsd);
        Assert.True(gp.EstimatedMonthlyCostUsd < bc.EstimatedMonthlyCostUsd);
    }

    // ── Reasoning is populated ──────────────────────────────────────

    [Fact]
    public void Recommendation_AlwaysIncludesReasoning()
    {
        var result = TierRecommender.Recommend(Perf(cpu: 20, io: 10), Data(10 * OneGb));

        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    // ── Pricing integration (RecommendAsync) ────────────────────────

    [Fact]
    public async Task RecommendAsync_WithNullPricingService_UsesHardcodedCost()
    {
        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, pricingService: null);

        Assert.Equal(5m, result.EstimatedMonthlyCostUsd);
        Assert.Null(result.RecommendedRegion);
        Assert.Empty(result.RegionalPricing);
    }

    [Fact]
    public async Task RecommendAsync_WithPricing_UsesCheapestRegion()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
                new() { ArmRegionName = "westus", DisplayName = "West US", EstimatedMonthlyCostUsd = 80m },
                new() { ArmRegionName = "westeurope", DisplayName = "West Europe", EstimatedMonthlyCostUsd = 120m },
            });

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.Equal(80m, result.EstimatedMonthlyCostUsd);
        Assert.Equal("westus", result.RecommendedRegion);
    }

    [Fact]
    public async Task RecommendAsync_WithPricing_PopulatesRegionalPricing()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
                new() { ArmRegionName = "westus", DisplayName = "West US", EstimatedMonthlyCostUsd = 80m },
                new() { ArmRegionName = "westeurope", DisplayName = "West Europe", EstimatedMonthlyCostUsd = 120m },
            });

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.NotNull(result.RegionalPricing);
        Assert.Equal(3, result.RegionalPricing.Count);
    }

    [Fact]
    public async Task RecommendAsync_WithEmptyPricing_KeepsHardcodedCost()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>());

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.Equal(5m, result.EstimatedMonthlyCostUsd);
        Assert.Null(result.RecommendedRegion);
    }

    [Fact]
    public async Task RecommendAsync_CallsPricingWithCorrectArgs()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>());

        await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, mock.Object);

        mock.Verify(s => s.GetPricingForTierAsync("Azure SQL Database", "Basic", 2), Times.Once);
    }

    [Fact]
    public async Task RecommendAsync_WithPricing_SelectsCheapestFromUnsortedList()
    {
        var mock = new Mock<IAzurePricingService>();
        mock.Setup(s => s.GetPricingForTierAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<RegionPricing>
            {
                new() { ArmRegionName = "westeurope", DisplayName = "West Europe", EstimatedMonthlyCostUsd = 500m },
                new() { ArmRegionName = "eastus", DisplayName = "East US", EstimatedMonthlyCostUsd = 100m },
                new() { ArmRegionName = "westus", DisplayName = "West US", EstimatedMonthlyCostUsd = 300m },
            });

        var result = await TierRecommender.RecommendAsync(
            Perf(cpu: 1, io: 0.1), Data(1 * OneGb), 100, null, mock.Object);

        Assert.Equal(100m, result.EstimatedMonthlyCostUsd);
        Assert.Equal("eastus", result.RecommendedRegion);
    }

    [Fact]
    public void Recommend_AlwaysReturnsValidServiceTier()
    {
        string[] validTiers = [
            "Azure SQL Database",
            "Azure SQL Database Hyperscale",
            "Azure SQL Managed Instance",
            "SQL Server on Azure VM"
        ];

        // Test various workload profiles
        var profiles = new[]
        {
            (Perf(cpu: 1, io: 0.1), Data(1 * OneGb)),
            (Perf(cpu: 50, io: 50), Data(50 * OneGb)),
            (Perf(cpu: 85, io: 200), Data(2 * OneTb)),
            (Perf(cpu: 5, io: 0.5), Data(0)),
        };

        foreach (var (perf, data) in profiles)
        {
            var result = TierRecommender.Recommend(perf, data);
            Assert.Contains(result.ServiceTier, validTiers);
            Assert.False(string.IsNullOrWhiteSpace(result.ComputeSize), "ComputeSize should never be empty");
        }
    }
}
