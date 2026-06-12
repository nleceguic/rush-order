namespace RushOrder.Domain.Events;

public sealed record OrderPlacedEvent(
    Guid OrderId,
    Guid TableId,
    Guid RestaurantId,
    string OrderNumber) : DomainEvent;
