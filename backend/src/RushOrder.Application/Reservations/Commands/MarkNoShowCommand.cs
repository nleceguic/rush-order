using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Reservations.Commands;

public record MarkNoShowCommand(Guid ReservationId) : ICommand<Unit>;

public sealed class MarkNoShowCommandValidator : AbstractValidator<MarkNoShowCommand>
{
    public MarkNoShowCommandValidator() => RuleFor(x => x.ReservationId).NotEmpty();
}

public sealed class MarkNoShowCommandHandler : IRequestHandler<MarkNoShowCommand, Unit>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNoShowCommandHandler(IReservationRepository reservationRepository, IUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(MarkNoShowCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Reservation), request.ReservationId);

        reservation.MarkNoShow();

        await _reservationRepository.UpdateAsync(reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
