using Scaffold.Core.Models;

namespace Scaffold.Core.Interfaces;

public interface IMigrationEngine
{
    string SourcePlatform { get; }
    Task<MigrationResult> ExecuteCutoverAsync(MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default);
    Task StartContinuousSyncAsync(MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default);
    Task<MigrationResult> CompleteCutoverAsync(Guid migrationId, CancellationToken ct = default);
}

public class MigrationProgress
{
    public string Phase { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public string CurrentTable { get; set; } = string.Empty;
    public long RowsProcessed { get; set; }
    public string? Message { get; set; }
}
