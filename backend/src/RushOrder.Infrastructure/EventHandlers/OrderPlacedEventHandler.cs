using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Events;
using RushOrder.Infrastructure.Hubs;

namespace RushOrder.Infrastructure.EventHandlers;

public sealed class OrderPlacedEventHandler : INotificationHandler<OrderPlacedEvent>
{
    private readonly IHubContext<RestaurantHub> _restaurantHub;
    private readonly IOrderRepository _orderRepository;
    private readonly ITableRepository _tableRepository;
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public OrderPlacedEventHandler(
        IHubContext<RestaurantHub> restaurantHub,
        IOrderRepository orderRepository,
        ITableRepository tableRepository,
        ILogger<OrderPlacedEventHandler> logger)
    {
        _restaurantHub = restaurantHub;
        _orderRepository = orderRepository;
        _tableRepository = tableRepository;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);
            if (order is null)
            {
                _logger.LogWarning("OrderPlacedEventHandler: order {OrderId} not found.", notification.OrderId);
                return;
            }

            var table = await _tableRepository.GetByIdAsync(notification.TableId, cancellationToken);

            var dto = new OrderRealTimeDto(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                RestaurantId: notification.RestaurantId,
                TableId: notification.TableId,
                TableName: table?.Name ?? string.Empty,
                Status: order.Status.ToString(),
                Source: order.Source.ToString(),
                Items: order.Items.Select(i => new OrderItemRealTimeDto(
                    i.ProductId, i.Name, i.Quantity, i.Notes,
                    i.Modifiers.ToList())).ToList(),
                Total: order.Total.Amount,
                Currency: order.Total.Currency,
                Notes: order.Notes,
                PlacedAt: order.CreatedAt);

            var restaurantGroup = $"restaurant:{notification.RestaurantId}";
            var kitchenGroup = $"kitchen:{notification.RestaurantId}";

            await _restaurantHub.Clients.Group(restaurantGroup)
                .SendAsync("OrderReceived", dto, cancellationToken);

            await _restaurantHub.Clients.Group(kitchenGroup)
                .SendAsync("OrderReceived", dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OrderReceived SignalR event for order {OrderId}.", notification.OrderId);
        }
    }
}
