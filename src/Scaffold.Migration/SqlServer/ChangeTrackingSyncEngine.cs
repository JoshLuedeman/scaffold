using Microsoft.Data.SqlClient;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Migration.SqlServer;

/// <summary>
/// Uses SQL Server Change Tracking to perform initial data load followed by
/// continuous incremental replication from source to target.
/// </summary>
public class ChangeTrackingSyncEngine
{
    private readonly string _sourceConnectionString;
    private readonly string _targetConnectionString;
    private readonly IProgress<MigrationProgress>? _progress;
    private readonly TimeSpan _pollInterval;

    private long _currentVersion;
    private long _totalRowsSynced;
    private CancellationTokenSource? _syncCts;
    private Task? _syncLoopTask;
    private List<TableInfo> _trackedTables = [];

    public ChangeTrackingSyncEngine(
        string sourceConnectionString,
        string targetConnectionString,
        IProgress<MigrationProgress>? progress = null,
        TimeSpan? pollInterval = null)
    {
        _sourceConnectionString = sourceConnectionString;
        _targetConnectionString = targetConnectionString;
        _progress = progress;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    public long CurrentVersion => _currentVersion;
    public long TotalRowsSynced => _totalRowsSynced;
    public bool IsRunning => _syncLoopTask is { IsCompleted: false };

    /// <summary>
    /// Enables Change Tracking, performs initial load, and starts the continuous sync loop.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _trackedTables = await GetUserTablesAsync(ct);

        await EnableChangeTrackingAsync(ct);

        await PerformInitialLoadAsync(ct);

        _currentVersion = await GetCurrentVersionAsync(ct);

        _syncCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _syncLoopTask = RunSyncLoopAsync(_syncCts.Token);
    }

