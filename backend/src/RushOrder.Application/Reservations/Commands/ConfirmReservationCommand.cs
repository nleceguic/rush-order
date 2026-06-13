using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Reservations.Commands;

public record ConfirmReservationCommand(Guid ReservationId) : ICommand<Unit>;

public sealed class ConfirmReservationCommandValidator : AbstractValidator<ConfirmReservationCommand>
{
    public ConfirmReservationCommandValidator() => RuleFor(x => x.ReservationId).NotEmpty();
}

public sealed class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand, Unit>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmReservationCommandHandler(IReservationRepository reservationRepository, IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Reservation), request.ReservationId);

        reservation.Confirm();

        await _reservationRepository.UpdateAsync(reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
