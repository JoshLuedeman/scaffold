using System.ComponentModel.DataAnnotations.Schema;
using Scaffold.Core.Enums;

namespace Scaffold.Core.Models;

public class AssessmentReport
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public SchemaInventory Schema { get; set; } = new();
    public DataProfile DataProfile { get; set; } = new();
    public PerformanceProfile Performance { get; set; } = new();
    public List<CompatibilityIssue> CompatibilityIssues { get; set; } = [];
    public TierRecommendation Recommendation { get; set; } = new();

    public double CompatibilityScore { get; set; }
    public RiskRating Risk { get; set; }
}

public class SchemaInventory
{
    public int TableCount { get; set; }
    public int ViewCount { get; set; }
    public int StoredProcedureCount { get; set; }
    public int IndexCount { get; set; }
    public int TriggerCount { get; set; }
    public List<SchemaObject> Objects { get; set; } = [];
}

public class SchemaObject
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string ObjectType { get; set; } = string.Empty;
    public string? ParentObjectName { get; set; }
    public string? SubType { get; set; }
}

public class DataProfile
{
    public long TotalRowCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public List<TableProfile> Tables { get; set; } = [];
}

public class TableProfile
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public long SizeBytes { get; set; }
}

public class PerformanceProfile
{
    public double AvgCpuPercent { get; set; }
    public long MemoryUsedMb { get; set; }
    public double AvgIoMbPerSecond { get; set; }
    public long MaxDatabaseSizeMb { get; set; }
}

public class CompatibilityIssue
{
    public string ObjectName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public CompatibilitySeverity Severity { get; set; } = CompatibilitySeverity.Supported;
    public string? DocUrl { get; set; }
}

public class TierRecommendation
{
    public string ServiceTier { get; set; } = string.Empty;
    public string ComputeSize { get; set; } = string.Empty;
    public int? Dtus { get; set; }
    public int? VCores { get; set; }
    public int StorageGb { get; set; }
    public decimal EstimatedMonthlyCostUsd { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string? RecommendedRegion { get; set; }
    [NotMapped]
    public List<RegionPricing> RegionalPricing { get; set; } = [];
}
