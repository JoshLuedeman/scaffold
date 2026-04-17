using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Uses PostgreSQL logical replication (pgoutput protocol) to perform
/// continuous incremental sync from source PG to target Azure PG.
/// Flow: validate wal_level → create publication → create replication slot →
/// initial data load → stream changes → apply to target.
/// </summary>
public class LogicalReplicationSyncEngine : IAsyncDisposable
{
    private readonly PostgreSqlBulkCopier _bulkCopier;
    private readonly IProgress<MigrationProgress>? _progress;
    private readonly TimeSpan _pollInterval;
    private readonly ReplicationRetryPolicy? _retryPolicy;

    private string _sourceConnectionString = string.Empty;
    private string _targetConnectionString = string.Empty;
    private string _publicationName = string.Empty;
    private string _slotName = string.Empty;
    private List<string> _tableNames = [];
    private long _totalRowsSynced;
    private CancellationTokenSource? _syncCts;
    private Task? _replicationTask;
    private LogicalReplicationConnection? _replicationConnection;
    private PgOutputReplicationSlot? _preCreatedSlot;

    public LogicalReplicationSyncEngine(
        PostgreSqlBulkCopier bulkCopier,
        IProgress<MigrationProgress>? progress = null,
        TimeSpan? pollInterval = null,
        ReplicationRetryPolicy? retryPolicy = null)
    {
        _bulkCopier = bulkCopier ?? throw new ArgumentNullException(nameof(bulkCopier));
        _progress = progress;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _retryPolicy = retryPolicy;
    }

    public long TotalRowsSynced => _totalRowsSynced;
    public bool IsRunning => _replicationTask is { IsCompleted: false };

    /// <summary>
    /// Validates prerequisites, creates publication + slot, performs initial load, starts streaming.
    /// </summary>
    public async Task StartAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IReadOnlyList<string> tableNames,
        Guid migrationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
            throw new ArgumentException("Source connection string is required.", nameof(sourceConnectionString));
        if (string.IsNullOrWhiteSpace(targetConnectionString))
            throw new ArgumentException("Target connection string is required.", nameof(targetConnectionString));

        _sourceConnectionString = sourceConnectionString;
        _targetConnectionString = targetConnectionString;
        _tableNames = tableNames.ToList();
        _publicationName = GeneratePublicationName(migrationId);
        _slotName = GenerateSlotName(migrationId);

        // Step 1: Validate wal_level = logical
        await ValidateWalLevelAsync(ct);

        // Step 2: Ensure REPLICA IDENTITY is set for tables without PKs
        await EnsureReplicaIdentityAsync(ct);

        // Step 3: Create publication for specified tables
        await CreatePublicationAsync(ct);

        try
        {
            // Step 4: Create replication slot (captures consistent snapshot LSN)
            _replicationConnection = new LogicalReplicationConnection(_sourceConnectionString);
            await _replicationConnection.Open(ct);

            _preCreatedSlot = await _replicationConnection.CreatePgOutputReplicationSlot(
                _slotName, slotSnapshotInitMode: LogicalSlotSnapshotInitMode.Export, cancellationToken: ct);

            // Step 5: Initial data load via bulk copier (snapshot-consistent)
            _progress?.Report(new MigrationProgress
            {
                Phase = "InitialLoad",
                PercentComplete = 0,
                Message = "Performing initial data load..."
            });

            _totalRowsSynced = await _bulkCopier.CopyDataAsync(
                _sourceConnectionString, _targetConnectionString,
                _tableNames, _progress, ct: ct);

            await _bulkCopier.ResetSequencesAsync(_targetConnectionString, ct);

            // Step 6: Start streaming from the pre-created slot
            _syncCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _replicationTask = StreamChangesAsync(_syncCts.Token);
        }
        catch
        {
            // Cleanup on failure: dispose CTS, drop slot/publication
            if (_syncCts is not null)
            {
                _syncCts.Dispose();
                _syncCts = null;
            }

            if (_replicationConnection is not null)
            {
                await _replicationConnection.DisposeAsync();
                _replicationConnection = null;
            }

            await CleanupReplicationResourcesAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Stops replication, applies final changes, validates, returns result.
    /// </summary>
    public async Task<MigrationResult> CompleteCutoverAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        if (_syncCts is null)
        {
            throw new InvalidOperationException(
                "Cannot complete cutover before starting continuous sync. Call StartAsync first.");
        }

        // Stop the replication stream
        await _syncCts.CancelAsync();
        if (_replicationTask is not null)
        {
            try { await _replicationTask; }
            catch (OperationCanceledException) { }
        }

        // Cleanup publication and slot
        await CleanupReplicationResourcesAsync(ct);

        // Reset sequences one final time
        await _bulkCopier.ResetSequencesAsync(_targetConnectionString, ct);

        return new MigrationResult
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RowsMigrated = _totalRowsSynced,
            Success = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Validates that source PG has wal_level = logical.
    /// </summary>
    internal virtual async Task ValidateWalLevelAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SHOW wal_level", conn);
        var walLevel = (string)(await cmd.ExecuteScalarAsync(ct))!;
        if (!string.Equals(walLevel, "logical", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Source PostgreSQL wal_level is '{walLevel}', but 'logical' is required for replication. " +
                "Set wal_level = logical in postgresql.conf and restart the server.");
        }
    }

