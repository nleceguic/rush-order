using MediatR;

namespace RushOrder.Domain.Common;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
