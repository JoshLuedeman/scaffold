using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Migration engine for SQL Server → PostgreSQL cross-platform migrations.
/// Orchestrates schema deployment, data copy, script execution, and validation.
/// Only the Cutover strategy is supported; ContinuousSync requires Change Tracking
/// which is SQL Server-specific.
/// </summary>
public class SqlServerToPostgreSqlMigrator : IMigrationEngine
{
    private readonly PostgreSqlSchemaDeployer _schemaDeployer;
    private readonly CrossPlatformBulkCopier _bulkCopier;
    private readonly PostgreSqlScriptExecutor _scriptExecutor;
    private readonly PostgreSqlValidationEngine _validationEngine;

    public SqlServerToPostgreSqlMigrator(
        PostgreSqlSchemaDeployer schemaDeployer,
        CrossPlatformBulkCopier bulkCopier,
        PostgreSqlScriptExecutor scriptExecutor,
        PostgreSqlValidationEngine validationEngine)
    {
        _schemaDeployer = schemaDeployer ?? throw new ArgumentNullException(nameof(schemaDeployer));
        _bulkCopier = bulkCopier ?? throw new ArgumentNullException(nameof(bulkCopier));
        _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));
    }

    public SqlServerToPostgreSqlMigrator()
    {
        var schemaReader = new SqlServerSchemaReader();
        var ddlTranslator = new DdlTranslator();
        _schemaDeployer = new PostgreSqlSchemaDeployer(schemaReader, ddlTranslator);
        _bulkCopier = new CrossPlatformBulkCopier();
        _scriptExecutor = new PostgreSqlScriptExecutor();
        _validationEngine = new PostgreSqlValidationEngine();
    }

    public string SourcePlatform => "SqlServer";

    public async Task<MigrationResult> ExecuteCutoverAsync(
        MigrationPlan plan,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
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

            // Step 1: Deploy schema (reads SQL Server schema, translates DDL, deploys to PG)
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeployment",
                PercentComplete = 0,
                Message = "Starting cross-platform schema deployment..."
            });

            await _schemaDeployer.DeploySchemaAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                plan.IncludedObjects,
                progress,
                ct);

            // Step 2: Execute pre-migration scripts on PG target
            if (plan.PreMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = "PreScripts",
                    PercentComplete = 0,
                    Message = "Running pre-migration scripts on PostgreSQL target..."
                });
                await _scriptExecutor.ExecuteScriptsAsync(
                    plan.ExistingTargetConnectionString!,
                    plan.PreMigrationScripts,
                    progress, ct, plan.ScriptTimeoutSeconds);
            }

            // Step 3: Bulk copy data (SQL Server → PG via COPY protocol)
            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = 0,
                Message = "Starting cross-platform data migration..."
            });

            var rowsMigrated = await _bulkCopier.CopyDataAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                plan.IncludedObjects,
                progress,
                plan.BulkCopyTimeoutSeconds,
                ct);

            // Step 4: Execute post-migration scripts on PG target
            if (plan.PostMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = "PostScripts",
                    PercentComplete = 0,
                    Message = "Running post-migration scripts on PostgreSQL target..."
                });
                await _scriptExecutor.ExecuteScriptsAsync(
                    plan.ExistingTargetConnectionString!,
                    plan.PostMigrationScripts,
                    progress, ct, plan.ScriptTimeoutSeconds);
            }

            // Step 5: Cross-platform validation (row count comparison)
            progress?.Report(new MigrationProgress
            {
                Phase = "Validation",
                PercentComplete = 0,
                Message = "Running cross-platform validation..."
            });

            var validationSummary = await _validationEngine.ValidateAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                plan.IncludedObjects,
                ct);

            result.RowsMigrated = rowsMigrated;
            result.Validations = validationSummary.Results;
            result.Success = validationSummary.AllPassed;
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

    /// <summary>
    /// Continuous sync is not supported for cross-platform SQL Server → PostgreSQL migrations.
    /// Change Tracking is a SQL Server-specific feature.
    /// </summary>
    public Task StartContinuousSyncAsync(
        MigrationPlan plan,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Continuous sync is not supported for cross-platform SQL Server → PostgreSQL migrations. Use Cutover strategy.");
    }

    /// <summary>
    /// CompleteCutover is only applicable for ContinuousSync strategy,
    /// which is not supported for cross-platform migrations.
    /// </summary>
    public Task<MigrationResult> CompleteCutoverAsync(Guid migrationId, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "CompleteCutover is only applicable for ContinuousSync strategy, which is not supported for cross-platform migrations.");
    }

    private static void ValidateConnectionStrings(MigrationPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.SourceConnectionString))
            throw new ArgumentException("SourceConnectionString is required.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.ExistingTargetConnectionString))
            throw new ArgumentException("ExistingTargetConnectionString is required.", nameof(plan));
    }
}