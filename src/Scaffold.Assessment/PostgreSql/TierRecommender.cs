using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.PostgreSql;

public static class TierRecommender
{
    private const double StorageHeadroomFactor = 1.2;
    private const long OneGb = 1_073_741_824L;

    private static readonly string[] TargetServices =
    [
        "Azure Database for PostgreSQL - Flexible Server",
        "PostgreSQL on Azure VM"
    ];

    public static async Task<TierRecommendation> RecommendAsync(
        PerformanceProfile perf,
        DataProfile data,
        double compatibilityScore,
        List<CompatibilityIssue>? compatibilityIssues,
        IAzurePricingService? pricingService)
    {
        var rec = Recommend(perf, data, compatibilityScore, compatibilityIssues);

        if (pricingService is not null)
        {
            var pricing = await pricingService.GetPricingForTierAsync(
                rec.ServiceTier, rec.ComputeSize, rec.StorageGb);

            if (pricing.Count > 0)
            {
                rec.RegionalPricing = pricing;
                var cheapest = pricing.OrderBy(p => p.EstimatedMonthlyCostUsd).First();
                rec.RecommendedRegion = cheapest.ArmRegionName;
                rec.EstimatedMonthlyCostUsd = cheapest.EstimatedMonthlyCostUsd;
            }
        }

        return rec;
    }

    public static TierRecommendation Recommend(
        PerformanceProfile perf,
        DataProfile data,
        double compatibilityScore = 100,
        List<CompatibilityIssue>? compatibilityIssues = null)
    {
        var storageGb = Math.Max(1, (int)Math.Ceiling(data.TotalSizeBytes * StorageHeadroomFactor / OneGb));
        var cpu = perf.AvgCpuPercent;
        var io = perf.AvgIoMbPerSecond;
        var issues = compatibilityIssues ?? [];

        // Rank target services by number of Unsupported issues (fewest blockers wins)
        var bestService = PickBestService(issues);

        // Build recommendation for the best service
        return bestService switch
        {
            "PostgreSQL on Azure VM" => BuildVm(cpu, storageGb, issues),
            _ => BuildFlexibleServer(cpu, io, storageGb, issues)
        };
    }

    /// <summary>
    /// Pick the target service with the fewest Unsupported issues.
    /// Preference order when tied: Flexible Server > VM
    /// (PaaS-first: recommend the most managed option that works).
    /// </summary>
    private static string PickBestService(List<CompatibilityIssue> issues)
    {
        var ranked = TargetServices
            .Select(service =>
            {
                var unsupported = 0;
                var partial = 0;
                foreach (var issue in issues)
                {
                    var severity = CompatibilityMatrix.GetSeverity(issue.IssueType, service);
                    if (severity == CompatibilitySeverity.Unsupported) unsupported++;
                    else if (severity == CompatibilitySeverity.Partial) partial++;
                }
                return new { Service = service, Unsupported = unsupported, Partial = partial };
            })
            .OrderBy(r => r.Unsupported)
            .ThenBy(r => r.Partial)
            .ToList();

        return ranked[0].Service;
    }

    private static TierRecommendation BuildFlexibleServer(double cpu, double io, int storageGb, List<CompatibilityIssue> issues)
    {
        var unsupportedCount = CountUnsupported(issues, "Azure Database for PostgreSQL - Flexible Server");

        // Burstable: low CPU and low IO
        if (cpu < 15 && io < 10)
        {
            return Build("Azure Database for PostgreSQL - Flexible Server", "B_Standard_B2ms",
                dtus: null, vCores: 2, storageGb, 50m,
                $"Low resource usage (CPU {cpu:F1}%, IO {io:F1} MB/s) suits Burstable tier with 2 vCores. {unsupportedCount} unsupported issues.");
        }

        // General Purpose: moderate CPU and IO
        if (cpu < 40 && io < 50)
        {
            if (cpu < 25 && io < 25)
            {
                return Build("Azure Database for PostgreSQL - Flexible Server", "GP_Standard_D2s_v3",
                    dtus: null, vCores: 2, storageGb, 150m,
                    $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) suits General Purpose D2s with 2 vCores. {unsupportedCount} unsupported issues.");
            }

            return Build("Azure Database for PostgreSQL - Flexible Server", "GP_Standard_D4s_v3",
                dtus: null, vCores: 4, storageGb, 300m,
                $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) suits General Purpose D4s with 4 vCores. {unsupportedCount} unsupported issues.");
        }

        // General Purpose: higher CPU
        if (cpu < 70)
        {
            if (cpu < 55)
            {
                return Build("Azure Database for PostgreSQL - Flexible Server", "GP_Standard_D8s_v3",
                    dtus: null, vCores: 8, storageGb, 600m,
                    $"Higher workload (CPU {cpu:F1}%, IO {io:F1} MB/s) suits General Purpose D8s with 8 vCores. {unsupportedCount} unsupported issues.");
            }

            return Build("Azure Database for PostgreSQL - Flexible Server", "GP_Standard_D16s_v3",
                dtus: null, vCores: 16, storageGb, 1200m,
                $"High workload (CPU {cpu:F1}%, IO {io:F1} MB/s) suits General Purpose D16s with 16 vCores. {unsupportedCount} unsupported issues.");
        }

        // Memory Optimized: high CPU or high IO
        if (cpu < 85 && io < 150)
        {
            return Build("Azure Database for PostgreSQL - Flexible Server", "MO_Standard_E4s_v3",
                dtus: null, vCores: 4, storageGb, 500m,
                $"High resource demands (CPU {cpu:F1}%, IO {io:F1} MB/s) require Memory Optimized E4s with 4 vCores. {unsupportedCount} unsupported issues.");
        }

        return Build("Azure Database for PostgreSQL - Flexible Server", "MO_Standard_E8s_v3",
            dtus: null, vCores: 8, storageGb, 900m,
            $"Very high resource demands (CPU {cpu:F1}%, IO {io:F1} MB/s) require Memory Optimized E8s with 8 vCores. {unsupportedCount} unsupported issues.");
    }

    private static TierRecommendation BuildVm(double cpu, int storageGb, List<CompatibilityIssue> issues)
    {
        int vCores;
        string vmSize;
        decimal cost;

        if (cpu > 60)
        {
            vCores = 8; vmSize = "Standard_D8s_v5"; cost = 600m;
        }
        else if (cpu > 30)
        {
            vCores = 4; vmSize = "Standard_D4s_v5"; cost = 350m;
        }
        else
        {
            vCores = 2; vmSize = "Standard_D2s_v5"; cost = 200m;
        }

        var unsupportedCount = CountUnsupported(issues, "PostgreSQL on Azure VM");
        return Build("PostgreSQL on Azure VM", vmSize, dtus: null, vCores, storageGb, cost,
            $"PostgreSQL on Azure VM is recommended with {unsupportedCount} unsupported issues (fewest blockers). Full PostgreSQL feature parity with {vCores} vCPUs on Linux.");
    }

    private static int CountUnsupported(List<CompatibilityIssue> issues, string targetService)
    {
        return issues.Count(i => CompatibilityMatrix.GetSeverity(i.IssueType, targetService) == CompatibilitySeverity.Unsupported);
    }

    private static TierRecommendation Build(
        string tier, string computeSize, int? dtus, int? vCores, int storageGb, decimal cost, string reasoning)
    {
        return new TierRecommendation
        {
            ServiceTier = tier,
            ComputeSize = computeSize,
            Dtus = dtus,
            VCores = vCores,
            StorageGb = storageGb,
            EstimatedMonthlyCostUsd = cost,
            Reasoning = reasoning
        };
    }
}