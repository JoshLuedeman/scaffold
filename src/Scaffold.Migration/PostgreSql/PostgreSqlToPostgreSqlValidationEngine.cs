using Npgsql;
using Scaffold.Core.Models;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Post-migration validation for PostgreSQL → PostgreSQL migrations.
/// Compares row counts between source and target PostgreSQL databases.
/// Unlike <see cref="PostgreSqlValidationEngine"/> which uses SQL Server as the source,
/// this engine uses Npgsql for both source and target connections.
/// </summary>
public class PostgreSqlToPostgreSqlValidationEngine
{
    /// <summary>
    /// Validates all specified tables by comparing source and target PostgreSQL row counts.
    /// </summary>
    /// <param name="sourceConnectionString">PostgreSQL source connection string.</param>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="tableNames">Tables to validate (schema.table format).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of validation results across all tables.</returns>
    public virtual async Task<ValidationSummary> ValidateAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IReadOnlyList<string> tableNames,
        CancellationToken ct = default)
    {
        var results = new List<ValidationResult>();

        foreach (var table in tableNames)
        {
            ct.ThrowIfCancellationRequested();

            var sourceCount = await GetRowCountAsync(sourceConnectionString, table, ct);
            var targetCount = await GetRowCountAsync(targetConnectionString, table, ct);

            results.Add(new ValidationResult
            {
                TableName = table,
                SourceRowCount = sourceCount,
                TargetRowCount = targetCount,
                ChecksumMatch = sourceCount == targetCount
            });
        }

        return new ValidationSummary
        {
            Results = results,
            TablesValidated = results.Count,
            TablesPassed = results.Count(r => r.Passed),
            TablesFailed = results.Count(r => !r.Passed),
            AllPassed = results.TrueForAll(r => r.Passed)
        };
    }

    private static async Task<long> GetRowCountAsync(
        string connectionString, string tableName, CancellationToken ct)
    {
        var quoted = PgIdentifierHelper.QuotePgName(tableName);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quoted}", conn);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
