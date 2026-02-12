using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public static class TierRecommender
{
    private const double StorageHeadroomFactor = 1.2;
    private const long OneGb = 1_073_741_824L;
    private const long OneTb = OneGb * 1024;

    private static readonly string[] TargetServices =
    [
        "Azure SQL Database",
        "Azure SQL Database Hyperscale",
        "Azure SQL Managed Instance",
        "SQL Server on Azure VM"
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
        var totalSizeBytes = data.TotalSizeBytes;
        var cpu = perf.AvgCpuPercent;
        var io = perf.AvgIoMbPerSecond;
        var issues = compatibilityIssues ?? [];

        // Rank target services by number of Unsupported issues (fewest blockers wins)
        var bestService = PickBestService(issues, totalSizeBytes);

        // Build recommendation for the best service
        return bestService switch
        {
            "SQL Server on Azure VM" => BuildVm(cpu, storageGb, issues),
            "Azure SQL Managed Instance" => BuildManagedInstance(cpu, io, storageGb, issues),
            "Azure SQL Database Hyperscale" => BuildHyperscale(cpu, storageGb, issues),
            _ => BuildSqlDatabase(cpu, io, totalSizeBytes, storageGb, issues)
        };
    }

    /// <summary>
    /// Pick the target service with the fewest Unsupported issues.
    /// Preference order when tied: SQL Database > Hyperscale > Managed Instance > VM
    /// (PaaS-first: recommend the most managed option that works).
    /// </summary>
    private static string PickBestService(List<CompatibilityIssue> issues, long totalSizeBytes)
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

        var best = ranked[0];

        // If best is SQL Database but DB > 1TB, prefer Hyperscale (same blocker count for most issues)
        if (best.Service == "Azure SQL Database" && totalSizeBytes > OneTb)
        {
            var hyperscale = ranked.First(r => r.Service == "Azure SQL Database Hyperscale");
            if (hyperscale.Unsupported <= best.Unsupported)
                return hyperscale.Service;
        }

        return best.Service;
    }

    private static TierRecommendation BuildVm(double cpu, int storageGb, List<CompatibilityIssue> issues)
    {
        int vCores;
        string vmSize;
        decimal cost;

        if (cpu > 60)
        {
            vCores = 8; vmSize = "Standard_D8s_v5"; cost = 800m;
        }
        else if (cpu > 30)
        {
            vCores = 4; vmSize = "Standard_D4s_v5"; cost = 500m;
        }
        else
        {
            vCores = 2; vmSize = "Standard_D2s_v5"; cost = 300m;
        }

        var unsupportedCount = CountUnsupported(issues, "SQL Server on Azure VM");
        return Build("SQL Server on Azure VM", vmSize, dtus: null, vCores, storageGb, cost,
            $"SQL Server on Azure VM is recommended with {unsupportedCount} unsupported issues (fewest blockers). Full SQL Server feature parity with {vCores} vCPUs.");
    }

    private static TierRecommendation BuildManagedInstance(double cpu, double io, int storageGb, List<CompatibilityIssue> issues)
    {
        int vCores;
        string computeSize;
        decimal cost;

        if (cpu > 60 || io > 100)
        {
            vCores = 8; computeSize = "GP_Gen5_8"; cost = 1200m;
        }
        else if (cpu > 30 || io > 50)
        {
            vCores = 4; computeSize = "GP_Gen5_4"; cost = 640m;
        }
        else
        {
            vCores = 4; computeSize = "GP_Gen5_4"; cost = 640m;
        }

        var unsupportedCount = CountUnsupported(issues, "Azure SQL Managed Instance");
        return Build("Azure SQL Managed Instance", computeSize, dtus: null, vCores, storageGb, cost,
            $"Azure SQL Managed Instance is recommended with {unsupportedCount} unsupported issues. Supports SQL Agent, cross-DB queries, Service Broker, and CLR with {vCores} vCores.");
    }

    private static TierRecommendation BuildHyperscale(double cpu, int storageGb, List<CompatibilityIssue> issues)
    {
        int vCores;
        string computeSize;
        decimal cost;

        if (cpu > 60)
        {
            vCores = 8; computeSize = "HS_Gen5_8"; cost = 1300m;
        }
        else if (cpu > 30)
        {
            vCores = 4; computeSize = "HS_Gen5_4"; cost = 700m;
        }
        else
        {
            vCores = 2; computeSize = "HS_Gen5_2"; cost = 650m;
        }

        var unsupportedCount = CountUnsupported(issues, "Azure SQL Database Hyperscale");
        return Build("Azure SQL Database Hyperscale", computeSize, dtus: null, vCores, storageGb, cost,
            $"Azure SQL Database Hyperscale is recommended with {unsupportedCount} unsupported issues. Supports rapid scale and very large databases with {vCores} vCores.");
    }

    private static TierRecommendation BuildSqlDatabase(double cpu, double io, long totalSizeBytes, int storageGb, List<CompatibilityIssue> issues)
    {
        var unsupportedCount = CountUnsupported(issues, "Azure SQL Database");
        var suffix = $" ({unsupportedCount} unsupported issues)";

        // Business Critical: high CPU (>70%) or high IO (>100 MB/s)
        if (cpu > 70 || io > 100)
            return BuildBusinessCritical(cpu, io, storageGb, suffix);

        // vCore General Purpose: DB > 250GB or moderate-high CPU (>40%)
        if (totalSizeBytes > 250L * OneGb || cpu > 40)
            return BuildGeneralPurpose(cpu, storageGb, suffix);

        // DTU-based tiers for smaller workloads
        return BuildDtu(cpu, io, totalSizeBytes, storageGb, suffix);
    }

    private static int CountUnsupported(List<CompatibilityIssue> issues, string targetService)
    {
        return issues.Count(i => CompatibilityMatrix.GetSeverity(i.IssueType, targetService) == CompatibilitySeverity.Unsupported);
    }

    private static TierRecommendation BuildBusinessCritical(double cpu, double io, int storageGb, string suffix)
    {
        int vCores;
        decimal cost;

        if (cpu > 80 || io > 200)
        {
            vCores = 8;
            cost = 1800m;
        }
        else if (cpu > 60 || io > 150)
        {
            vCores = 4;
            cost = 900m;
        }
        else
        {
            vCores = 2;
            cost = 450m;
        }

        return Build("Azure SQL Database", $"BC_Gen5_{vCores}", dtus: null, vCores, storageGb, cost,
            $"High resource demands (CPU {cpu:F1}%, IO {io:F1} MB/s) require Business Critical tier with {vCores} vCores for low-latency IO.{suffix}");
    }

    private static TierRecommendation BuildGeneralPurpose(double cpu, int storageGb, string suffix)
    {
        int vCores;
        decimal cost;

        if (cpu > 60)
        {
            vCores = 8;
            cost = 800m;
        }
        else if (cpu > 30)
        {
            vCores = 4;
            cost = 400m;
        }
        else
        {
            vCores = 2;
            cost = 200m;
        }

        return Build("Azure SQL Database", $"GP_Gen5_{vCores}", dtus: null, vCores, storageGb, cost,
            $"Workload characteristics (CPU {cpu:F1}%) suit General Purpose tier with {vCores} vCores.{suffix}");
    }

    private static TierRecommendation BuildDtu(double cpu, double io, long totalSizeBytes, int storageGb, string suffix)
    {
        if (totalSizeBytes < 2L * OneGb && cpu < 10 && io < 1)
            return Build("Azure SQL Database", "Basic", dtus: 5, vCores: null, Math.Min(storageGb, 2), 5m,
                $"Small database with low CPU and IO — Basic tier is the most cost-effective option.{suffix}");

        if (cpu < 15 && io < 5)
            return Build("Azure SQL Database", "S0", dtus: 10, vCores: null, storageGb, 15m,
                $"Light workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S0.{suffix}");

        if (cpu < 25 && io < 15)
            return Build("Azure SQL Database", "S1", dtus: 20, vCores: null, storageGb, 30m,
                $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S1.{suffix}");

        if (cpu < 35 && io < 30)
            return Build("Azure SQL Database", "S2", dtus: 50, vCores: null, storageGb, 75m,
                $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S2.{suffix}");

        if (cpu <= 40 || io <= 50)
            return Build("Azure SQL Database", "S3", dtus: 100, vCores: null, storageGb, 150m,
                $"Higher workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S3.{suffix}");

        if (cpu <= 55 || io <= 75)
            return Build("Azure SQL Database", "P1", dtus: 125, vCores: null, storageGb, 465m,
                $"Demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P1.{suffix}");

        if (cpu <= 65 || io <= 90)
            return Build("Azure SQL Database", "P2", dtus: 250, vCores: null, storageGb, 930m,
                $"Demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P2.{suffix}");

        return Build("Azure SQL Database", "P4", dtus: 500, vCores: null, storageGb, 1860m,
            $"Very demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P4.{suffix}");
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
