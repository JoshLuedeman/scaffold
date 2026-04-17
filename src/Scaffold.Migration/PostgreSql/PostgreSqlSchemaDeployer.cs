using Npgsql;
using Scaffold.Core.Interfaces;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Deploys translated DDL to a PostgreSQL target database.
/// Reads schema from a SQL Server source, translates to PostgreSQL DDL using
/// <see cref="SqlServerSchemaReader"/> and <see cref="DdlTranslator"/>,
/// then executes the DDL statements on the target PostgreSQL database
/// within a transaction.
/// </summary>
public class PostgreSqlSchemaDeployer
{
    private readonly SqlServerSchemaReader _schemaReader;
    private readonly DdlTranslator _ddlTranslator;

    public PostgreSqlSchemaDeployer(SqlServerSchemaReader schemaReader, DdlTranslator ddlTranslator)
    {
        _schemaReader = schemaReader ?? throw new ArgumentNullException(nameof(schemaReader));
        _ddlTranslator = ddlTranslator ?? throw new ArgumentNullException(nameof(ddlTranslator));
    }

    /// <summary>
    /// Reads schema from source SQL Server, translates to PG DDL, deploys to target PG.
    /// All DDL is executed within a single transaction; on failure the transaction is rolled back.
    /// </summary>
    /// <param name="sourceConnectionString">SQL Server source connection string.</param>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="includedTables">Optional filter: only deploy these tables.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public virtual async Task DeploySchemaAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IReadOnlyList<string>? includedTables = null,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaDeployment",
            PercentComplete = 0,
            Message = "Reading source schema..."
        });

        // 1. Read detailed schema from SQL Server source
        var tables = await _schemaReader.ReadSchemaAsync(sourceConnectionString, includedTables, ct);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaDeployment",
            PercentComplete = 10,
            Message = $"Read {tables.Count} table definitions"
        });

        // 2. Translate to PG DDL
        var ddlStatements = _ddlTranslator.TranslateSchema(tables);

        progress?.Report(new MigrationProgress
        {
            Phase = "SchemaDeployment",
            PercentComplete = 20,
            Message = $"Generated {ddlStatements.Count} DDL statements"
        });

        // 3. Collect unique schemas and create them first
        var schemas = tables
            .Select(t => DdlTranslator.MapSchema(t.Schema))
            .Distinct()
            .Where(s => s != "public")
            .ToList();

        // 4. Deploy to PG in a transaction
        await using var connection = new NpgsqlConnection(targetConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Create non-public schemas
            foreach (var schema in schemas)
            {
                var quotedSchema = DdlTranslator.QuoteIdentifier(schema);
                await using var cmd = new NpgsqlCommand(
                    $"CREATE SCHEMA IF NOT EXISTS {quotedSchema}", connection, transaction);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Execute DDL statements with progress
            for (int i = 0; i < ddlStatements.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var pct = 20 + (double)(i + 1) / ddlStatements.Count * 80;
                progress?.Report(new MigrationProgress
                {
                    Phase = "SchemaDeployment",
                    PercentComplete = pct,
                    Message = $"Executing DDL statement {i + 1}/{ddlStatements.Count}"
                });

                await using var cmd = new NpgsqlCommand(ddlStatements[i], connection, transaction);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            progress?.Report(new MigrationProgress
            {
                Phase = "SchemaDeployment",
                PercentComplete = 100,
                Message = "Schema deployment complete"
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            throw new InvalidOperationException(
                $"Schema deployment failed: {ex.Message}. All changes rolled back.", ex);
        }
    }

    /// <summary>
    /// Extracts unique non-public PG schemas from a list of table definitions.
    /// Visible for testing.
    /// </summary>
    internal static List<string> ExtractSchemas(IReadOnlyList<Models.TableDefinition> tables)
    {
        return tables
            .Select(t => DdlTranslator.MapSchema(t.Schema))
            .Distinct()
            .Where(s => s != "public")
            .ToList();
    }
}