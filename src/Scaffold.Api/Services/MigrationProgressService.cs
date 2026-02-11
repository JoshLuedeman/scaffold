using Microsoft.AspNetCore.SignalR;
using Scaffold.Api.Hubs;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Services;

public class MigrationProgressService : IProgress<MigrationProgress>
{
    private readonly IHubContext<MigrationHub> _hubContext;
    private string _migrationId = string.Empty;

    public MigrationProgressService(IHubContext<MigrationHub> hubContext)
    {
        _hubContext = hubContext;
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
    }

    public async Task MigrationStarted(string migrationId)
    {
        _migrationId = migrationId;
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationStarted", new { MigrationId = migrationId, StartedAt = DateTime.UtcNow });
    }

    public async Task MigrationCompleted(string migrationId)
    {
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationCompleted", new { MigrationId = migrationId, CompletedAt = DateTime.UtcNow });
    }

    public async Task MigrationFailed(string migrationId, string error)
    {
        await _hubContext.Clients.Group(migrationId)
            .SendAsync("MigrationFailed", new { MigrationId = migrationId, Error = error, FailedAt = DateTime.UtcNow });
    }
}
