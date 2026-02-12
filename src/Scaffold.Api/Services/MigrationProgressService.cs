using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Hubs;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Services;

public class MigrationProgressService : IProgress<MigrationProgress>
{
    private readonly IHubContext<MigrationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private string _migrationId = string.Empty;

    public MigrationProgressService(IHubContext<MigrationHub> hubContext, IServiceScopeFactory scopeFactory)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    public void SetMigrationId(string migrationId)
    {
        _migrationId = migrationId;
    }

    public void Report(MigrationProgress value)
    {
        if (string.IsNullOrEmpty(_migrationId))
            return;

        _hubContext.Clients.Group(_migrationId)
            .SendAsync("MigrationProgress", value)
            .ConfigureAwait(false);

        // Persist to DB on a background thread to avoid blocking the migration
        _ = PersistProgressAsync(value);
    }

    public async Task MigrationStarted(string migrationId)
    {
        _migrationId = migrationId;
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationStarted", new { MigrationId = migrationId, StartedAt = DateTime.UtcNow });

        await PersistProgressAsync(new MigrationProgress
        {
            Phase = "Started",
            PercentComplete = 0,
            Message = "Migration started"
        });
    }

    public async Task MigrationCompleted(string migrationId)
    {
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationCompleted", new { MigrationId = migrationId, CompletedAt = DateTime.UtcNow });

        await PersistProgressAsync(new MigrationProgress
        {
            Phase = "Completed",
            PercentComplete = 100,
            Message = "Migration completed"
        });
    }

    public async Task MigrationFailed(string migrationId, string error)
    {
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationFailed", new { MigrationId = migrationId, Error = error, FailedAt = DateTime.UtcNow });

        await PersistProgressAsync(new MigrationProgress
        {
            Phase = "Failed",
            PercentComplete = 0,
            Message = error
        });
    }

    private async Task PersistProgressAsync(MigrationProgress value)
    {
        if (string.IsNullOrEmpty(_migrationId) || !Guid.TryParse(_migrationId, out var migrationGuid))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ScaffoldDbContext>();
            db.MigrationProgressRecords.Add(new MigrationProgressRecord
            {
                Id = Guid.NewGuid(),
                MigrationId = migrationGuid,
                Phase = value.Phase,
                PercentComplete = value.PercentComplete,
                CurrentTable = value.CurrentTable,
                RowsProcessed = value.RowsProcessed,
                Message = value.Message,
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch
        {
            // Don't let persistence failures break the migration
        }
    }
}
