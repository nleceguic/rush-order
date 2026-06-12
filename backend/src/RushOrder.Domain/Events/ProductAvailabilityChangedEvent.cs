namespace RushOrder.Domain.Events;

public sealed record ProductAvailabilityChangedEvent(
    Guid ProductId,
    Guid RestaurantId,
    bool IsAvailable) : DomainEvent;
