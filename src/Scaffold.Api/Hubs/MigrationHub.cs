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

    public async Task LeaveMigrationGroup(string migrationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, migrationId);
    }
}
