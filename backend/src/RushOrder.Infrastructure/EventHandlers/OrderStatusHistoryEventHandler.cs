using MediatR;
using Microsoft.Extensions.Logging;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.Infrastructure.EventHandlers;

// Persists one row per order status transition — the only source of "how
// long did this order actually take to prepare" (see IPrepTimeService).
public sealed class OrderStatusHistoryEventHandler :
    INotificationHandler<OrderPlacedEvent>,
    INotificationHandler<OrderStatusChangedEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderStatusHistoryEventHandler> _logger;

    public OrderStatusHistoryEventHandler(AppDbContext context, ILogger<OrderStatusHistoryEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
    {
        await RecordAsync(
            notification.TenantId, notification.OrderId, notification.RestaurantId,
            fromStatus: null, toStatus: OrderStatus.Pending, cancellationToken);
    }

    public async Task Handle(OrderStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        await RecordAsync(
            notification.TenantId, notification.OrderId, notification.RestaurantId,
            notification.PreviousStatus, notification.NewStatus, cancellationToken);
    }

    private Task RecordAsync(
        Guid tenantId, Guid orderId, Guid restaurantId,
        OrderStatus? fromStatus, OrderStatus toStatus, CancellationToken ct)
    {
        try
        {
            // Domain events are dispatched from inside AppDbContext.SaveChangesAsync,
            // *before* it calls base.SaveChangesAsync() — so just Add() here and let
            // it ride along in that same, already-in-flight save. Calling
            // SaveChangesAsync again from within this handler would re-enter the
            // same DbContext mid-save, which EF Core doesn't support safely.
            var history = OrderStatusHistory.Create(tenantId, orderId, restaurantId, fromStatus, toStatus);
            _context.OrderStatusHistories.Add(history);
        }
        catch (Exception ex)
        {
            // Never let history bookkeeping fail the order flow itself.
            _logger.LogError(ex, "Failed to record order status history for order {OrderId} -> {Status}", orderId, toStatus);
        }

        return Task.CompletedTask;
    }
}
