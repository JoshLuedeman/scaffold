using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Api.Dtos;

public record CreateMigrationPlanRequest(
    MigrationStrategy Strategy,
    List<string>? IncludedObjects = null,
    List<string>? ExcludedObjects = null,
    DateTime? ScheduledAt = null,
    string? PreMigrationScript = null,
    string? PostMigrationScript = null,
    TierOverrideDto? TargetTierOverride = null,
    bool UseExistingTarget = false,
    string? ExistingTargetConnectionString = null);

public record UpdateMigrationPlanRequest(
    MigrationStrategy? Strategy = null,
    List<string>? IncludedObjects = null,
    List<string>? ExcludedObjects = null,
    DateTime? ScheduledAt = null,
    string? PreMigrationScript = null,
    string? PostMigrationScript = null,
    TierOverrideDto? TargetTierOverride = null,
    bool? UseExistingTarget = null,
    string? ExistingTargetConnectionString = null);

public record TierOverrideDto(
    string ServiceTier,
    string ComputeSize,
    int? Dtus = null,
    int? VCores = null,
    int StorageGb = 0,
    decimal EstimatedMonthlyCostUsd = 0,
    string? Reasoning = null);

public record MigrationPlanResponse(
    Guid Id,
    Guid ProjectId,
    string Strategy,
    List<string> IncludedObjects,
    List<string> ExcludedObjects,
    DateTime? ScheduledAt,
    string? PreMigrationScript,
    string? PostMigrationScript,
    TierRecommendation TargetTier,
    bool UseExistingTarget,
    string? ExistingTargetConnectionString,
    DateTime CreatedAt,
    bool IsApproved,
    string? ApprovedBy,
    bool IsRejected,
    string? RejectedBy,
    string? RejectionReason)
{
    public static MigrationPlanResponse FromModel(MigrationPlan plan) =>
        new(
            plan.Id,
            plan.ProjectId,
            plan.Strategy.ToString(),
            plan.IncludedObjects,
            plan.ExcludedObjects,
            plan.ScheduledAt,
            plan.PreMigrationScript,
            plan.PostMigrationScript,
            plan.TargetTier,
            plan.UseExistingTarget,
            plan.ExistingTargetConnectionString,
            plan.CreatedAt,
            plan.IsApproved,
            plan.ApprovedBy,
            plan.IsRejected,
            plan.RejectedBy,
            plan.RejectionReason);
}

public record RejectMigrationPlanRequest(string? Reason = null);

public record MigrationPlanSummaryResponse(
    Guid PlanId,
    Guid ProjectId,
    string Strategy,
    string StrategyDescription,
    SourceDatabaseSummary SourceDatabase,
    TargetTierSummary TargetTier,
    ObjectCountsSummary ObjectCounts,
    TimelineEstimate EstimatedTimeline,
    DateTime? ScheduledAt,
    bool IsApproved,
    string? ApprovedBy,
    bool IsRejected,
    string? RejectionReason);

public record SourceDatabaseSummary(
    int TableCount,
    int ViewCount,
    int StoredProcedureCount,
    int IndexCount,
    int TriggerCount,
    long TotalRows,
    string TotalSizeFormatted);

public record TargetTierSummary(
    string ServiceTier,
    string ComputeSize,
    int StorageGb,
    decimal EstimatedMonthlyCostUsd,
    string Reasoning);

public record ObjectCountsSummary(
    int TotalObjectsInSource,
    int IncludedCount,
    int ExcludedCount);

public record TimelineEstimate(
    string Description,
    double EstimatedMinutes);
