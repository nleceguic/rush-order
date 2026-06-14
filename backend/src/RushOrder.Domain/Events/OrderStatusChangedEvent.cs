using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Events;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid RestaurantId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus) : DomainEvent;
