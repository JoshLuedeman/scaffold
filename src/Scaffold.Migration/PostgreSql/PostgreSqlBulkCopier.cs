using Npgsql;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.Shared;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Copies data between PostgreSQL databases using the COPY protocol.
/// Streams data without loading entire tables into memory.
/// Tables are processed in FK dependency order.
/// </summary>
public class PostgreSqlBulkCopier
{
    private const int DefaultTimeoutSeconds = 600;
    private const int ProgressReportInterval = 10_000;

    /// <summary>
    /// Copies all rows for specified tables from source PG to target PG.
    /// Tables are ordered by FK dependencies (parents before children).
    /// </summary>
    /// <param name="sourceConnectionString">PostgreSQL source connection string.</param>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="tableNames">Tables to copy (schema.table format).</param>
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
        var timeout = PgIdentifierHelper.ClampTimeout(bulkCopyTimeout, DefaultTimeoutSeconds);

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
        for (int i = 0; i < orderedTables.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var table = orderedTables[i];

            progress?.Report(new MigrationProgress
            {
                Phase = "DataMigration",
                PercentComplete = (double)i / orderedTables.Count * 100,
                CurrentTable = table,
                Message = $"Copying table {i + 1}/{orderedTables.Count}: {table}..."
            });

            var rowsCopied = await CopyTableAsync(
                sourceConnectionString, targetConnectionString,
                table, timeout, i, orderedTables.Count, progress, ct);
            totalRows += rowsCopied;
        }

        progress?.Report(new MigrationProgress
        {
            Phase = "DataMigration",
            PercentComplete = 100,
            RowsProcessed = totalRows,
            Message = $"Data migration complete: {totalRows:N0} rows copied across {orderedTables.Count} tables."
        });

