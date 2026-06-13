using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Reservations.DTOs;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Reservations.Queries;

public record GetReservationsQuery(
    Guid RestaurantId,
    DateTimeOffset Date,
    ReservationStatus? Status) : IQuery<List<ReservationDto>>;

public sealed class GetReservationsQueryHandler : IRequestHandler<GetReservationsQuery, List<ReservationDto>>
{
    private readonly IReservationRepository _reservationRepository;

    public GetReservationsQueryHandler(IReservationRepository reservationRepository)
        => _reservationRepository = reservationRepository;

    public async Task<List<ReservationDto>> Handle(GetReservationsQuery request, CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository.GetByDateAsync(
            request.RestaurantId, request.Date, request.Status, cancellationToken);

        return reservations
            .Select(r => new ReservationDto(
                r.Id, r.RestaurantId, r.TableId, r.GuestName, r.GuestEmail, r.GuestPhone,
                r.PartySize, r.ReservedAt, r.EndsAt, r.DurationMinutes,
                r.Status.ToString(), r.ConfirmationCode, r.Notes, r.CancellationReason, r.CreatedAt))
            .ToList();
    }
}
