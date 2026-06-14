using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RushOrder.Domain.Events;
using RushOrder.Infrastructure.Hubs;

namespace RushOrder.Infrastructure.EventHandlers;

public sealed class TableStatusChangedEventHandler : INotificationHandler<TableStatusChangedEvent>
{
    private readonly IHubContext<RestaurantHub> _restaurantHub;
    private readonly ILogger<TableStatusChangedEventHandler> _logger;

    public TableStatusChangedEventHandler(
        IHubContext<RestaurantHub> restaurantHub,
        ILogger<TableStatusChangedEventHandler> logger)
    {
        _restaurantHub = restaurantHub;
        _logger = logger;
    }

    public async Task Handle(TableStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _restaurantHub.Clients
                .Group($"restaurant:{notification.RestaurantId}")
                .SendAsync(
                    "TableStatusChanged",
                    notification.TableId.ToString(),
                    notification.NewStatus.ToString(),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send TableStatusChanged SignalR event for table {TableId}.", notification.TableId);
        }
    }
}
