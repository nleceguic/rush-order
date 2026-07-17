using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Events;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid TenantId,
    Guid RestaurantId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus) : DomainEvent;
