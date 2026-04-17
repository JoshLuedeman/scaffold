using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

public class MigrationPlan : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
    public DatabasePlatform SourcePlatform { get; set; } = DatabasePlatform.SqlServer;
    public DatabasePlatform TargetPlatform { get; set; } = DatabasePlatform.SqlServer;
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

    /// <summary>Timeout in seconds for SqlBulkCopy operations. Default: 600. Clamped to [30, 3600].</summary>
    public int? BulkCopyTimeoutSeconds { get; set; }

    /// <summary>Timeout in seconds for pre/post migration script execution. Default: 300. Clamped to [30, 3600].</summary>
    public int? ScriptTimeoutSeconds { get; set; }

    public MigrationStatus Status { get; set; } = MigrationStatus.Pending;
    public Guid? MigrationId { get; set; }

    public string? TargetRegion { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public bool IsRejected { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
}
