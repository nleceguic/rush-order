using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RushOrder.Infrastructure.Hubs;

[Authorize]
public sealed class RestaurantHub : Hub
{
    // Client joins the general restaurant group (all staff)
    public Task JoinRestaurantAsync(string restaurantId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"restaurant:{restaurantId}");

    // Client joins the kitchen-only group (KDS screen)
    public Task JoinKitchenAsync(string restaurantId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen:{restaurantId}");

    // Staff acknowledges receipt of a new order — can be used for logging/auditing
    public Task AcknowledgeOrderAsync(string orderId)
    {
        // Acknowledgment received — implementation can log or update state if needed
        return Task.CompletedTask;
    }

    public override async Task OnConnectedAsync()
    {
        var tid = Context.User?.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tid))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tid}");

        await base.OnConnectedAsync();
    }
}
