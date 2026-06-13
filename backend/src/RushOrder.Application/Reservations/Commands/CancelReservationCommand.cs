using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Reservations.Commands;

public record CancelReservationCommand(Guid ReservationId, string? Reason = null) : ICommand<Unit>;

public sealed class CancelReservationCommandValidator : AbstractValidator<CancelReservationCommand>
{
    public CancelReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason is not null);
    }
}

public sealed class CancelReservationCommandHandler : IRequestHandler<CancelReservationCommand, Unit>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelReservationCommandHandler(IReservationRepository reservationRepository, IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Reservation), request.ReservationId);

        reservation.Cancel(request.Reason);

        await _reservationRepository.UpdateAsync(reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
