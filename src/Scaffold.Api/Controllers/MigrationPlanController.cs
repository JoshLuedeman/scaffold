using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Api.Dtos;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/migration-plans")]
public class MigrationPlanController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ScaffoldDbContext _dbContext;

    public MigrationPlanController(
        IProjectRepository projectRepository,
        ScaffoldDbContext dbContext)
    {
        _projectRepository = projectRepository;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateMigrationPlanRequest request, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            // Default IncludedObjects to all objects from the latest assessment
            var includedObjects = request.IncludedObjects ?? [];
            if (includedObjects.Count == 0 && project.Assessment?.Schema.Objects is { Count: > 0 } objects)
            {
                includedObjects = objects.Select(o => o.Name).ToList();
            }

            // Default TargetTier to the assessment's recommendation if not overridden
            var targetTier = project.Assessment?.Recommendation ?? new TierRecommendation();
            if (request.TargetTierOverride is { } t)
            {
                targetTier = new TierRecommendation
                {
                    ServiceTier = t.ServiceTier,
                    ComputeSize = t.ComputeSize,
                    Dtus = t.Dtus,
                    VCores = t.VCores,
                    StorageGb = t.StorageGb,
                    EstimatedMonthlyCostUsd = t.EstimatedMonthlyCostUsd,
                    Reasoning = t.Reasoning ?? string.Empty
                };
            }

            var plan = new MigrationPlan
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Strategy = request.Strategy,
                IncludedObjects = includedObjects,
                ExcludedObjects = request.ExcludedObjects ?? [],
                ScheduledAt = request.ScheduledAt,
                PreMigrationScripts = MapScripts(request.PreMigrationScripts, request.PreMigrationScript, MigrationScriptPhase.Pre),
                PostMigrationScripts = MapScripts(request.PostMigrationScripts, request.PostMigrationScript, MigrationScriptPhase.Post),
                TargetTier = targetTier,
                UseExistingTarget = request.UseExistingTarget,
                ExistingTargetConnectionString = request.ExistingTargetConnectionString
            };

            // Auto-populate source connection string from project's assessed connection
            if (project.SourceConnection is not null && string.IsNullOrEmpty(plan.SourceConnectionString))
            {
                plan.SourceConnectionString = project.SourceConnection.BuildConnectionString();
            }

            // Replace existing plan if present
            if (project.MigrationPlan is not null)
            {
                _dbContext.MigrationPlans.Remove(project.MigrationPlan);
            }

            _dbContext.MigrationPlans.Add(plan);
            await _dbContext.SaveChangesAsync(ct);

            project.MigrationPlan = plan;
            project.Status = ProjectStatus.MigrationPlanned;
            await _projectRepository.UpdateAsync(project);

            return CreatedAtAction(nameof(GetLatest), new { projectId }, MigrationPlanResponse.FromModel(plan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(Guid projectId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null)
                return NotFound("No migration plan found for this project.");

            return Ok(MigrationPlanResponse.FromModel(project.MigrationPlan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{planId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, [FromBody] UpdateMigrationPlanRequest request, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null || project.MigrationPlan.Id != planId)
                return NotFound("Migration plan not found.");

            if (project.MigrationPlan.IsApproved)
            {
                // Reset approval so the plan can be re-edited and re-approved
                project.MigrationPlan.IsApproved = false;
                project.MigrationPlan.ApprovedBy = null;
            }

            var plan = project.MigrationPlan;

            if (request.Strategy.HasValue) plan.Strategy = request.Strategy.Value;
            if (request.IncludedObjects is not null) plan.IncludedObjects = request.IncludedObjects;
            if (request.ExcludedObjects is not null) plan.ExcludedObjects = request.ExcludedObjects;
            if (request.ScheduledAt.HasValue) plan.ScheduledAt = request.ScheduledAt;
            if (request.PreMigrationScripts is not null || request.PreMigrationScript is not null)
                plan.PreMigrationScripts = MapScripts(request.PreMigrationScripts, request.PreMigrationScript, MigrationScriptPhase.Pre);
            if (request.PostMigrationScripts is not null || request.PostMigrationScript is not null)
                plan.PostMigrationScripts = MapScripts(request.PostMigrationScripts, request.PostMigrationScript, MigrationScriptPhase.Post);
            if (request.UseExistingTarget.HasValue) plan.UseExistingTarget = request.UseExistingTarget.Value;
            if (!string.IsNullOrEmpty(request.ExistingTargetConnectionString)) plan.ExistingTargetConnectionString = request.ExistingTargetConnectionString;

            if (request.TargetTierOverride is { } t)
            {
                plan.TargetTier = new TierRecommendation
                {
                    ServiceTier = t.ServiceTier,
                    ComputeSize = t.ComputeSize,
                    Dtus = t.Dtus,
                    VCores = t.VCores,
                    StorageGb = t.StorageGb,
                    EstimatedMonthlyCostUsd = t.EstimatedMonthlyCostUsd,
                    Reasoning = t.Reasoning ?? string.Empty
                };
            }

            await _dbContext.SaveChangesAsync(ct);

            return Ok(MigrationPlanResponse.FromModel(plan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{planId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid projectId, Guid planId, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null || project.MigrationPlan.Id != planId)
                return NotFound("Migration plan not found.");

            if (project.MigrationPlan.IsApproved)
                return BadRequest("Migration plan is already approved.");

            project.MigrationPlan.IsApproved = true;
            project.MigrationPlan.ApprovedBy = User.Identity?.Name ?? "unknown";
            project.MigrationPlan.IsRejected = false;
            project.MigrationPlan.RejectedBy = null;
            project.MigrationPlan.RejectionReason = null;

            // If a schedule is set, mark as Scheduled so the background service picks it up
            project.MigrationPlan.Status = project.MigrationPlan.ScheduledAt.HasValue
                ? MigrationStatus.Scheduled
                : MigrationStatus.Pending;

            await _dbContext.SaveChangesAsync(ct);

            return Ok(MigrationPlanResponse.FromModel(project.MigrationPlan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{planId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid projectId, Guid planId, [FromBody] RejectMigrationPlanRequest request, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null || project.MigrationPlan.Id != planId)
                return NotFound("Migration plan not found.");

            if (project.MigrationPlan.IsRejected)
                return BadRequest("Migration plan is already rejected.");

            project.MigrationPlan.IsRejected = true;
            project.MigrationPlan.RejectedBy = User.Identity?.Name ?? "unknown";
            project.MigrationPlan.RejectionReason = request.Reason;
            project.MigrationPlan.IsApproved = false;
            project.MigrationPlan.ApprovedBy = null;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(MigrationPlanResponse.FromModel(project.MigrationPlan));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{planId:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid projectId, Guid planId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null || project.MigrationPlan.Id != planId)
                return NotFound("Migration plan not found.");

            var plan = project.MigrationPlan;
            var assessment = project.Assessment;

            var sourceDb = new SourceDatabaseSummary(
                TableCount: assessment?.Schema.TableCount ?? 0,
                ViewCount: assessment?.Schema.ViewCount ?? 0,
                StoredProcedureCount: assessment?.Schema.StoredProcedureCount ?? 0,
                IndexCount: assessment?.Schema.IndexCount ?? 0,
                TriggerCount: assessment?.Schema.TriggerCount ?? 0,
                TotalRows: assessment?.DataProfile.TotalRowCount ?? 0,
                TotalSizeFormatted: FormatBytes(assessment?.DataProfile.TotalSizeBytes ?? 0));

            var targetTier = new TargetTierSummary(
                ServiceTier: plan.TargetTier.ServiceTier,
                ComputeSize: plan.TargetTier.ComputeSize,
                StorageGb: plan.TargetTier.StorageGb,
                EstimatedMonthlyCostUsd: plan.TargetTier.EstimatedMonthlyCostUsd,
                Reasoning: plan.TargetTier.Reasoning);

            var totalSourceObjects = assessment?.Schema.Objects.Count ?? 0;
            var objectCounts = new ObjectCountsSummary(
                TotalObjectsInSource: totalSourceObjects,
                IncludedCount: plan.IncludedObjects.Count,
                ExcludedCount: plan.ExcludedObjects.Count);

            var timeline = EstimateTimeline(plan.Strategy, assessment?.DataProfile.TotalSizeBytes ?? 0);

            var strategyDescription = plan.Strategy switch
            {
                MigrationStrategy.Cutover => "Full cutover migration — the source database will be taken offline, data migrated, and the target brought online. Best for smaller databases or when a maintenance window is acceptable.",
                MigrationStrategy.ContinuousSync => "Continuous sync migration — data is replicated in real-time from source to target, with a final cutover when ready. Minimizes downtime for larger databases.",
                _ => plan.Strategy.ToString()
            };

            var summary = new MigrationPlanSummaryResponse(
                PlanId: plan.Id,
                ProjectId: plan.ProjectId,
                Strategy: plan.Strategy.ToString(),
                StrategyDescription: strategyDescription,
                SourceDatabase: sourceDb,
                TargetTier: targetTier,
                ObjectCounts: objectCounts,
                EstimatedTimeline: timeline,
                ScheduledAt: plan.ScheduledAt,
                IsApproved: plan.IsApproved,
                ApprovedBy: plan.ApprovedBy,
                IsRejected: plan.IsRejected,
                RejectionReason: plan.RejectionReason);

            return Ok(summary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{planId:guid}/validate-start")]
    public async Task<IActionResult> ValidateStart(Guid projectId, Guid planId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null || project.MigrationPlan.Id != planId)
                return NotFound("Migration plan not found.");

            if (!project.MigrationPlan.IsApproved)
                return BadRequest("Cannot start migration: the plan has not been approved.");

            if (project.MigrationPlan.IsRejected)
                return BadRequest("Cannot start migration: the plan has been rejected.");

            return Ok(new { CanStart = true, Message = "Migration plan is approved and ready to start." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
            >= 1_024 => $"{bytes / 1_024.0:F2} KB",
            _ => $"{bytes} bytes"
        };
    }

    // ~1 GB per minute for cutover, 2x for continuous sync initial load
    private static TimelineEstimate EstimateTimeline(MigrationStrategy strategy, long totalSizeBytes)
    {
        var sizeGb = totalSizeBytes / 1_073_741_824.0;
        if (sizeGb < 0.001) sizeGb = 0.001;

        return strategy switch
        {
            MigrationStrategy.Cutover => new TimelineEstimate(
                Description: $"Estimated ~{sizeGb:F1} GB to migrate at ~1 GB/min during cutover window.",
                EstimatedMinutes: Math.Ceiling(sizeGb)),
            MigrationStrategy.ContinuousSync => new TimelineEstimate(
                Description: $"Estimated ~{sizeGb:F1} GB initial sync at ~0.5 GB/min, then continuous replication until final cutover.",
                EstimatedMinutes: Math.Ceiling(sizeGb * 2)),
            _ => new TimelineEstimate("Unknown strategy.", 0)
        };
    }

    private static List<MigrationScript> MapScripts(List<MigrationScriptDto>? dtos, string? legacyScript, MigrationScriptPhase phase)
    {
        if (dtos is { Count: > 0 })
        {
            return dtos.Select(d => new MigrationScript
            {
                ScriptId = d.ScriptId,
                Label = d.Label,
                ScriptType = Enum.Parse<MigrationScriptType>(d.ScriptType),
                Phase = Enum.Parse<MigrationScriptPhase>(d.Phase),
                SqlContent = d.SqlContent,
                IsEnabled = d.IsEnabled,
                Order = d.Order
            }).ToList();
        }

        if (!string.IsNullOrEmpty(legacyScript))
        {
            return
            [
                new MigrationScript
                {
                    ScriptId = Guid.NewGuid().ToString(),
                    Label = phase == MigrationScriptPhase.Pre ? "Pre-migration script" : "Post-migration script",
                    ScriptType = MigrationScriptType.Custom,
                    Phase = phase,
                    SqlContent = legacyScript,
                    IsEnabled = true,
                    Order = 0
                }
            ];
        }

        return [];
    }
}
