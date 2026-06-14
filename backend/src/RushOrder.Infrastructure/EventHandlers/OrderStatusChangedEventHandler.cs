using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Infrastructure.Hubs;

namespace RushOrder.Infrastructure.EventHandlers;

public sealed class OrderStatusChangedEventHandler : INotificationHandler<OrderStatusChangedEvent>
{
    private readonly IHubContext<RestaurantHub> _restaurantHub;
    private readonly IHubContext<OrderTrackingHub> _trackingHub;
    private readonly ILogger<OrderStatusChangedEventHandler> _logger;

    public OrderStatusChangedEventHandler(
        IHubContext<RestaurantHub> restaurantHub,
        IHubContext<OrderTrackingHub> trackingHub,
        ILogger<OrderStatusChangedEventHandler> logger)
    {
        _restaurantHub = restaurantHub;
        _trackingHub = trackingHub;
        _logger = logger;
    }

    public async Task Handle(OrderStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow;
            var orderId = notification.OrderId.ToString();

            // Notify all restaurant staff
            await _restaurantHub.Clients
                .Group($"restaurant:{notification.RestaurantId}")
                .SendAsync("OrderStatusUpdated", orderId, notification.NewStatus.ToString(), timestamp, cancellationToken);

            // Notify the customer tracking group
            var (message, estimatedMinutes) = StatusMessage(notification.NewStatus);

            await _trackingHub.Clients
                .Group($"order:{orderId}")
                .SendAsync("OrderStatusUpdated", notification.NewStatus.ToString(), message, estimatedMinutes, cancellationToken);

            // If the order is ready, send the distinct OrderReady event
            if (notification.NewStatus == OrderStatus.Ready)
            {
                await _trackingHub.Clients
                    .Group($"order:{orderId}")
                    .SendAsync("OrderReady", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OrderStatusUpdated SignalR event for order {OrderId}.", notification.OrderId);
        }
    }

    private static (string message, int? estimatedMinutes) StatusMessage(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed  => ("Tu pedido ha sido confirmado.", 20),
        OrderStatus.Preparing  => ("Tu pedido está siendo preparado.", 12),
        OrderStatus.Ready      => ("¡Tu pedido está listo para servir!", null),
        OrderStatus.Served     => ("Tu pedido ha sido servido. ¡Buen provecho!", null),
        OrderStatus.Paid       => ("Pedido pagado. Gracias.", null),
        OrderStatus.Cancelled  => ("Tu pedido ha sido cancelado.", null),
        _                      => (status.ToString(), null)
    };
}
