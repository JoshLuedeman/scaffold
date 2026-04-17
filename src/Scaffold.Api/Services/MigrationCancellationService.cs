using System.Collections.Concurrent;

namespace Scaffold.Api.Services;

/// <summary>
/// Manages cancellation tokens for active migrations.
/// Registered as singleton so all scopes share the same token store.
/// </summary>
public class MigrationCancellationService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    /// <summary>
    /// Creates and registers a CancellationTokenSource for a migration.
    /// Returns the linked token.
    /// </summary>
    public CancellationToken Register(Guid migrationId)
    {
        var cts = new CancellationTokenSource();
        _tokens[migrationId] = cts;
        return cts.Token;
    }

    /// <summary>
    /// Cancels the migration if it is registered and not already cancelled.
    /// Returns true if cancellation was requested, false if not found.
    /// </summary>
    public bool Cancel(Guid migrationId)
    {
        if (_tokens.TryRemove(migrationId, out var cts))
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
            cts.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes the registration for a completed migration.
    /// Should be called in finally blocks after migration completes.
    /// </summary>
    public void Unregister(Guid migrationId)
    {
        if (_tokens.TryRemove(migrationId, out var cts))
            cts.Dispose();
    }

    /// <summary>
    /// Returns true if the migration is currently registered (active).
    /// </summary>
    public bool IsActive(Guid migrationId) => _tokens.ContainsKey(migrationId);
}
