namespace Scaffold.Core.Models;

public class MigrationProgressRecord
{
    public Guid Id { get; set; }
    public Guid MigrationId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public string CurrentTable { get; set; } = string.Empty;
    public long RowsProcessed { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
