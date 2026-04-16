using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Scaffold.Api.Hubs;

[Authorize]
public class MigrationHub : Hub
{
    public async Task JoinMigration(string migrationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, migrationId);
    }

    /// <summary>
    /// Named LeaveMigrationGroup (not LeaveMigration) to match SignalR group management semantics.
    /// </summary>
    public async Task LeaveMigrationGroup(string migrationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, migrationId);
    }
}