    /// <summary>
    /// Stops the sync loop, performs a final sync, validates row counts,
    /// and returns a MigrationResult.
    /// </summary>
    public async Task<MigrationResult> CompleteCutoverAsync(Guid projectId, CancellationToken ct = default)
    {
        var result = new MigrationResult
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Stop the continuous sync loop
            if (_syncCts is not null)
            {
                await _syncCts.CancelAsync();
            }

            if (_syncLoopTask is not null)
            {
                try { await _syncLoopTask; }
                catch (OperationCanceledException) { /* expected */ }
            }

            // Final sync pass to drain remaining changes
            _progress?.Report(new MigrationProgress
            {
                Phase = "FinalSync",
                PercentComplete = 90,
                Message = "Performing final sync pass..."
            });

            await SyncChangesAsync(ct);

            // Validate row counts
            _progress?.Report(new MigrationProgress
            {
                Phase = "Validation",
                PercentComplete = 95,
                Message = "Validating source and target row counts..."
            });

            foreach (var table in _trackedTables)
            {
                var sourceCount = await GetRowCountAsync(_sourceConnectionString, table, ct);
                var targetCount = await GetRowCountAsync(_targetConnectionString, table, ct);

                result.Validations.Add(new ValidationResult
                {
                    TableName = $"{table.Schema}.{table.Name}",
                    SourceRowCount = sourceCount,
                    TargetRowCount = targetCount,
                    ChecksumMatch = sourceCount == targetCount
                });
            }

            result.RowsMigrated = _totalRowsSynced;
            result.Success = result.Validations.TrueForAll(v => v.Passed);
            result.CompletedAt = DateTime.UtcNow;

            _progress?.Report(new MigrationProgress
            {
                Phase = "Complete",
                PercentComplete = 100,
                RowsProcessed = _totalRowsSynced,
                Message = result.Success
                    ? "Cutover completed successfully."
                    : "Cutover completed with validation warnings."
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    #region Setup

    private async Task EnableChangeTrackingAsync(CancellationToken ct)
    {
        _progress?.Report(new MigrationProgress
        {
            Phase = "ChangeTrackingSetup",
            PercentComplete = 0,
            Message = "Enabling Change Tracking on source database..."
        });

        await using var conn = new SqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        var dbName = conn.Database;

        // Enable at database level (idempotent check)
        var checkDbSql = """
            SELECT COUNT(1) FROM sys.change_tracking_databases
            WHERE database_id = DB_ID()
            """;

        await using (var checkCmd = new SqlCommand(checkDbSql, conn))
        {
            var enabled = (int)(await checkCmd.ExecuteScalarAsync(ct))! > 0;
            if (!enabled)
            {
                var enableSql = $"""
                    ALTER DATABASE [{dbName}]
                    SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON)
                    """;
                await using var cmd = new SqlCommand(enableSql, conn);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        // Enable on each table
        foreach (var table in _trackedTables)
        {
            var checkTableSql = """
                SELECT COUNT(1) FROM sys.change_tracking_tables
                WHERE object_id = OBJECT_ID(@tableName)
                """;

            await using var checkCmd = new SqlCommand(checkTableSql, conn);
            checkCmd.Parameters.AddWithValue("@tableName", $"{table.Schema}.{table.Name}");

            var tableEnabled = (int)(await checkCmd.ExecuteScalarAsync(ct))! > 0;
            if (!tableEnabled)
            {
                var enableTableSql = $"ALTER TABLE [{table.Schema}].[{table.Name}] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON)";
                await using var cmd = new SqlCommand(enableTableSql, conn);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            _progress?.Report(new MigrationProgress
            {
                Phase = "ChangeTrackingSetup",
                CurrentTable = $"{table.Schema}.{table.Name}",
                Message = $"Change Tracking enabled on {table.Schema}.{table.Name}"
            });
        }
    }

    #endregion

    #region Initial Load

    private async Task PerformInitialLoadAsync(CancellationToken ct)
    {
        _progress?.Report(new MigrationProgress
        {
            Phase = "InitialLoad",
            PercentComplete = 10,
            Message = "Starting initial data load..."
        });

        for (var i = 0; i < _trackedTables.Count; i++)
        {
            var table = _trackedTables[i];
            var qualifiedName = $"[{table.Schema}].[{table.Name}]";

            _progress?.Report(new MigrationProgress
            {
                Phase = "InitialLoad",
                CurrentTable = $"{table.Schema}.{table.Name}",
                PercentComplete = 10 + (40.0 * i / _trackedTables.Count),
                Message = $"Copying {table.Schema}.{table.Name}..."
            });

            await using var sourceConn = new SqlConnection(_sourceConnectionString);
            await sourceConn.OpenAsync(ct);

            await using var reader = await new SqlCommand($"SELECT * FROM {qualifiedName}", sourceConn)
                .ExecuteReaderAsync(ct);

            await using var targetConn = new SqlConnection(_targetConnectionString);
            await targetConn.OpenAsync(ct);

            // Enable identity insert if table has an identity column
            var hasIdentity = await HasIdentityColumnAsync(targetConn, table, ct);
            if (hasIdentity)
            {
                await using var identCmd = new SqlCommand($"SET IDENTITY_INSERT {qualifiedName} ON", targetConn);
                await identCmd.ExecuteNonQueryAsync(ct);
            }

            using var bulkCopy = new SqlBulkCopy(targetConn)
            {
                DestinationTableName = qualifiedName,
                BatchSize = 10_000,
                BulkCopyTimeout = 600,
                EnableStreaming = true
            };

            // Map columns by name
            for (var col = 0; col < reader.FieldCount; col++)
            {
                var colName = reader.GetName(col);
                bulkCopy.ColumnMappings.Add(colName, colName);
            }

            await bulkCopy.WriteToServerAsync(reader, ct);
            _totalRowsSynced += bulkCopy.RowsCopied;

            if (hasIdentity)
            {
                await using var identOffCmd = new SqlCommand($"SET IDENTITY_INSERT {qualifiedName} OFF", targetConn);
                await identOffCmd.ExecuteNonQueryAsync(ct);
            }
        }

        _progress?.Report(new MigrationProgress
        {
            Phase = "InitialLoad",
            PercentComplete = 50,
            RowsProcessed = _totalRowsSynced,
            Message = $"Initial load complete. {_totalRowsSynced} rows copied."
        });
    }

    #endregion

    #region Continuous Sync

    private async Task RunSyncLoopAsync(CancellationToken ct)
    {
        _progress?.Report(new MigrationProgress
        {
            Phase = "ContinuousSync",
            PercentComplete = 50,
            Message = "Continuous sync loop started."
        });

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, ct);
                await SyncChangesAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _progress?.Report(new MigrationProgress
                {
                    Phase = "ContinuousSync",
                    Message = $"Sync error (will retry): {ex.Message}"
                });
            }
        }
    }

    private async Task SyncChangesAsync(CancellationToken ct)
    {
        var latestVersion = await GetCurrentVersionAsync(ct);
        if (latestVersion == _currentVersion)
            return;

        foreach (var table in _trackedTables)
        {
            var qualifiedName = $"[{table.Schema}].[{table.Name}]";

            var pkColumns = await GetPrimaryKeyColumnsAsync(table, ct);
            if (pkColumns.Count == 0) continue;

            var pkJoin = string.Join(" AND ", pkColumns.Select(pk => $"T.[{pk}] = CT.[{pk}]"));
            var pkMatch = string.Join(" AND ", pkColumns.Select(pk => $"TGT.[{pk}] = CT.[{pk}]"));

            await using var sourceConn = new SqlConnection(_sourceConnectionString);
            await sourceConn.OpenAsync(ct);

            // Get changes since last version
            var changesSql = $"""
                SELECT CT.SYS_CHANGE_OPERATION, CT.*
                FROM CHANGETABLE(CHANGES {qualifiedName}, @lastVersion) AS CT
                """;

            await using var changesCmd = new SqlCommand(changesSql, sourceConn);
            changesCmd.Parameters.AddWithValue("@lastVersion", _currentVersion);

            await using var changesReader = await changesCmd.ExecuteReaderAsync(ct);

            var inserts = new List<Dictionary<string, object?>>();
            var updates = new List<Dictionary<string, object?>>();
            var deletes = new List<Dictionary<string, object?>>();

            while (await changesReader.ReadAsync(ct))
            {
                var op = changesReader.GetString(0);
                var row = new Dictionary<string, object?>();
                for (var i = 1; i < changesReader.FieldCount; i++)
                {
                    var name = changesReader.GetName(i);
                    if (!name.StartsWith("SYS_CHANGE_", StringComparison.OrdinalIgnoreCase))
                    {
                        row[name] = changesReader.IsDBNull(i) ? null : changesReader.GetValue(i);
                    }
                }

                switch (op)
                {
                    case "I": inserts.Add(row); break;
                    case "U": updates.Add(row); break;
                    case "D": deletes.Add(row); break;
                }
            }

            await changesReader.CloseAsync();

            // For inserts/updates, fetch full rows from source
            await ApplyInsertsAsync(sourceConn, qualifiedName, table, pkColumns, inserts, ct);
            await ApplyUpdatesAsync(sourceConn, qualifiedName, table, pkColumns, updates, ct);
            await ApplyDeletesAsync(qualifiedName, pkColumns, deletes, ct);

            var changeCount = inserts.Count + updates.Count + deletes.Count;
            _totalRowsSynced += changeCount;
        }

        _currentVersion = latestVersion;

        _progress?.Report(new MigrationProgress
        {
            Phase = "ContinuousSync",
            RowsProcessed = _totalRowsSynced,
            Message = $"Synced to version {_currentVersion}. Total rows processed: {_totalRowsSynced}"
        });
    }

    private async Task ApplyInsertsAsync(
        SqlConnection sourceConn, string qualifiedName, TableInfo table,
        List<string> pkColumns, List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        foreach (var pkValues in rows)
        {
            var pkWhere = string.Join(" AND ", pkColumns.Select(pk => $"[{pk}] = @pk_{pk}"));
            var selectSql = $"SELECT * FROM {qualifiedName} WHERE {pkWhere}";

            await using var cmd = new SqlCommand(selectSql, sourceConn);
            foreach (var pk in pkColumns)
                cmd.Parameters.AddWithValue($"@pk_{pk}", pkValues[pk] ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) continue;

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var values = columns.Select((_, i) => reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i)).ToList();
            await reader.CloseAsync();

            var colList = string.Join(", ", columns.Select(c => $"[{c}]"));
            var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
            var insertSql = $"INSERT INTO {qualifiedName} ({colList}) VALUES ({paramList})";

            await using var targetConn = new SqlConnection(_targetConnectionString);
            await targetConn.OpenAsync(ct);

            var hasIdentity = await HasIdentityColumnAsync(targetConn, table, ct);
            if (hasIdentity)
            {
                await using var identCmd = new SqlCommand($"SET IDENTITY_INSERT {qualifiedName} ON", targetConn);
                await identCmd.ExecuteNonQueryAsync(ct);
            }

            await using var insertCmd = new SqlCommand(insertSql, targetConn);
            for (var i = 0; i < values.Count; i++)
                insertCmd.Parameters.AddWithValue($"@p{i}", values[i]);
            await insertCmd.ExecuteNonQueryAsync(ct);

            if (hasIdentity)
            {
                await using var identOffCmd = new SqlCommand($"SET IDENTITY_INSERT {qualifiedName} OFF", targetConn);
                await identOffCmd.ExecuteNonQueryAsync(ct);
            }
        }
    }

    private async Task ApplyUpdatesAsync(
        SqlConnection sourceConn, string qualifiedName, TableInfo table,
        List<string> pkColumns, List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        foreach (var pkValues in rows)
        {
            var pkWhere = string.Join(" AND ", pkColumns.Select(pk => $"[{pk}] = @pk_{pk}"));
            var selectSql = $"SELECT * FROM {qualifiedName} WHERE {pkWhere}";

            await using var cmd = new SqlCommand(selectSql, sourceConn);
            foreach (var pk in pkColumns)
                cmd.Parameters.AddWithValue($"@pk_{pk}", pkValues[pk] ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) continue;

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var values = columns.Select((_, i) => reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i)).ToList();
            await reader.CloseAsync();

            var nonPkColumns = columns.Where(c => !pkColumns.Contains(c)).ToList();
            if (nonPkColumns.Count == 0) continue;

            var setClause = string.Join(", ", nonPkColumns.Select((c, _) =>
            {
                var idx = columns.IndexOf(c);
                return $"[{c}] = @p{idx}";
            }));
            var whereClause = string.Join(" AND ", pkColumns.Select(pk =>
            {
                var idx = columns.IndexOf(pk);
                return $"[{pk}] = @p{idx}";
            }));

            var updateSql = $"UPDATE {qualifiedName} SET {setClause} WHERE {whereClause}";

            await using var targetConn = new SqlConnection(_targetConnectionString);
            await targetConn.OpenAsync(ct);

            await using var updateCmd = new SqlCommand(updateSql, targetConn);
            for (var i = 0; i < values.Count; i++)
                updateCmd.Parameters.AddWithValue($"@p{i}", values[i]);
            await updateCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task ApplyDeletesAsync(
        string qualifiedName, List<string> pkColumns,
        List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        await using var targetConn = new SqlConnection(_targetConnectionString);
        await targetConn.OpenAsync(ct);

        foreach (var pkValues in rows)
        {
            var whereClause = string.Join(" AND ", pkColumns.Select(pk => $"[{pk}] = @pk_{pk}"));
            var deleteSql = $"DELETE FROM {qualifiedName} WHERE {whereClause}";

            await using var cmd = new SqlCommand(deleteSql, targetConn);
            foreach (var pk in pkColumns)
                cmd.Parameters.AddWithValue($"@pk_{pk}", pkValues[pk] ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    #endregion

    #region Helpers

    private async Task<long> GetCurrentVersionAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT CHANGE_TRACKING_CURRENT_VERSION()", conn);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DBNull or null ? 0 : Convert.ToInt64(result);
    }

    private async Task<List<TableInfo>> GetUserTablesAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT s.name AS SchemaName, t.name AS TableName
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name
            """;

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tables = new List<TableInfo>();
        while (await reader.ReadAsync(ct))
        {
            tables.Add(new TableInfo
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return tables;
    }

    private async Task<List<string>> GetPrimaryKeyColumnsAsync(TableInfo table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT c.name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.is_primary_key = 1
              AND i.object_id = OBJECT_ID(@tableName)
            ORDER BY ic.key_ordinal
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", $"{table.Schema}.{table.Name}");
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var columns = new List<string>();
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(0));

        return columns;
    }

    private static async Task<bool> HasIdentityColumnAsync(SqlConnection conn, TableInfo table, CancellationToken ct)
    {
        var sql = """
            SELECT COUNT(1) FROM sys.identity_columns
            WHERE object_id = OBJECT_ID(@tableName)
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", $"{table.Schema}.{table.Name}");
        var result = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return result > 0;
    }

    private static async Task<long> GetRowCountAsync(string connectionString, TableInfo table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = $"SELECT COUNT_BIG(*) FROM [{table.Schema}].[{table.Name}]";
        await using var cmd = new SqlCommand(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    #endregion

    private sealed class TableInfo
    {
        public string Schema { get; init; } = "dbo";
        public string Name { get; init; } = string.Empty;
    }
}
