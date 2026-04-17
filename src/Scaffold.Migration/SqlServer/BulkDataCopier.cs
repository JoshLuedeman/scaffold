using Microsoft.Data.SqlClient;
using Scaffold.Core.Interfaces;

namespace Scaffold.Migration.SqlServer;

/// <summary>
/// Copies table data from source to target SQL Server using SqlBulkCopy,
/// respecting foreign key dependency order.
/// </summary>
public class BulkDataCopier
{
    private const int BatchSize = 10_000;
    private const int BulkCopyTimeoutSeconds = 600;
    private const int NotifyAfterRows = 10_000;

    /// <summary>
    /// Copies all rows for the specified tables from source to target,
    /// ordering tables by foreign key dependencies.
    /// </summary>
    public virtual async Task<long> CopyDataAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IReadOnlyList<string> tableNames,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default,
        int? bulkCopyTimeout = null)
    {
        var orderedTables = tableNames.Count > 0
            ? await GetOrderedTablesAsync(sourceConnectionString, tableNames, ct)
            : [];

        if (orderedTables.Count == 0)
        {
            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = 100,
                Message = "No tables to migrate."
            });
            return 0;
        }

        var effectiveTimeout = ClampTimeout(bulkCopyTimeout, BulkCopyTimeoutSeconds);
        long totalRowsCopied = 0;

        await using var targetConn = new SqlConnection(targetConnectionString);
        await targetConn.OpenAsync(ct);

        // Disable foreign key constraints on target for the duration of bulk load
        await ToggleConstraintsAsync(targetConn, orderedTables, disable: true, ct);

        try
        {
            for (var i = 0; i < orderedTables.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var table = orderedTables[i];
                var pct = (double)i / orderedTables.Count * 100;

                progress?.Report(new MigrationProgress
                {
                    Phase = "DataMigration",
                    PercentComplete = pct,
                    CurrentTable = table,
                    RowsProcessed = totalRowsCopied,
                    Message = $"Copying table {table} ({i + 1}/{orderedTables.Count})..."
                });

                var rowsCopied = await CopyTableAsync(
                    sourceConnectionString, targetConn, table, totalRowsCopied, orderedTables.Count, i, effectiveTimeout, progress, ct);

                totalRowsCopied += rowsCopied;
            }
        }
        finally
        {
            // Re-enable foreign key constraints
            await ToggleConstraintsAsync(targetConn, orderedTables, disable: false, ct);
        }

        progress?.Report(new MigrationProgress
        {
            Phase = "DataMigration",
            PercentComplete = 100,
            RowsProcessed = totalRowsCopied,
            Message = $"Data migration complete. {totalRowsCopied:N0} total rows copied."
        });

        return totalRowsCopied;
    }

    /// <summary>
    /// Queries sys.foreign_keys to build a dependency graph and returns tables
    /// in topological order (parents before children). Tables not present in
    /// <paramref name="tableNames"/> are excluded.
    /// </summary>
    internal static async Task<List<string>> GetOrderedTablesAsync(
        string connectionString,
        IReadOnlyList<string> tableNames,
        CancellationToken ct = default)
    {
        var tableSet = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);

        // dependsOn[child] = set of parent tables
        var dependsOn = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tableNames)
            dependsOn[t] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string query = """
            SELECT
                SCHEMA_NAME(fk.schema_id) + '.' + OBJECT_NAME(fk.parent_object_id)  AS ChildTable,
                SCHEMA_NAME(rt.schema_id) + '.' + rt.name                            AS ParentTable
            FROM sys.foreign_keys fk
            JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
            """;

        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var child = reader.GetString(0);
            var parent = reader.GetString(1);

            if (tableSet.Contains(child) && tableSet.Contains(parent) &&
                !string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
            {
                dependsOn[child].Add(parent);
            }
        }

        return TopologicalSort(tableNames, dependsOn);
    }

    private static async Task<long> CopyTableAsync(
        string sourceConnectionString,
        SqlConnection targetConn,
        string tableName,
        long runningTotal,
        int tableCount,
        int tableIndex,
        int bulkCopyTimeout,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        long rowsCopied = 0;

        await using var sourceConn = new SqlConnection(sourceConnectionString);
        await sourceConn.OpenAsync(ct);

        await using var cmd = new SqlCommand($"SELECT * FROM {QuoteName(tableName)}", sourceConn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        using var bulkCopy = new SqlBulkCopy(targetConn)
        {
            DestinationTableName = QuoteName(tableName),
            BatchSize = BatchSize,
            BulkCopyTimeout = bulkCopyTimeout,
            NotifyAfter = NotifyAfterRows,
            EnableStreaming = true
        };

        bulkCopy.SqlRowsCopied += (_, e) =>
        {
            rowsCopied = e.RowsCopied;
            var pct = ((double)tableIndex / tableCount + (1.0 / tableCount * 0.5)) * 100;

            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = pct,
                CurrentTable = tableName,
                RowsProcessed = runningTotal + rowsCopied,
                Message = $"Copying {tableName}: {rowsCopied:N0} rows so far..."
            });
        };

        await bulkCopy.WriteToServerAsync(reader, ct);

        // SqlRowsCopied may not fire for the final batch; get actual count
        rowsCopied = bulkCopy.RowsCopied;

        return rowsCopied;
    }

    private static async Task ToggleConstraintsAsync(
        SqlConnection connection,
        IReadOnlyList<string> tableNames,
        bool disable,
        CancellationToken ct)
    {
        var action = disable ? "NOCHECK" : "CHECK";

        foreach (var table in disable ? tableNames : tableNames.Reverse())
        {
            var sql = $"ALTER TABLE {QuoteName(table)} {action} CONSTRAINT ALL";
            await using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// Quotes a potentially schema-qualified table name for use in SQL.
    /// Input like "dbo.Users" becomes "[dbo].[Users]".
    /// </summary>
    internal static string QuoteName(string tableName)
    {
        var parts = tableName.Split('.');
        return string.Join(".", parts.Select(p => $"[{p.Trim('[', ']')}]"));
    }

    /// <summary>
    /// Pure topological sort (Kahn's algorithm). Parents before children.
    /// If a cycle is detected, remaining tables are appended at the end.
    /// </summary>
    internal static List<string> TopologicalSort(
        IReadOnlyList<string> tableNames,
        Dictionary<string, HashSet<string>> dependsOn)
    {
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tableNames) inDegree[t] = dependsOn[t].Count;

        var queue = new Queue<string>(tableNames.Where(t => inDegree[t] == 0));
        var ordered = new List<string>(tableNames.Count);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var (child, parents) in dependsOn)
            {
                if (parents.Remove(current))
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                        queue.Enqueue(child);
                }
            }
        }

        // If cycle detected, append remaining tables at the end
        foreach (var t in tableNames)
        {
            if (!ordered.Contains(t))
                ordered.Add(t);
        }

        return ordered;
    }

    /// <summary>
    /// Clamps a timeout value to the range [min, max], using defaultValue when value is null.
    /// </summary>
    internal static int ClampTimeout(int? value, int defaultValue, int min = 30, int max = 3600)
        => Math.Clamp(value ?? defaultValue, min, max);
}
