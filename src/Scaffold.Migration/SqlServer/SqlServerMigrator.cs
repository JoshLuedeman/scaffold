using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Migration.SqlServer;

public class SqlServerMigrator : IMigrationEngine
{
    private readonly SchemaDeployer _schemaDeployer;
    private readonly BulkDataCopier _bulkDataCopier;
    private readonly ScriptExecutor _scriptExecutor;
    private ChangeTrackingSyncEngine? _syncEngine;
    private MigrationPlan? _activePlan;

    public SqlServerMigrator()
    {
        _schemaDeployer = new SchemaDeployer();
        _bulkDataCopier = new BulkDataCopier();
        _scriptExecutor = new ScriptExecutor();
    }

    public SqlServerMigrator(SchemaDeployer schemaDeployer, BulkDataCopier bulkDataCopier)
        : this(schemaDeployer, bulkDataCopier, new ScriptExecutor()) { }

    public SqlServerMigrator(SchemaDeployer schemaDeployer, BulkDataCopier bulkDataCopier, ScriptExecutor scriptExecutor)
    {
        _schemaDeployer = schemaDeployer;
        _bulkDataCopier = bulkDataCopier;
        _scriptExecutor = scriptExecutor;
    }

    public string SourcePlatform => "SqlServer";

    public async Task<MigrationResult> ExecuteCutoverAsync(MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default)
    {
        var result = new MigrationResult
        {
            Id = Guid.NewGuid(),
            ProjectId = plan.ProjectId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            ValidateConnectionStrings(plan);

            // Step 1: Deploy schema
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeployment",
                PercentComplete = 0,
                Message = "Starting schema deployment..."
            });

            var targetDb = SchemaDeployer.ExtractDatabaseName(plan.ExistingTargetConnectionString!);
            await _schemaDeployer.DeploySchemaAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                targetDb,
                progress,
                ct);

            // Step 2: Execute pre-migration scripts on target
            if (plan.PreMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress { Phase = "PreScripts", PercentComplete = 0, Message = "Running pre-migration scripts..." });
                await _scriptExecutor.ExecuteScriptsAsync(plan.ExistingTargetConnectionString!, plan.PreMigrationScripts, progress, ct);
            }

            // Step 3: Data migration via SqlBulkCopy
            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = 50,
                Message = "Starting data migration..."
            });

            var rowsMigrated = await _bulkDataCopier.CopyDataAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                plan.IncludedObjects,
                progress,
                ct);

            // Step 4: Execute post-migration scripts on target
            if (plan.PostMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress { Phase = "PostScripts", PercentComplete = 0, Message = "Running post-migration scripts..." });
                await _scriptExecutor.ExecuteScriptsAsync(plan.ExistingTargetConnectionString!, plan.PostMigrationScripts, progress, ct);
            }

            result.RowsMigrated = rowsMigrated;
            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    public async Task StartContinuousSyncAsync(MigrationPlan plan, IProgress<MigrationProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateConnectionStrings(plan);
        _activePlan = plan;

        // Schema deployment is the first step of continuous sync as well
        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaDeployment",
            PercentComplete = 0,
            Message = "Starting schema deployment for continuous sync..."
        });

        var targetDb = SchemaDeployer.ExtractDatabaseName(plan.ExistingTargetConnectionString!);
        await _schemaDeployer.DeploySchemaAsync(
            plan.SourceConnectionString!,
            plan.ExistingTargetConnectionString!,
            targetDb,
            progress,
            ct);

        // Execute pre-migration scripts before starting sync
        if (plan.PreMigrationScripts.Count > 0)
        {
            progress?.Report(new MigrationProgress { Phase = "PreScripts", PercentComplete = 0, Message = "Running pre-migration scripts..." });
            await _scriptExecutor.ExecuteScriptsAsync(plan.ExistingTargetConnectionString!, plan.PreMigrationScripts, progress, ct);
        }

        // Start Change Tracking based continuous sync
        _syncEngine = new ChangeTrackingSyncEngine(
            plan.SourceConnectionString!,
            plan.ExistingTargetConnectionString!,
            progress);

        await _syncEngine.StartAsync(ct);
    }

    public async Task<MigrationResult> CompleteCutoverAsync(Guid migrationId, CancellationToken ct = default)
    {
        if (_syncEngine is null || _activePlan is null)
            throw new InvalidOperationException("No active continuous sync session. Call StartContinuousSyncAsync first.");

        var result = await _syncEngine.CompleteCutoverAsync(_activePlan.ProjectId, ct);

        // Execute post-migration scripts after final sync
        if (_activePlan.PostMigrationScripts.Count > 0)
        {
            await _scriptExecutor.ExecuteScriptsAsync(_activePlan.ExistingTargetConnectionString!, _activePlan.PostMigrationScripts, ct: ct);
        }

        return result;
    }

    private static void ValidateConnectionStrings(MigrationPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.SourceConnectionString))
            throw new ArgumentException("SourceConnectionString is required.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.ExistingTargetConnectionString))
            throw new ArgumentException("ExistingTargetConnectionString is required.", nameof(plan));
    }
}
