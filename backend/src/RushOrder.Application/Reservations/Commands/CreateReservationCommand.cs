using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Reservations.Commands;

public record CreateReservationResult(Guid ReservationId, string ConfirmationCode);

public record CreateReservationCommand(
    Guid RestaurantId,
    string GuestName,
    string? GuestEmail,
    string GuestPhone,
    int PartySize,
    DateTimeOffset ReservedAt,
    string? Notes,
    int DurationMinutes = 90) : ICommand<CreateReservationResult>;

public sealed class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.GuestName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GuestEmail).EmailAddress().MaximumLength(200).When(x => x.GuestEmail is not null);
        RuleFor(x => x.GuestPhone).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PartySize).InclusiveBetween(1, 30);
        RuleFor(x => x.ReservedAt).GreaterThan(DateTimeOffset.UtcNow);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(30, 480);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

public sealed class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, CreateReservationResult>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly INotificationService _notificationService;

    public CreateReservationCommandHandler(
        IReservationRepository reservationRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService,
        INotificationService notificationService)
    {
        _reservationRepository = reservationRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _notificationService = notificationService;
    }

    public async Task<CreateReservationResult> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), request.RestaurantId);

        if (!restaurant.IsActive)
            throw new BusinessRuleException("Restaurant is not currently accepting reservations.");

        var from = request.ReservedAt;
        var to = request.ReservedAt.AddMinutes(request.DurationMinutes);
        var hasAvailability = await _reservationRepository.HasAvailabilityAsync(
            request.RestaurantId, request.PartySize, from, to, cancellationToken);

        if (!hasAvailability)
            throw new BusinessRuleException($"No availability for {request.PartySize} guests at the requested time.");

        var confirmationCode = GenerateConfirmationCode();
        var reservation = Reservation.Create(
            tenantId,
            request.RestaurantId,
            request.GuestName,
            request.GuestEmail,
            request.GuestPhone,
            request.PartySize,
            request.ReservedAt,
            confirmationCode,
            request.Notes,
            request.DurationMinutes);

        await _reservationRepository.AddAsync(reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notificationService.SendReservationConfirmationAsync(reservation, cancellationToken);

        return new CreateReservationResult(reservation.Id, reservation.ConfirmationCode);
    }

    private static string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
