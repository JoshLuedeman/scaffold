using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;
using Scaffold.Core.Interfaces;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Copies data from SQL Server to PostgreSQL using SqlDataReader and Npgsql COPY protocol.
/// Streams data row-by-row without buffering entire tables in memory.
/// Tables are processed in FK dependency order to maintain referential integrity.
/// </summary>
public class CrossPlatformBulkCopier
{
    private const int DefaultBatchSize = 10_000;
    private const int DefaultTimeoutSeconds = 600;

    /// <summary>
    /// Copies all rows for specified tables from SQL Server source to PostgreSQL target.
    /// Tables are ordered by FK dependencies (parents before children).
    /// </summary>
    /// <param name="sourceConnectionString">SQL Server source connection string.</param>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="tableNames">Tables to copy.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="bulkCopyTimeout">Optional timeout in seconds (clamped to 30-3600).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total rows copied across all tables.</returns>
    public virtual async Task<long> CopyDataAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IReadOnlyList<string> tableNames,
        IProgress<MigrationProgress>? progress = null,
        int? bulkCopyTimeout = null,
        CancellationToken ct = default)
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

        long totalRows = 0;
        var timeout = ClampTimeout(bulkCopyTimeout, DefaultTimeoutSeconds);

        for (int i = 0; i < orderedTables.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var table = orderedTables[i];

            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = (double)i / orderedTables.Count * 100,
                CurrentTable = table,
                Message = $"Copying table {table} ({i + 1}/{orderedTables.Count})"
            });

            var rowsCopied = await CopyTableAsync(
                sourceConnectionString, targetConnectionString, table,
                progress, i, orderedTables.Count, timeout, ct);
            totalRows += rowsCopied;
        }

        progress?.Report(new MigrationProgress
        {
            Phase = "DataMigration",
            PercentComplete = 100,
            RowsProcessed = totalRows,
            Message = $"Data migration complete: {totalRows:N0} rows across {orderedTables.Count} tables"
        });

        return totalRows;
    }

    /// <summary>
    /// Copies a single table from SQL Server to PostgreSQL using streaming binary COPY.
    /// Disables triggers during import and re-enables them after.
    /// </summary>
    private static async Task<long> CopyTableAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string tableName,
        IProgress<MigrationProgress>? progress,
        int tableIndex, int tableCount,
        int timeout,
        CancellationToken ct)
    {
        long rowCount = 0;

        // 1. Read column metadata from source
        await using var sourceConn = new SqlConnection(sourceConnectionString);
        await sourceConn.OpenAsync(ct);

        var quotedSqlName = QuoteSqlName(tableName);
        await using var cmd = new SqlCommand($"SELECT * FROM {quotedSqlName}", sourceConn);
        cmd.CommandTimeout = timeout;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        var sqlTypes = new string[fieldCount];

        for (int c = 0; c < fieldCount; c++)
        {
            columnNames[c] = reader.GetName(c);
            sqlTypes[c] = reader.GetDataTypeName(c);
        }

        // 2. Build PG COPY command
        var pgTableName = QuotePgName(tableName);
        var pgColumns = string.Join(", ", columnNames.Select(n => $"\"{n.Replace("\"", "\"\"")}\""));
        var copyCommand = $"COPY {pgTableName} ({pgColumns}) FROM STDIN (FORMAT BINARY)";

        // 3. Open PG connection and stream data within a transaction
        await using var targetConn = new NpgsqlConnection(targetConnectionString);
        await targetConn.OpenAsync(ct);
        await using var transaction = await targetConn.BeginTransactionAsync(ct);

        try
        {
            // Disable triggers to avoid FK violations during bulk load
            await using (var disableCmd = new NpgsqlCommand(
                $"ALTER TABLE {pgTableName} DISABLE TRIGGER ALL", targetConn, transaction))
            {
                disableCmd.CommandTimeout = timeout;
                await disableCmd.ExecuteNonQueryAsync(ct);
            }

            // Scope the writer so it is disposed before ENABLE TRIGGER ALL runs;
            // Npgsql keeps the connection in COPY state until the writer is disposed.
            await using (var writer = await targetConn.BeginBinaryImportAsync(copyCommand, ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    await writer.StartRowAsync(ct);

                    for (int c = 0; c < fieldCount; c++)
                    {
                        if (reader.IsDBNull(c))
                        {
                            await writer.WriteNullAsync(ct);
                        }
                        else
                        {
                            var value = ConvertValue(reader.GetValue(c), sqlTypes[c]);
                            var npgsqlType = MapToNpgsqlDbType(sqlTypes[c]);
                            await writer.WriteAsync(value!, npgsqlType, ct);
                        }
                    }

                    rowCount++;

                    if (rowCount % DefaultBatchSize == 0)
                    {
                        var pct = (double)tableIndex / tableCount * 100 + (1.0 / tableCount * 50);
                        progress?.Report(new MigrationProgress
                        {
                            Phase = "DataMigration",
                            PercentComplete = pct,
                            CurrentTable = tableName,
                            RowsProcessed = rowCount,
                            Message = $"Copying {tableName}: {rowCount:N0} rows so far..."
                        });
                    }
                }

                await writer.CompleteAsync(ct);
            }

            // Re-enable triggers within the same transaction
            await using (var enableCmd = new NpgsqlCommand(
                $"ALTER TABLE {pgTableName} ENABLE TRIGGER ALL", targetConn, transaction))
            {
                enableCmd.CommandTimeout = timeout;
                await enableCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return rowCount;
    }

    /// <summary>
    /// Gets tables ordered by FK dependencies using Kahn's algorithm.
    /// Parent tables come before child tables that reference them.
    /// </summary>
    public virtual async Task<List<string>> GetOrderedTablesAsync(
        string connectionString,
        IReadOnlyList<string> tableNames,
        CancellationToken ct)
    {
        var tableSet = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Maps a SQL Server column type to the appropriate NpgsqlDbType for binary COPY.
    /// </summary>
    public static NpgsqlDbType MapToNpgsqlDbType(string sqlServerType)
    {
        var normalized = sqlServerType.Trim().ToLowerInvariant();

        return normalized switch
        {
            "int" or "integer" => NpgsqlDbType.Integer,
            "bigint" => NpgsqlDbType.Bigint,
            "smallint" => NpgsqlDbType.Smallint,
            "tinyint" => NpgsqlDbType.Smallint,
            "bit" or "boolean" => NpgsqlDbType.Boolean,
            "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" => NpgsqlDbType.Text,
            "datetime" or "datetime2" or "smalldatetime" => NpgsqlDbType.Timestamp,
            "datetimeoffset" => NpgsqlDbType.TimestampTz,
            "decimal" or "numeric" or "money" or "smallmoney" => NpgsqlDbType.Numeric,
            "float" => NpgsqlDbType.Double,
            "real" => NpgsqlDbType.Real,
            "uniqueidentifier" => NpgsqlDbType.Uuid,
            "varbinary" or "binary" or "image" or "timestamp" or "rowversion" => NpgsqlDbType.Bytea,
            "xml" => NpgsqlDbType.Xml,
            "date" => NpgsqlDbType.Date,
            "time" => NpgsqlDbType.Time,
            _ => NpgsqlDbType.Text  // Fallback for unknown types
        };
    }

    /// <summary>
    /// Converts a value read from SQL Server to the appropriate .NET type for PostgreSQL.
    /// </summary>
    public static object? ConvertValue(object? value, string sqlServerType)
    {
        if (value is null or DBNull) return null;

        var normalized = sqlServerType.Trim().ToLowerInvariant();

        return normalized switch
        {
            // tinyint (byte) → smallint (short) since PG has no single-byte integer
            "tinyint" when value is byte b => (short)b,
            // bit → bool (SQL Server may return as bool already, but ensure)
            "bit" when value is bool => value,
            "bit" when value is int i => i != 0,
            "bit" when value is byte b => b != 0,
            // Most types pass through directly
            _ => value
        };
    }

    /// <summary>
    /// Quotes a table name for PostgreSQL: dbo.Users → "public"."Users".
    /// Maps "dbo" schema to "public". Escapes embedded double-quotes.
    /// </summary>
    public static string QuotePgName(string tableName)
    {
        var parts = tableName.Split('.');
        if (parts.Length == 2 &&
            parts[0].Trim('[', ']', '"').Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = "public";
        }

        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']', '"');
            return $"\"{clean.Replace("\"", "\"\"")}\"";
        }));
    }

    /// <summary>
    /// Quotes a table name for SQL Server: dbo.Users → [dbo].[Users].
    /// Escapes embedded ']' characters by doubling them to prevent SQL injection.
    /// </summary>
    public static string QuoteSqlName(string tableName)
    {
        var parts = tableName.Split('.');
        return string.Join(".", parts.Select(p =>
        {
            var clean = p.Trim('[', ']');
            return $"[{clean.Replace("]", "]]")}]";
        }));
    }

    /// <summary>
    /// Clamps a timeout value to [30, 3600], using defaultValue when value is null.
    /// </summary>
    internal static int ClampTimeout(int? value, int defaultValue)
        => Math.Clamp(value ?? defaultValue, 30, 3600);

    /// <summary>
    /// Topological sort (Kahn's algorithm). Parents before children.
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

        // Append remaining (circular dependencies) in original order
        foreach (var t in tableNames)
        {
            if (!ordered.Contains(t))
                ordered.Add(t);
        }

        return ordered;
    }
}