using Npgsql;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Migration engine for PostgreSQL → Azure PostgreSQL same-platform migrations.
/// Orchestrates an 8-step cutover pipeline:
///   1. Schema extraction (source PG)
///   2. Extension evaluation + installation (target PG)
///   3. DDL generation + deployment (target PG)
///   4. Pre-migration scripts
///   5. Data copy (PG COPY protocol)
///   6. Sequence reset
///   7. Post-migration scripts
///   8. Validation (row-count comparison)
///
/// ContinuousSync will be supported in Wave 4 via logical replication (Issue #34).
/// </summary>
public class PostgreSqlMigrator : IMigrationEngine
{
    private readonly PostgreSqlSchemaExtractor _schemaExtractor;
    private readonly PostgreSqlDdlGenerator _ddlGenerator;
    private readonly PostgreSqlBulkCopier _bulkCopier;
    private readonly PostgreSqlScriptExecutor _scriptExecutor;
    private readonly PostgreSqlToPostgreSqlValidationEngine _validationEngine;
    private readonly AzureExtensionHandler _extensionHandler;

    public PostgreSqlMigrator(
        PostgreSqlSchemaExtractor schemaExtractor,
        PostgreSqlDdlGenerator ddlGenerator,
        PostgreSqlBulkCopier bulkCopier,
        PostgreSqlScriptExecutor scriptExecutor,
        PostgreSqlToPostgreSqlValidationEngine validationEngine,
        AzureExtensionHandler extensionHandler)
    {
        _schemaExtractor = schemaExtractor ?? throw new ArgumentNullException(nameof(schemaExtractor));
        _ddlGenerator = ddlGenerator ?? throw new ArgumentNullException(nameof(ddlGenerator));
        _bulkCopier = bulkCopier ?? throw new ArgumentNullException(nameof(bulkCopier));
        _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));
        _extensionHandler = extensionHandler ?? throw new ArgumentNullException(nameof(extensionHandler));
    }

    public string SourcePlatform => "PostgreSql";

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

            // Step 1: Extract schema from source PG
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaExtraction",
                PercentComplete = 0,
                Message = "Extracting schema from source PostgreSQL..."
            });

            var snapshot = await _schemaExtractor.ExtractSchemaAsync(
                plan.SourceConnectionString!,
                plan.IncludedObjects.Count > 0 ? plan.IncludedObjects : null,
                progress,
                ct);

            // Step 2: Handle extensions (evaluate + install compatible ones on target)
            progress?.Report(new MigrationProgress
            {
                Phase = "Extensions",
                PercentComplete = 10,
                Message = "Evaluating and installing PostgreSQL extensions..."
            });

            var extResult = await _extensionHandler.InstallExtensionsAsync(
                plan.ExistingTargetConnectionString!,
                snapshot.Extensions,
                progress,
                ct);

            if (!extResult.Success)
            {
                result.Success = false;
                result.Errors.AddRange(
                    extResult.Warnings
                        .Where(w => w.Severity == ExtensionWarningSeverity.Error)
                        .Select(w => w.Message));
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            // Step 3: Generate and deploy DDL on target
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeployment",
                PercentComplete = 15,
                Message = "Deploying schema to target PostgreSQL..."
            });

            var ddlStatements = _ddlGenerator.GenerateDdl(snapshot);

            if (ddlStatements.Count > 0)
            {
                await using var targetConn = new NpgsqlConnection(plan.ExistingTargetConnectionString);
                await targetConn.OpenAsync(ct);
                foreach (var ddl in ddlStatements)
                {
                    ct.ThrowIfCancellationRequested();
                    await using var cmd = new NpgsqlCommand(ddl, targetConn);
                    cmd.CommandTimeout = plan.ScriptTimeoutSeconds > 0 ? plan.ScriptTimeoutSeconds.Value : 300;
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Step 4: Execute pre-migration scripts on target
            if (plan.PreMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = "PreScripts",
                    PercentComplete = 25,
                    Message = "Running pre-migration scripts on target..."
                });
                await _scriptExecutor.ExecuteScriptsAsync(
                    plan.ExistingTargetConnectionString!,
                    plan.PreMigrationScripts,
                    progress, ct, plan.ScriptTimeoutSeconds);
            }

            // Step 5: Copy data (PG → PG via COPY protocol)
            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = 30,
                Message = "Starting data migration..."
            });

            var tableNames = snapshot.Tables.Select(t =>
                $"{PgIdentifierHelper.MapSchema(t.Schema)}.{t.TableName}").ToList();

            var rowsMigrated = await _bulkCopier.CopyDataAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                tableNames,
                progress,
                plan.BulkCopyTimeoutSeconds,
                ct);

            // Step 6: Reset sequences
            progress?.Report(new MigrationProgress
            {
                Phase = "SequenceReset",
                PercentComplete = 85,
                Message = "Resetting sequences..."
            });

            await _bulkCopier.ResetSequencesAsync(plan.ExistingTargetConnectionString!, ct);

            // Step 7: Execute post-migration scripts
            if (plan.PostMigrationScripts.Count > 0)
            {
                progress?.Report(new MigrationProgress
                {
                    Phase = "PostScripts",
                    PercentComplete = 90,
                    Message = "Running post-migration scripts on target..."
                });
                await _scriptExecutor.ExecuteScriptsAsync(
                    plan.ExistingTargetConnectionString!,
                    plan.PostMigrationScripts,
                    progress, ct, plan.ScriptTimeoutSeconds);
            }

            // Step 8: Validation
            progress?.Report(new MigrationProgress
            {
                Phase = "Validation",
                PercentComplete = 95,
                Message = "Running post-migration validation..."
            });

            var validationSummary = await _validationEngine.ValidateAsync(
                plan.SourceConnectionString!,
                plan.ExistingTargetConnectionString!,
                tableNames,
                ct);

            result.RowsMigrated = rowsMigrated;
            result.Validations = validationSummary.Results;
            result.Success = validationSummary.AllPassed;
            result.CompletedAt = DateTime.UtcNow;

            // Extension warnings don't fail migration but are recorded
            var warningMessages = extResult.Warnings
                .Where(w => w.Severity != ExtensionWarningSeverity.Error)
                .Select(w => $"[Warning] {w.Message}")
                .ToList();
            if (warningMessages.Count > 0)
                result.Errors.AddRange(warningMessages);
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
    /// ContinuousSync for PG→PG will use logical replication (Wave 4, Issue #34).
    /// Not yet implemented.
    /// </summary>
    public Task StartContinuousSyncAsync(
        MigrationPlan plan,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Continuous sync for PostgreSQL → PostgreSQL is not yet implemented. It will use logical replication (Issue #34).");
    }

    public Task<MigrationResult> CompleteCutoverAsync(Guid migrationId, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "CompleteCutover for PostgreSQL requires an active continuous sync session. Start with StartContinuousSyncAsync first.");
    }

    private static void ValidateConnectionStrings(MigrationPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.SourceConnectionString))
            throw new ArgumentException("SourceConnectionString is required.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.ExistingTargetConnectionString))
            throw new ArgumentException("ExistingTargetConnectionString is required.", nameof(plan));
    }
}