    /// <summary>
    /// Ensures tables without PKs have REPLICA IDENTITY FULL.
    /// </summary>
    internal virtual async Task EnsureReplicaIdentityAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        foreach (var table in _tableNames)
        {
            var pgName = PgIdentifierHelper.QuotePgName(table);
            // Check if table has a PK
            var parts = table.Split('.');
            var schema = parts.Length == 2 ? parts[0].Trim('"') : "public";
            var tableName = parts.Length == 2 ? parts[1].Trim('"') : parts[0].Trim('"');

            await using var checkCmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM pg_constraint c
                JOIN pg_class t ON c.conrelid = t.oid
                JOIN pg_namespace n ON t.relnamespace = n.oid
                WHERE c.contype = 'p' AND n.nspname = @schema AND t.relname = @table", conn);
            checkCmd.Parameters.AddWithValue("schema", schema);
            checkCmd.Parameters.AddWithValue("table", tableName);

            var pkCount = (long)(await checkCmd.ExecuteScalarAsync(ct))!;
            if (pkCount == 0)
            {
                // Set REPLICA IDENTITY FULL for tables without PKs
                await using var alterCmd = new NpgsqlCommand(
                    $"ALTER TABLE {pgName} REPLICA IDENTITY FULL", conn);
                await alterCmd.ExecuteNonQueryAsync(ct);
            }
        }
    }

    /// <summary>
    /// Creates a publication on the source for the specified tables.
    /// </summary>
    internal virtual async Task CreatePublicationAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        // Drop if exists (for re-runnable migrations)
        await using (var dropCmd = new NpgsqlCommand(
            $"DROP PUBLICATION IF EXISTS {PgIdentifierHelper.QuoteIdentifier(_publicationName)}", conn))
        {
            await dropCmd.ExecuteNonQueryAsync(ct);
        }

        var tableList = string.Join(", ", _tableNames.Select(PgIdentifierHelper.QuotePgName));
        await using var createCmd = new NpgsqlCommand(
            $"CREATE PUBLICATION {PgIdentifierHelper.QuoteIdentifier(_publicationName)} FOR TABLE {tableList}", conn);
        await createCmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Streams changes from the pre-created replication slot and applies them to target.
    /// Uses a single target connection for all apply operations.
    /// </summary>
    private async Task StreamChangesAsync(CancellationToken ct)
    {
        NpgsqlConnection? targetConn = null;
        try
        {
            // Open a single connection to the target for all apply operations
            targetConn = new NpgsqlConnection(_targetConnectionString);
            await targetConn.OpenAsync(ct);

            // Use the pre-created replication slot (already created in StartAsync)
            var slot = _preCreatedSlot
                ?? throw new InvalidOperationException("Replication slot was not pre-created. Call StartAsync first.");

            var options = new PgOutputReplicationOptions(_publicationName, protocolVersion: 1);

            await foreach (var message in _replicationConnection!.StartReplication(slot, options, ct))
            {
                ct.ThrowIfCancellationRequested();

                if (_retryPolicy is not null)
                {
                    await _retryPolicy.ExecuteAsync(
                        async token => await ApplyReplicationMessageAsync(message, targetConn, token), ct);
                }
                else
                {
                    await ApplyReplicationMessageAsync(message, targetConn, ct);
                }

                // Acknowledge the message
                _replicationConnection!.SetReplicationStatus(message.WalEnd);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            if (targetConn is not null)
            {
                await targetConn.DisposeAsync();
            }

            if (_replicationConnection is not null)
            {
                await _replicationConnection.DisposeAsync();
                _replicationConnection = null;
            }
        }
    }

    /// <summary>
    /// Applies a single replication message to the target database.
    /// </summary>
    internal virtual async Task ApplyReplicationMessageAsync(
        PgOutputReplicationMessage message,
        NpgsqlConnection targetConn,
        CancellationToken ct)
    {
        switch (message)
        {
            case InsertMessage insert:
                await ApplyInsertAsync(insert, targetConn, ct);
                break;
            case UpdateMessage update:
                await ApplyUpdateAsync(update, targetConn, ct);
                break;
            case KeyDeleteMessage keyDelete:
                await ApplyKeyDeleteAsync(keyDelete, targetConn, ct);
                break;
            case FullDeleteMessage fullDelete:
                await ApplyFullDeleteAsync(fullDelete, targetConn, ct);
                break;
            // BeginMessage, CommitMessage, RelationMessage — no action needed for target
        }
    }

    private async Task ApplyInsertAsync(InsertMessage insert, NpgsqlConnection targetConn, CancellationToken ct)
    {
        var tableName = $"{insert.Relation.Namespace}.{insert.Relation.RelationName}";
        var pgTable = PgIdentifierHelper.QuotePgName(tableName);
        var columns = insert.Relation.Columns;
        var colNames = string.Join(", ",
            columns.Select(c => PgIdentifierHelper.QuoteIdentifier(c.ColumnName)));
        var paramNames = string.Join(", ",
            columns.Select((_, i) => $"@p{i}"));

        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO {pgTable} ({colNames}) VALUES ({paramNames}) ON CONFLICT DO NOTHING", targetConn);

        int i = 0;
        await foreach (var value in insert.NewRow)
        {
            var val = value.IsDBNull ? DBNull.Value : await value.Get<object>(ct);
            cmd.Parameters.AddWithValue($"p{i}", val);
            i++;
        }

        await cmd.ExecuteNonQueryAsync(ct);
        Interlocked.Increment(ref _totalRowsSynced);
    }

    private async Task ApplyUpdateAsync(UpdateMessage update, NpgsqlConnection targetConn, CancellationToken ct)
    {
        var tableName = $"{update.Relation.Namespace}.{update.Relation.RelationName}";
        var pgTable = PgIdentifierHelper.QuotePgName(tableName);
        var columns = update.Relation.Columns;
        var colNames = string.Join(", ",
            columns.Select(c => PgIdentifierHelper.QuoteIdentifier(c.ColumnName)));
        var paramNames = string.Join(", ",
            columns.Select((_, i) => $"@p{i}"));

        // Find PK columns (PartOfKey flag in pgoutput)
        var pkColumns = columns
            .Where(c => c.Flags.HasFlag(RelationMessage.Column.ColumnFlags.PartOfKey))
            .Select(c => PgIdentifierHelper.QuoteIdentifier(c.ColumnName))
            .ToList();

        string sql;
        if (pkColumns.Count > 0)
        {
            var nonKeyColumns = columns
                .Where(c => !c.Flags.HasFlag(RelationMessage.Column.ColumnFlags.PartOfKey))
                .ToList();

            var updateSet = nonKeyColumns.Count > 0
                ? string.Join(", ", nonKeyColumns.Select(c =>
                    $"{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)} = EXCLUDED.{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)}"))
                : string.Join(", ", columns.Select(c =>
                    $"{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)} = EXCLUDED.{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)}"));

            sql = $"INSERT INTO {pgTable} ({colNames}) VALUES ({paramNames}) " +
                  $"ON CONFLICT ({string.Join(", ", pkColumns)}) DO UPDATE SET {updateSet}";
        }
        else
        {
            // No PK — just try insert, ignore conflicts
            sql = $"INSERT INTO {pgTable} ({colNames}) VALUES ({paramNames}) ON CONFLICT DO NOTHING";
        }

        await using var cmd = new NpgsqlCommand(sql, targetConn);

        int i = 0;
        await foreach (var value in update.NewRow)
        {
            var val = value.IsDBNull ? DBNull.Value : await value.Get<object>(ct);
            cmd.Parameters.AddWithValue($"p{i}", val);
            i++;
        }

        await cmd.ExecuteNonQueryAsync(ct);
        Interlocked.Increment(ref _totalRowsSynced);
    }

    private async Task ApplyKeyDeleteAsync(KeyDeleteMessage delete, NpgsqlConnection targetConn, CancellationToken ct)
    {
        var tableName = $"{delete.Relation.Namespace}.{delete.Relation.RelationName}";
        var pgTable = PgIdentifierHelper.QuotePgName(tableName);
        var columns = delete.Relation.Columns;

        // Use key columns for WHERE clause
        var keyColumns = columns
            .Where(c => c.Flags.HasFlag(RelationMessage.Column.ColumnFlags.PartOfKey))
            .ToList();

        if (keyColumns.Count == 0) return; // Can't delete without key identity

        var whereClauses = keyColumns.Select((c, i) =>
            $"{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)} IS NOT DISTINCT FROM @p{i}");
        var sql = $"DELETE FROM {pgTable} WHERE {string.Join(" AND ", whereClauses)}";

        await using var cmd = new NpgsqlCommand(sql, targetConn);

        int i = 0;
        await foreach (var value in delete.Key)
        {
            var val = value.IsDBNull ? DBNull.Value : await value.Get<object>(ct);
            cmd.Parameters.AddWithValue($"p{i}", val);
            i++;
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task ApplyFullDeleteAsync(FullDeleteMessage delete, NpgsqlConnection targetConn, CancellationToken ct)
    {
        var tableName = $"{delete.Relation.Namespace}.{delete.Relation.RelationName}";
        var pgTable = PgIdentifierHelper.QuotePgName(tableName);
        var columns = delete.Relation.Columns;

        // Use all columns for WHERE clause (REPLICA IDENTITY FULL)
        var whereClauses = columns.Select((c, i) =>
            $"{PgIdentifierHelper.QuoteIdentifier(c.ColumnName)} IS NOT DISTINCT FROM @p{i}");
        var sql = $"DELETE FROM {pgTable} WHERE {string.Join(" AND ", whereClauses)}";

        await using var cmd = new NpgsqlCommand(sql, targetConn);

        int i = 0;
        await foreach (var value in delete.OldRow)
        {
            var val = value.IsDBNull ? DBNull.Value : await value.Get<object>(ct);
            cmd.Parameters.AddWithValue($"p{i}", val);
            i++;
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Cleans up replication slot and publication on the source database.
    /// </summary>
    internal virtual async Task CleanupReplicationResourcesAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_sourceConnectionString);
        await conn.OpenAsync(ct);

        // Drop replication slot (parameterized to prevent SQL injection)
        try
        {
            await using var dropSlotCmd = new NpgsqlCommand(
                "SELECT pg_drop_replication_slot(@slot)", conn);
            dropSlotCmd.Parameters.AddWithValue("slot", _slotName);
            await dropSlotCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42704") // slot does not exist
        {
            // Already cleaned up — safe to ignore
        }

        // Drop publication
        try
        {
            await using var dropPubCmd = new NpgsqlCommand(
                $"DROP PUBLICATION IF EXISTS {PgIdentifierHelper.QuoteIdentifier(_publicationName)}", conn);
            await dropPubCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException)
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Generates a replication slot name from a migration ID.
    /// PG slot names: lowercase, max 63 chars, [a-z0-9_].
    /// </summary>
    internal static string GenerateSlotName(Guid migrationId)
    {
        var raw = $"scaffold_{migrationId:N}";
        return raw.Length > 63 ? raw[..63] : raw;
    }

    /// <summary>
    /// Generates a publication name from a migration ID.
    /// PG identifiers: max 63 chars.
    /// </summary>
    internal static string GeneratePublicationName(Guid migrationId)
    {
        var raw = $"scaffold_pub_{migrationId:N}";
        return raw.Length > 63 ? raw[..63] : raw;
    }

    /// <summary>
    /// Disposes replication resources: cancels streaming, awaits completion,
    /// disposes connection, and cleans up slot/publication.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_syncCts is not null)
        {
            try { await _syncCts.CancelAsync(); }
            catch { /* best effort */ }
        }

        if (_replicationTask is not null)
        {
            try { await _replicationTask; }
            catch (OperationCanceledException) { }
            catch { /* best effort */ }
        }

        if (_replicationConnection is not null)
        {
            await _replicationConnection.DisposeAsync();
            _replicationConnection = null;
        }

        if (_syncCts is not null)
        {
            _syncCts.Dispose();
            _syncCts = null;
        }

        try
        {
            await CleanupReplicationResourcesAsync(CancellationToken.None);
        }
        catch
        {
            // Best effort cleanup during dispose
        }

        GC.SuppressFinalize(this);
    }
}