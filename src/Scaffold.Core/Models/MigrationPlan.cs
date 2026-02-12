using System.ComponentModel.DataAnnotations.Schema;
using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

public class MigrationPlan
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public MigrationStrategy Strategy { get; set; }
    public List<string> IncludedObjects { get; set; } = [];
    public List<string> ExcludedObjects { get; set; } = [];
    public DateTime? ScheduledAt { get; set; }

    public List<MigrationScript> PreMigrationScripts { get; set; } = [];
    public List<MigrationScript> PostMigrationScripts { get; set; } = [];

    [Obsolete("Use PreMigrationScripts instead")]
    [NotMapped]
    public string? PreMigrationScript { get; set; }
    [Obsolete("Use PostMigrationScripts instead")]
    [NotMapped]
    public string? PostMigrationScript { get; set; }

    public TierRecommendation TargetTier { get; set; } = new();
    public bool UseExistingTarget { get; set; }
    public string? ExistingTargetConnectionString { get; set; }
    public string? SourceConnectionString { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? TargetRegion { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public bool IsRejected { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
}
