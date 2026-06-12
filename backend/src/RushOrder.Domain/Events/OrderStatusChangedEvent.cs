using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Events;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus) : DomainEvent;
