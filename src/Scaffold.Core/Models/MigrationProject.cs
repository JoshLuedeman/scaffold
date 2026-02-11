using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

public class MigrationProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Created;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ConnectionInfo? SourceConnection { get; set; }
    public AssessmentReport? Assessment { get; set; }
    public MigrationPlan? MigrationPlan { get; set; }
}
