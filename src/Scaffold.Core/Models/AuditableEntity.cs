namespace Scaffold.Core.Models;

/// <summary>
/// Base class for entities that track creation and modification timestamps.
/// Timestamps are automatically set by the DbContext SaveChangesAsync override.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}