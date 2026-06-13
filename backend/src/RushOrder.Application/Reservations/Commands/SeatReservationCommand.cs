using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Reservations.Commands;

public record SeatReservationCommand(Guid ReservationId, Guid TableId) : ICommand<Unit>;

public sealed class SeatReservationCommandValidator : AbstractValidator<SeatReservationCommand>
{
    public SeatReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.TableId).NotEmpty();
    }
}

public sealed class SeatReservationCommandHandler : IRequestHandler<SeatReservationCommand, Unit>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeatReservationCommandHandler(
        IReservationRepository reservationRepository,
        ITableRepository tableRepository,
        IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _tableRepository = tableRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(SeatReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Reservation), request.ReservationId);

        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        if (table.Capacity < reservation.PartySize)
            throw new BusinessRuleException(
                $"Table capacity ({table.Capacity}) is insufficient for party size ({reservation.PartySize}).");

        reservation.Seat(table.Id);
        table.MarkOccupied();

        await _reservationRepository.UpdateAsync(reservation, cancellationToken);
        await _tableRepository.UpdateAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
