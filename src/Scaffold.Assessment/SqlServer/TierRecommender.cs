using Scaffold.Core.Models;

namespace Scaffold.Assessment.SqlServer;

public static class TierRecommender
{
    private const double StorageHeadroomFactor = 1.2;
    private const long OneGb = 1_073_741_824L;
    private const long OneTb = OneGb * 1024;

    public static TierRecommendation Recommend(PerformanceProfile perf, DataProfile data)
    {
        var storageGb = Math.Max(1, (int)Math.Ceiling(data.TotalSizeBytes * StorageHeadroomFactor / OneGb));
        var totalSizeBytes = data.TotalSizeBytes;
        var cpu = perf.AvgCpuPercent;
        var io = perf.AvgIoMbPerSecond;

        // Hyperscale: DB > 1TB
        if (totalSizeBytes > OneTb)
            return Build("Hyperscale", "HS_Gen5_2", dtus: null, vCores: 2, storageGb, 650m,
                "Database exceeds 1 TB; Hyperscale provides rapid scale and supports very large databases.");

        // Business Critical: high CPU (>70%) or high IO (>100 MB/s)
        if (cpu > 70 || io > 100)
            return BuildBusinessCritical(cpu, io, storageGb);

        // vCore General Purpose: DB > 250GB or moderate-high CPU (>40%)
        if (totalSizeBytes > 250L * OneGb || cpu > 40)
            return BuildGeneralPurpose(cpu, storageGb);

        // DTU-based tiers for smaller workloads
        return BuildDtu(cpu, io, totalSizeBytes, storageGb);
    }

    private static TierRecommendation BuildBusinessCritical(double cpu, double io, int storageGb)
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

        return Build("BusinessCritical", $"BC_Gen5_{vCores}", dtus: null, vCores, storageGb, cost,
            $"High resource demands (CPU {cpu:F1}%, IO {io:F1} MB/s) require Business Critical tier with {vCores} vCores for low-latency IO.");
    }

    private static TierRecommendation BuildGeneralPurpose(double cpu, int storageGb)
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

        return Build("GeneralPurpose", $"GP_Gen5_{vCores}", dtus: null, vCores, storageGb, cost,
            $"Workload characteristics (CPU {cpu:F1}%) suit General Purpose tier with {vCores} vCores.");
    }

    private static TierRecommendation BuildDtu(double cpu, double io, long totalSizeBytes, int storageGb)
    {
        // Basic: DB < 2GB, low CPU (<10%), low IO (<1 MB/s)
        if (totalSizeBytes < 2L * OneGb && cpu < 10 && io < 1)
            return Build("Basic", "Basic", dtus: 5, vCores: null, Math.Min(storageGb, 2), 5m,
                "Small database with low CPU and IO — Basic tier is the most cost-effective option.");

        // Standard tiers
        if (cpu < 15 && io < 5)
            return Build("Standard", "S0", dtus: 10, vCores: null, storageGb, 15m,
                $"Light workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S0.");

        if (cpu < 25 && io < 15)
            return Build("Standard", "S1", dtus: 20, vCores: null, storageGb, 30m,
                $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S1.");

        if (cpu < 35 && io < 30)
            return Build("Standard", "S2", dtus: 50, vCores: null, storageGb, 75m,
                $"Moderate workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S2.");

        if (cpu <= 40 || io <= 50)
            return Build("Standard", "S3", dtus: 100, vCores: null, storageGb, 150m,
                $"Higher workload (CPU {cpu:F1}%, IO {io:F1} MB/s) fits Standard S3.");

        // Premium fallback for DTU range
        if (cpu <= 55 || io <= 75)
            return Build("Premium", "P1", dtus: 125, vCores: null, storageGb, 465m,
                $"Demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P1.");

        if (cpu <= 65 || io <= 90)
            return Build("Premium", "P2", dtus: 250, vCores: null, storageGb, 930m,
                $"Demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P2.");

        return Build("Premium", "P4", dtus: 500, vCores: null, storageGb, 1860m,
            $"Very demanding workload (CPU {cpu:F1}%, IO {io:F1} MB/s) requires Premium P4.");
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
