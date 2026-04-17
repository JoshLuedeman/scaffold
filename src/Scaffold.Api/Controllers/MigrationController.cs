using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scaffold.Api.Services;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure.Data;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/migrations")]
public class MigrationController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ScaffoldDbContext _dbContext;
    private readonly IMigrationEngineFactory _migrationEngineFactory;
    private readonly MigrationProgressService _progressService;
    private readonly ValidationEngine _validationEngine;
    private readonly IConnectionStringProtector _protector;
    private readonly IPreMigrationValidator _preMigrationValidator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        IProjectRepository projectRepository,
        ScaffoldDbContext dbContext,
        IMigrationEngineFactory migrationEngineFactory,
        MigrationProgressService progressService,
        ValidationEngine validationEngine,
        IConnectionStringProtector protector,
        IPreMigrationValidator preMigrationValidator,
        IServiceScopeFactory scopeFactory,
        ILogger<MigrationController> logger)
    {
        _projectRepository = projectRepository;
        _dbContext = dbContext;
        _migrationEngineFactory = migrationEngineFactory;
        _progressService = progressService;
        _validationEngine = validationEngine;
        _protector = protector;
        _preMigrationValidator = preMigrationValidator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Start a migration. The plan must be approved.
    /// Returns 202 Accepted with the migration ID.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start(Guid projectId, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null)
                return NotFound("No migration plan found for this project.");

            if (!project.MigrationPlan.IsApproved)
                return BadRequest("Migration plan must be approved before starting.");

            if (project.MigrationPlan.IsRejected)
                return BadRequest("Cannot start a rejected migration plan.");

            var migrationId = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(project.MigrationPlan.SourceConnectionString))
                return BadRequest("Source connection string is not configured. Please re-save the migration plan.");

            if (string.IsNullOrWhiteSpace(project.MigrationPlan.ExistingTargetConnectionString))
                return BadRequest("Target connection string is required. Configure a target database in the migration plan.");

            var preValidation = await _preMigrationValidator.ValidateAsync(project.MigrationPlan);
            if (!preValidation.IsValid)
                return BadRequest(new { Errors = preValidation.Errors, Warnings = preValidation.Warnings });

            project.Status = ProjectStatus.Migrating;
            await _projectRepository.UpdateAsync(project);

            var plan = project.MigrationPlan;
            plan.Status = MigrationStatus.Running;
            plan.MigrationId = migrationId;
            await _dbContext.SaveChangesAsync(ct);

            // Pre-generate SQL for canned scripts that don't have content yet
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

            // Start migration as a background task
            // Decrypt connection strings before entering the background task
            var sourceConnStr = _protector.Unprotect(plan.SourceConnectionString!);
            var targetConnStr = _protector.Unprotect(plan.ExistingTargetConnectionString!);
            var migrationEngine = _migrationEngineFactory.Create(plan.SourcePlatform, plan.TargetPlatform);

            // Capture IDs and singleton references for use in the background task
            var planId = plan.Id;
            var projectId2 = project.Id;
            var migrationIdStr = migrationId.ToString();
            var scopeFactory = _scopeFactory;
            var progressService = _progressService;
            var validationEngine = _validationEngine;
            var logger = _logger;

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
                    var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

                    // Reload entities in the new scope
                    var bgPlan = await db.MigrationPlans
                        .Include(p => p.PreMigrationScripts)
                        .Include(p => p.PostMigrationScripts)
                        .FirstAsync(p => p.Id == planId);
                    var bgProject = await projectRepo.GetByIdAsync(projectId2);

                    progressService.SetMigrationId(migrationIdStr);
                    await progressService.MigrationStarted(migrationIdStr);

                    // Set decrypted connection strings on plan for engine use
                    bgPlan.SourceConnectionString = sourceConnStr;
                    bgPlan.ExistingTargetConnectionString = targetConnStr;
                    // Prevent EF from persisting decrypted values back to the database
                    db.Entry(bgPlan).Property(p => p.SourceConnectionString).IsModified = false;
                    db.Entry(bgPlan).Property(p => p.ExistingTargetConnectionString).IsModified = false;

                    Core.Models.MigrationResult result;

                    if (bgPlan.Strategy == MigrationStrategy.ContinuousSync)
                    {
                        await migrationEngine.StartContinuousSyncAsync(bgPlan, progressService, CancellationToken.None);
                        // For continuous sync, the result comes from CompleteCutoverAsync later
                        return;
                    }

                    result = await migrationEngine.ExecuteCutoverAsync(bgPlan, progressService, CancellationToken.None);
                    result.Id = migrationId;

                    // Run post-migration validation
                    var validationSummary = await validationEngine.ValidateAsync(
                        sourceConnStr,
                        targetConnStr,
                        bgPlan.IncludedObjects,
                        CancellationToken.None);

                    result.Validations = validationSummary.Results;
                    result.Success = result.Success && validationSummary.AllPassed;

                    db.MigrationResults.Add(result);
                    bgPlan.Status = result.Success ? MigrationStatus.Completed : MigrationStatus.Failed;
                    bgProject.Status = result.Success ? ProjectStatus.MigrationComplete : ProjectStatus.Failed;
                    await db.SaveChangesAsync(CancellationToken.None);

                    await progressService.MigrationCompleted(migrationIdStr);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Migration {MigrationId} failed", migrationIdStr);
                    try
                    {
                        await using var errorScope = scopeFactory.CreateAsyncScope();
                        var errorDb = errorScope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
                        var errorPlan = await errorDb.MigrationPlans.FindAsync(planId);
                        var errorProjectRepo = errorScope.ServiceProvider.GetRequiredService<IProjectRepository>();
                        var errorProject = await errorProjectRepo.GetByIdAsync(projectId2);
                        if (errorPlan is not null)
                            errorPlan.Status = MigrationStatus.Failed;
                        errorProject.Status = ProjectStatus.Failed;
                        await errorDb.SaveChangesAsync(CancellationToken.None);
                    }
                    catch { }
                    await progressService.MigrationFailed(migrationIdStr, "Migration failed due to an internal error. Check server logs for details.");
                }
            });

            return Accepted(new { MigrationId = migrationId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get migration status/result.
    /// </summary>
    [HttpGet("{migrationId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid migrationId, CancellationToken ct)
    {
        var result = await _dbContext.MigrationResults
            .FirstOrDefaultAsync(r => r.Id == migrationId && r.ProjectId == projectId, ct);

        if (result is null)
            return NotFound("Migration not found.");

        return Ok(result);
    }

    /// <summary>
    /// Trigger final cutover for a continuous sync migration.
    /// </summary>
    [HttpPost("{migrationId:guid}/cutover")]
    public async Task<IActionResult> Cutover(Guid projectId, Guid migrationId, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.Status != ProjectStatus.Migrating)
                return BadRequest("Project is not currently migrating.");

            if (project.MigrationPlan?.Strategy != MigrationStrategy.ContinuousSync)
                return BadRequest("Cutover is only available for continuous sync migrations.");

            var migrationEngine = _migrationEngineFactory.Create(project.MigrationPlan.SourcePlatform, project.MigrationPlan.TargetPlatform);
            var result = await migrationEngine.CompleteCutoverAsync(migrationId, ct);

            // Run post-cutover validation
            var plan = project.MigrationPlan;
            var sourceConnStr = _protector.Unprotect(plan.SourceConnectionString!);
            var targetConnStr = _protector.Unprotect(plan.ExistingTargetConnectionString!);
            var validationSummary = await _validationEngine.ValidateAsync(
                sourceConnStr,
                targetConnStr,
                plan.IncludedObjects,
                ct);

            result.Validations = validationSummary.Results;
            result.Success = result.Success && validationSummary.AllPassed;

            _dbContext.MigrationResults.Add(result);

            project.Status = result.Success
                ? ProjectStatus.MigrationComplete
                : ProjectStatus.Failed;
            await _projectRepository.UpdateAsync(project);
            await _dbContext.SaveChangesAsync(ct);

            await _progressService.MigrationCompleted(migrationId.ToString());

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Run validation on a completed migration.
    /// </summary>
    [HttpPost("{migrationId:guid}/validate")]
    public async Task<IActionResult> Validate(Guid projectId, Guid migrationId, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.MigrationPlan is null)
                return NotFound("No migration plan found for this project.");

            var result = await _dbContext.MigrationResults
                .FirstOrDefaultAsync(r => r.Id == migrationId && r.ProjectId == projectId, ct);

            if (result is null)
                return NotFound("Migration not found.");

            var plan = project.MigrationPlan;
            var sourceConnStr = _protector.Unprotect(plan.SourceConnectionString!);
            var targetConnStr = _protector.Unprotect(plan.ExistingTargetConnectionString!);
            var validationSummary = await _validationEngine.ValidateAsync(
                sourceConnStr,
                targetConnStr,
                plan.IncludedObjects,
                ct);

            result.Validations = validationSummary.Results;
            result.Success = validationSummary.AllPassed;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(validationSummary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get persisted progress records for a migration.
    /// </summary>
    [HttpGet("{migrationId:guid}/progress")]
    public async Task<IActionResult> GetProgress(Guid projectId, Guid migrationId, CancellationToken ct)
    {
        var records = await _dbContext.MigrationProgressRecords
            .Where(r => r.MigrationId == migrationId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync(ct);

        return Ok(records);
    }
}
