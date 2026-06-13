using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Reservations.DTOs;

namespace RushOrder.Application.Reservations.Queries;

public record GetUpcomingReservationsQuery(Guid RestaurantId, int Hours = 2) : IQuery<List<ReservationDto>>;

public sealed class GetUpcomingReservationsQueryHandler : IRequestHandler<GetUpcomingReservationsQuery, List<ReservationDto>>
{
    private readonly IReservationRepository _reservationRepository;

    public GetUpcomingReservationsQueryHandler(IReservationRepository reservationRepository)
        => _reservationRepository = reservationRepository;

    public async Task<List<ReservationDto>> Handle(GetUpcomingReservationsQuery request, CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddHours(request.Hours);
        var reservations = await _reservationRepository.GetUpcomingAsync(
            request.RestaurantId, from, to, cancellationToken);

        return reservations
            .Select(r => new ReservationDto(
                r.Id, r.RestaurantId, r.TableId, r.GuestName, r.GuestEmail, r.GuestPhone,
                r.PartySize, r.ReservedAt, r.EndsAt, r.DurationMinutes,
                r.Status.ToString(), r.ConfirmationCode, r.Notes, r.CancellationReason, r.CreatedAt))
            .ToList();
    }
}
