using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure.Data;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Services;

public class MigrationSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MigrationSchedulerService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    public MigrationSchedulerService(IServiceScopeFactory scopeFactory, ILogger<MigrationSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Migration scheduler started, polling every {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollForScheduledMigrationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error polling for scheduled migrations");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task PollForScheduledMigrationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();

        var duePlans = await db.MigrationPlans
            .Include(p => p.TargetTier)
            .Where(p => p.IsApproved
                && !p.IsRejected
                && p.Status == MigrationStatus.Scheduled
                && p.ScheduledAt.HasValue
                && p.ScheduledAt.Value <= DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var plan in duePlans)
        {
            _logger.LogInformation("Starting scheduled migration for plan {PlanId}, project {ProjectId}",
                plan.Id, plan.ProjectId);

            try
            {
                await ExecuteScheduledMigrationAsync(scope, plan, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start scheduled migration for plan {PlanId}", plan.Id);
                plan.Status = MigrationStatus.Failed;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task ExecuteScheduledMigrationAsync(IServiceScope scope, Core.Models.MigrationPlan plan, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var migrationEngineFactory = scope.ServiceProvider.GetRequiredService<IMigrationEngineFactory>();
        var progressService = scope.ServiceProvider.GetRequiredService<MigrationProgressService>();
        var validationEngine = scope.ServiceProvider.GetRequiredService<ValidationEngine>();
        var protector = scope.ServiceProvider.GetRequiredService<IConnectionStringProtector>();

        var project = await projectRepo.GetByIdAsync(plan.ProjectId);

        if (string.IsNullOrWhiteSpace(plan.SourceConnectionString) ||
            string.IsNullOrWhiteSpace(plan.ExistingTargetConnectionString))
        {
            _logger.LogWarning("Skipping scheduled migration for plan {PlanId}: missing connection strings", plan.Id);
            plan.Status = MigrationStatus.Failed;
            await db.SaveChangesAsync(ct);
            return;
        }

        var migrationId = Guid.NewGuid();
        plan.Status = MigrationStatus.Running;
        plan.MigrationId = migrationId;
        project.Status = ProjectStatus.Migrating;
        await projectRepo.UpdateAsync(project);
        await db.SaveChangesAsync(ct);

        // Pre-generate canned script SQL
        if (project.Assessment?.Schema is { } schema)
        {
            foreach (var script in plan.PreMigrationScripts.Concat(plan.PostMigrationScripts))
            {
                if (script.ScriptType == MigrationScriptType.Canned && string.IsNullOrWhiteSpace(script.SqlContent))
                {
                    script.SqlContent = MigrationScriptGenerator.Generate(script.ScriptId, schema) ?? string.Empty;
                }
            }
        }

        progressService.SetMigrationId(migrationId.ToString());
        await progressService.MigrationStarted(migrationId.ToString());

        // Decrypt connection strings for engine/validation use
        var sourceConnStr = protector.Unprotect(plan.SourceConnectionString!);
        var targetConnStr = protector.Unprotect(plan.ExistingTargetConnectionString!);
        plan.SourceConnectionString = sourceConnStr;
        plan.ExistingTargetConnectionString = targetConnStr;
        // Prevent EF from persisting decrypted values back to the database
        db.Entry(plan).Property(p => p.SourceConnectionString).IsModified = false;
        db.Entry(plan).Property(p => p.ExistingTargetConnectionString).IsModified = false;

        try
        {
            Core.Models.MigrationResult result;
            var migrationEngine = migrationEngineFactory.Create(plan.SourcePlatform);

            if (plan.Strategy == MigrationStrategy.ContinuousSync)
            {
                await migrationEngine.StartContinuousSyncAsync(plan, progressService, ct);
                return;
            }

            result = await migrationEngine.ExecuteCutoverAsync(plan, progressService, ct);
            result.Id = migrationId;

            var validationSummary = await validationEngine.ValidateAsync(
                sourceConnStr,
                targetConnStr,
                plan.IncludedObjects,
                CancellationToken.None);

            result.Validations = validationSummary.Results;
            result.Success = result.Success && validationSummary.AllPassed;

            db.MigrationResults.Add(result);
            plan.Status = result.Success ? MigrationStatus.Completed : MigrationStatus.Failed;
            project.Status = result.Success ? ProjectStatus.MigrationComplete : ProjectStatus.Failed;
            await db.SaveChangesAsync(CancellationToken.None);

            await progressService.MigrationCompleted(migrationId.ToString());

            _logger.LogInformation("Scheduled migration {MigrationId} completed, success={Success}",
                migrationId, result.Success);
        }
        catch (Exception ex)
        {
            plan.Status = MigrationStatus.Failed;
            project.Status = ProjectStatus.Failed;
            try { await db.SaveChangesAsync(CancellationToken.None); } catch { }
            await progressService.MigrationFailed(migrationId.ToString(), "Migration failed due to an internal error. Check server logs for details.");
            _logger.LogError(ex, "Scheduled migration {MigrationId} failed", migrationId);
        }
    }
}