        return totalRows;
    }

    /// <summary>
    /// Resets sequences on the target to match the max values after data copy.
    /// Should be called after all tables have been copied.
    /// </summary>
    /// <param name="targetConnectionString">PostgreSQL target connection string.</param>
    /// <param name="ct">Cancellation token.</param>
    public virtual async Task ResetSequencesAsync(
        string targetConnectionString,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(targetConnectionString);
        await conn.OpenAsync(ct);

        // Find all sequences owned by table columns (identity/serial columns)
        await using var cmd = new NpgsqlCommand(@"
            SELECT n.nspname || '.' || s.relname as seq_name,
                   dep_n.nspname || '.' || dep_t.relname as table_name,
                   a.attname as column_name
            FROM pg_class s
            JOIN pg_namespace n ON s.relnamespace = n.oid
            JOIN pg_depend d ON d.objid = s.oid AND d.deptype = 'a'
            JOIN pg_class dep_t ON d.refobjid = dep_t.oid
            JOIN pg_namespace dep_n ON dep_t.relnamespace = dep_n.oid
            JOIN pg_attribute a ON a.attrelid = d.refobjid AND a.attnum = d.refobjsubid
            WHERE s.relkind = 'S'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')", conn);

        var sequences = new List<(string SeqName, string TableName, string ColumnName)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                sequences.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var (seqName, tableName, columnName) in sequences)
        {
            ct.ThrowIfCancellationRequested();
            var quotedTable = PgIdentifierHelper.QuotePgName(tableName);
            var quotedCol = PgIdentifierHelper.QuoteIdentifier(columnName);
            var quotedSeq = PgIdentifierHelper.QuotePgName(seqName);

            // setval to max value, or 1 if table is empty
            await using var setCmd = new NpgsqlCommand(
                $"SELECT setval({quotedSeq}::text::regclass, COALESCE((SELECT MAX({quotedCol}) FROM {quotedTable}), 1), true)",
                conn);
            await setCmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// Copies a single table from source PG to target PG using text-mode COPY protocol.
    /// Disables triggers during import and re-enables them after.
    /// Truncates the target table before copying for re-runnable migrations.
    /// </summary>
    internal virtual async Task<long> CopyTableAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string tableName,
        int timeout,
        int tableIndex,
        int tableCount,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        var pgTableName = PgIdentifierHelper.QuotePgName(tableName);
        long rowCount = 0;

        // Get column names from source to build matching COPY commands
        var columnNames = await GetColumnNamesAsync(sourceConnectionString, tableName, ct);
        var pgColumns = string.Join(", ", columnNames.Select(PgIdentifierHelper.QuoteIdentifier));

        // Use text mode COPY (safe across PG versions)
        var exportCmd = $"COPY {pgTableName} ({pgColumns}) TO STDOUT";
        var importCmd = $"COPY {pgTableName} ({pgColumns}) FROM STDIN";

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

            // TRUNCATE target table (for re-runnable migrations)
            await using (var truncCmd = new NpgsqlCommand(
                $"TRUNCATE TABLE {pgTableName} CASCADE", targetConn, transaction))
            {
                truncCmd.CommandTimeout = timeout;
                await truncCmd.ExecuteNonQueryAsync(ct);
            }

            // Stream data: source COPY TO → target COPY FROM
            await using var sourceConn = new NpgsqlConnection(sourceConnectionString);
            await sourceConn.OpenAsync(ct);

            // Scope the reader/writer so they are disposed before ENABLE TRIGGER ALL;
            // Npgsql keeps the connection in COPY state until the reader/writer is disposed.
            // TextReader/TextWriter don't implement IAsyncDisposable, so use synchronous using.
            using (var exportReader = await sourceConn.BeginTextExportAsync(exportCmd, ct))
            await using (var importWriter = await targetConn.BeginTextImportAsync(importCmd, ct))
            {
                // Stream character data in chunks
                var buffer = new char[65536];
                int charsRead;
                while ((charsRead = await exportReader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await importWriter.WriteAsync(buffer, 0, charsRead);

                    // Estimate rows from newlines for progress reporting
                    for (int i = 0; i < charsRead; i++)
                    {
                        if (buffer[i] == '\n') rowCount++;
                    }

                    if (rowCount % ProgressReportInterval < 100)
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
    /// Gets column names for a table from the source PG database.
    /// Excludes generated columns (PG won't allow writing to them via COPY).
    /// </summary>
    internal static async Task<List<string>> GetColumnNamesAsync(
        string connectionString, string tableName, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var parts = tableName.Split('.');
        var schema = parts.Length == 2 ? parts[0].Trim('"', '[', ']') : "public";
        var table = parts.Length == 2 ? parts[1].Trim('"', '[', ']') : parts[0].Trim('"', '[', ']');

        await using var cmd = new NpgsqlCommand(@"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
              AND is_generated = 'NEVER'
              AND (generation_expression IS NULL OR generation_expression = '')
            ORDER BY ordinal_position", conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>
    /// Gets tables ordered by FK dependencies (parents first) from the PG source.
    /// Uses pg_constraint to discover foreign key relationships, then applies
    /// topological sort via Kahn's algorithm.
    /// </summary>
    internal virtual async Task<List<string>> GetOrderedTablesAsync(
        string connectionString,
        IReadOnlyList<string> tableNames,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Build lookup of all FK dependencies from pg_constraint
        var dependsOn = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tableNames)
            dependsOn[t] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                cn.nspname || '.' || c.relname as child_table,
                pn.nspname || '.' || p.relname as parent_table
            FROM pg_constraint con
            JOIN pg_class c ON con.conrelid = c.oid
            JOIN pg_namespace cn ON c.relnamespace = cn.oid
            JOIN pg_class p ON con.confrelid = p.oid
            JOIN pg_namespace pn ON p.relnamespace = pn.oid
            WHERE con.contype = 'f'
              AND cn.nspname NOT IN ('pg_catalog', 'information_schema')", conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var child = reader.GetString(0);
            var parent = reader.GetString(1);

            if (dependsOn.ContainsKey(child) && dependsOn.ContainsKey(parent)
                && !string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
            {
                dependsOn[child].Add(parent);
            }
        }

        return TopologicalSorter.Sort(tableNames.ToList(), dependsOn);
    }

    /// <summary>
    /// Builds the COPY export command for a table with quoted column names.
    /// Exposed for testing.
    /// </summary>
    internal static string BuildCopyExportCommand(string tableName, IReadOnlyList<string> columnNames)
    {
        var pgTableName = PgIdentifierHelper.QuotePgName(tableName);
        var pgColumns = string.Join(", ", columnNames.Select(PgIdentifierHelper.QuoteIdentifier));
        return $"COPY {pgTableName} ({pgColumns}) TO STDOUT";
    }

    /// <summary>
    /// Builds the COPY import command for a table with quoted column names.
    /// Exposed for testing.
    /// </summary>
    internal static string BuildCopyImportCommand(string tableName, IReadOnlyList<string> columnNames)
    {
        var pgTableName = PgIdentifierHelper.QuotePgName(tableName);
        var pgColumns = string.Join(", ", columnNames.Select(PgIdentifierHelper.QuoteIdentifier));
        return $"COPY {pgTableName} ({pgColumns}) FROM STDIN";
    }

    /// <summary>
    /// Builds the SQL command to reset a sequence to the max value of its owning column.
    /// Exposed for testing.
    /// </summary>
    internal static string BuildResetSequenceSql(string seqName, string tableName, string columnName)
    {
        var quotedTable = PgIdentifierHelper.QuotePgName(tableName);
        var quotedCol = PgIdentifierHelper.QuoteIdentifier(columnName);
        var quotedSeq = PgIdentifierHelper.QuotePgName(seqName);
        return $"SELECT setval({quotedSeq}::text::regclass, COALESCE((SELECT MAX({quotedCol}) FROM {quotedTable}), 1), true)";
    }
}
