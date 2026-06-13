using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Common.Interfaces;

public interface IReservationRepository : IRepository<Reservation>
{
    Task<IReadOnlyList<Reservation>> GetByDateAsync(Guid restaurantId, DateTimeOffset date, ReservationStatus? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reservation>> GetUpcomingAsync(Guid restaurantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<bool> HasAvailabilityAsync(Guid restaurantId, int partySize, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
