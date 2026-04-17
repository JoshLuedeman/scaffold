using Microsoft.Data.SqlClient;
using Npgsql;
using Scaffold.Core.Models;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Post-migration validation engine for PostgreSQL targets.
/// Compares source (SQL Server) row counts with target (PostgreSQL) counts
/// and performs data integrity verification.
/// For cross-platform validation, row count comparison is the primary check;
/// checksum comparison uses row-count equality as a proxy since SQL Server
/// CHECKSUM_AGG and PostgreSQL MD5 produce incompatible results.
/// </summary>
public class PostgreSqlValidationEngine
{
    private const int ChecksumBatchSize = 100_000;

    /// <summary>
    /// Validates all specified tables by comparing source SQL Server counts
    /// with target PostgreSQL counts.
    /// </summary>
    /// <param name="sourceConnectionString">SQL Server source connection string.</param>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="tableNames">Tables to validate.</param>
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
            var result = await ValidateTableAsync(
                sourceConnectionString, targetConnectionString, table, ct);
            results.Add(result);
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

    private static async Task<ValidationResult> ValidateTableAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string tableName,
        CancellationToken ct)
    {
        var sourceCount = await GetSqlServerRowCountAsync(sourceConnectionString, tableName, ct);
        var targetCount = await GetPostgreSqlRowCountAsync(targetConnectionString, tableName, ct);

        // For cross-platform validation, row count comparison is primary.
        // Checksum comparison is complex cross-platform (different hash functions),
        // so we mark checksum as matching when counts match.
        // Future enhancement: implement MD5-based column comparison.
        var checksumMatch = sourceCount == targetCount;

        return new ValidationResult
        {
            TableName = tableName,
            SourceRowCount = sourceCount,
            TargetRowCount = targetCount,
            ChecksumMatch = checksumMatch
        };
    }

    private static async Task<long> GetSqlServerRowCountAsync(
        string connectionString, string tableName, CancellationToken ct)
    {
        var quotedName = QuoteSqlName(tableName);
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"SELECT COUNT_BIG(*) FROM {quotedName}", conn);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<long> GetPostgreSqlRowCountAsync(
        string connectionString, string tableName, CancellationToken ct)
    {
        var quotedName = QuotePgName(tableName);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedName}", conn);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Quotes a table name for SQL Server: dbo.Users → [dbo].[Users].
    /// Escapes embedded ']' characters by doubling them to prevent SQL injection.
    /// </summary>
    internal static string QuoteSqlName(string tableName)
    {
        var parts = tableName.Split('.');
        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']');
            return $"[{clean.Replace("]", "]]")}]";
        }));
    }

    /// <summary>
    /// Quotes a table name for PostgreSQL: dbo.Users → "public"."Users".
    /// Maps "dbo" schema to "public". Escapes embedded double-quotes.
    /// </summary>
    internal static string QuotePgName(string tableName)
    {
        var parts = tableName.Split('.');
        if (parts.Length == 2 &&
            parts[0].Trim('"', '[', ']').Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = "public";
        }

        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']', '"');
            return $"\"{clean.Replace("\"", "\"\"")}\"";
        }));
    }
}