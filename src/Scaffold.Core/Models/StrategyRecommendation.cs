using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

/// <summary>
/// Recommendation for which migration strategy to use, based on assessment data.
/// </summary>
public class StrategyRecommendation
{
    /// <summary>
    /// The recommended migration strategy (Cutover or ContinuousSync).
    /// </summary>
    public MigrationStrategy RecommendedStrategy { get; set; }

    /// <summary>
    /// Human-readable explanation of why this strategy was recommended.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Estimated downtime if using Cutover strategy.
    /// </summary>
    public TimeSpan EstimatedDowntimeCutover { get; set; }

    /// <summary>
    /// Estimated downtime if using ContinuousSync strategy (final sync window).
    /// Null when ContinuousSync is not available (e.g., cross-platform migrations).
    /// </summary>
    public TimeSpan? EstimatedDowntimeContinuousSync { get; set; }

    /// <summary>
    /// Additional considerations for the migration (e.g., trigger risks, FK complexity).
    /// </summary>
    public List<string> Considerations { get; set; } = [];
}