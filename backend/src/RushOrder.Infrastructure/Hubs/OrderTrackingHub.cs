using Microsoft.AspNetCore.SignalR;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Hubs;

public sealed class OrderTrackingHub : Hub
{
    private readonly IOrderVerificationService _verificationService;

    public OrderTrackingHub(IOrderVerificationService verificationService)
        => _verificationService = verificationService;

    // Customer joins their order's tracking group after token verification
    public async Task JoinOrderTrackingAsync(string orderId, string? verificationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationToken)) return;
        if (!Guid.TryParse(orderId, out var orderGuid)) return;
        if (!_verificationService.VerifyToken(orderGuid, verificationToken)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"order:{orderId}");
    }
}
