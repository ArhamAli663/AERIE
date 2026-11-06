namespace AERIE.Api.Hubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

[Authorize]
public class AerieHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            // Add user connection to their private tenant group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        }

        // Add to public marketplace feed group
        await Groups.AddToGroupAsync(Context.ConnectionId, "MarketplaceFeed");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "MarketplaceFeed");

        await base.OnDisconnectedAsync(exception);
    }
}
