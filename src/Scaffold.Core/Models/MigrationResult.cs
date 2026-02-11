namespace Scaffold.Core.Models;

public class MigrationResult
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long RowsMigrated { get; set; }
    public long DataSizeBytes { get; set; }
    public List<ValidationResult> Validations { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public class ValidationResult
{
    public string TableName { get; set; } = string.Empty;
    public long SourceRowCount { get; set; }
    public long TargetRowCount { get; set; }
    public bool ChecksumMatch { get; set; }
    public bool Passed => SourceRowCount == TargetRowCount && ChecksumMatch;
}
