using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Events;

public sealed record TableStatusChangedEvent(
    Guid TableId,
    Guid RestaurantId,
    TableStatus NewStatus) : DomainEvent;
