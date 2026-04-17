using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment;

/// <summary>
/// Recommends a migration strategy (Cutover vs ContinuousSync) based on assessment data.
/// Heuristics consider database size, table count, activity level, and target platform.
/// </summary>
public static class MigrationStrategyRecommender
{
    // Size thresholds in bytes
    private const long SmallDbThresholdBytes = 10L * 1024 * 1024 * 1024;   // 10 GB
    private const long LargeDbThresholdBytes = 100L * 1024 * 1024 * 1024;  // 100 GB

    // Transfer rate estimate: ~100 MB/min for downtime calculation
    private const double TransferRateMbPerMinute = 100.0;

    // Activity thresholds (avg IO MB/s) for medium databases
    private const double HighActivityThresholdMbPerSec = 50.0;

    // ContinuousSync final sync window estimate (minutes)
    private const double ContinuousSyncFinalWindowMinutes = 15.0;

    /// <summary>
    /// Analyzes assessment data and returns a strategy recommendation.
    /// </summary>
    /// <param name="dataProfile">Database data profile (size, row counts, tables).</param>
    /// <param name="schema">Schema inventory (table count, triggers, etc.).</param>
    /// <param name="performance">Performance profile (IO, CPU).</param>
    /// <param name="targetPlatform">The target database platform.</param>
    /// <returns>A recommendation including strategy, reasoning, and estimated downtimes.</returns>
    public static StrategyRecommendation Recommend(
        DataProfile dataProfile,
        SchemaInventory schema,
        PerformanceProfile performance,
        DatabasePlatform targetPlatform)
    {
        var recommendation = new StrategyRecommendation();
        var considerations = new List<string>();

        var totalSizeBytes = dataProfile.TotalSizeBytes;
        var totalSizeMb = totalSizeBytes / (1024.0 * 1024.0);
        var totalSizeGb = totalSizeBytes / (1024.0 * 1024.0 * 1024.0);

        // Estimate cutover downtime: data transfer time + overhead
        var transferMinutes = totalSizeMb / TransferRateMbPerMinute;
        // Add schema deployment overhead: ~1 min per 50 tables, minimum 1 minute
        var schemaOverheadMinutes = Math.Max(1.0, schema.TableCount / 50.0);
        var cutoverDowntimeMinutes = transferMinutes + schemaOverheadMinutes;
        recommendation.EstimatedDowntimeCutover = TimeSpan.FromMinutes(cutoverDowntimeMinutes);

        // Rule 1: Cross-platform migrations → always Cutover
        // ContinuousSync depends on Change Tracking which is SQL Server-specific
        if (targetPlatform != DatabasePlatform.SqlServer)
        {
            recommendation.RecommendedStrategy = MigrationStrategy.Cutover;
            recommendation.Reasoning =
                $"Cross-platform migration to {targetPlatform} requires Cutover strategy. " +
                "ContinuousSync depends on SQL Server Change Tracking which is not available cross-platform.";
            recommendation.EstimatedDowntimeContinuousSync = null;
            considerations.Add("ContinuousSync is not available for cross-platform migrations.");

            AddCommonConsiderations(considerations, schema, dataProfile, totalSizeGb);
            recommendation.Considerations = considerations;
            return recommendation;
        }

        // Same-platform (SQL Server → SQL Server) — estimate ContinuousSync downtime
        recommendation.EstimatedDowntimeContinuousSync = TimeSpan.FromMinutes(ContinuousSyncFinalWindowMinutes);

        // Rule 2: Small databases (<10 GB, <100 tables) → Cutover
        if (totalSizeBytes < SmallDbThresholdBytes && schema.TableCount < 100)
        {
            recommendation.RecommendedStrategy = MigrationStrategy.Cutover;
            recommendation.Reasoning =
                $"Database is small ({totalSizeGb:F1} GB, {schema.TableCount} tables). " +
                $"Cutover is simpler and estimated downtime ({cutoverDowntimeMinutes:F0} min) is acceptable.";

            AddCommonConsiderations(considerations, schema, dataProfile, totalSizeGb);
            recommendation.Considerations = considerations;
            return recommendation;
        }

        // Rule 3: Large databases (>100 GB) → ContinuousSync
        if (totalSizeBytes >= LargeDbThresholdBytes)
        {
            recommendation.RecommendedStrategy = MigrationStrategy.ContinuousSync;
            recommendation.Reasoning =
                $"Database is large ({totalSizeGb:F1} GB). ContinuousSync reduces downtime from " +
                $"~{cutoverDowntimeMinutes:F0} min (Cutover) to ~{ContinuousSyncFinalWindowMinutes:F0} min (final sync window).";

            AddCommonConsiderations(considerations, schema, dataProfile, totalSizeGb);
            recommendation.Considerations = considerations;
            return recommendation;
        }

        // Rule 4: Medium databases (10-100 GB) — depends on activity level
        if (performance.AvgIoMbPerSecond >= HighActivityThresholdMbPerSec)
        {
            recommendation.RecommendedStrategy = MigrationStrategy.ContinuousSync;
            recommendation.Reasoning =
                $"Medium-sized database ({totalSizeGb:F1} GB) with high activity " +
                $"(avg {performance.AvgIoMbPerSecond:F1} MB/s IO). " +
                "ContinuousSync minimizes downtime for active databases.";
        }
        else
        {
            recommendation.RecommendedStrategy = MigrationStrategy.Cutover;
            recommendation.Reasoning =
                $"Medium-sized database ({totalSizeGb:F1} GB) with low activity " +
                $"(avg {performance.AvgIoMbPerSecond:F1} MB/s IO). " +
                $"Cutover is simpler with estimated downtime of ~{cutoverDowntimeMinutes:F0} min.";
        }

        AddCommonConsiderations(considerations, schema, dataProfile, totalSizeGb);
        recommendation.Considerations = considerations;
        return recommendation;
    }

    private static void AddCommonConsiderations(
        List<string> considerations,
        SchemaInventory schema,
        DataProfile dataProfile,
        double totalSizeGb)
    {
        if (schema.TriggerCount > 0)
        {
            considerations.Add(
                $"Database has {schema.TriggerCount} trigger(s). " +
                "Triggers may add complexity to ContinuousSync and should be tested thoroughly.");
        }

        // Count FK relationships from schema objects
        var fkCount = schema.Objects.Count(o =>
            o.ObjectType.Equals("FOREIGN_KEY_CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
            o.SubType?.Equals("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) == true);
        if (fkCount > 20)
        {
            considerations.Add(
                $"Database has {fkCount} foreign key constraints. " +
                "Bulk copy operations must respect FK ordering, which may increase migration time.");
        }

        if (dataProfile.Tables.Any(t => t.RowCount > 10_000_000))
        {
            var largeTables = dataProfile.Tables.Where(t => t.RowCount > 10_000_000).ToList();
            considerations.Add(
                $"{largeTables.Count} table(s) have more than 10 million rows. " +
                "Consider monitoring progress closely during data migration.");
        }

        if (totalSizeGb > 500)
        {
            considerations.Add(
                "Database exceeds 500 GB. Ensure sufficient disk space and network bandwidth on the target.");
        }
    }
}