using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Restaurants.Commands;

public record UpdateRestaurantSettingsCommand(
    Guid RestaurantId,
    bool OnlineOrderingEnabled,
    bool TableOrderingEnabled,
    bool ReservationsEnabled,
    int MaxReservationPartySize,
    int ReservationSlotMinutes,
    string OpeningTime,
    string ClosingTime,
    bool AutoConfirmOrders,
    bool PrintReceiptByDefault,
    bool KitchenDisplayEnabled,
    int OrderAlertAfterMinutes) : ICommand<Unit>;

public sealed class UpdateRestaurantSettingsCommandValidator
    : AbstractValidator<UpdateRestaurantSettingsCommand>
{
    public UpdateRestaurantSettingsCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.MaxReservationPartySize).InclusiveBetween(1, 100);
        RuleFor(x => x.ReservationSlotMinutes).InclusiveBetween(15, 120);
        RuleFor(x => x.OrderAlertAfterMinutes).InclusiveBetween(1, 120);
        RuleFor(x => x.OpeningTime).NotEmpty().Matches(@"^\d{2}:\d{2}$")
            .WithMessage("Opening time must be in HH:mm format.");
        RuleFor(x => x.ClosingTime).NotEmpty().Matches(@"^\d{2}:\d{2}$")
            .WithMessage("Closing time must be in HH:mm format.");
    }
}

public sealed class UpdateRestaurantSettingsCommandHandler
    : IRequestHandler<UpdateRestaurantSettingsCommand, Unit>
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRestaurantSettingsCommandHandler(
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork)
    {
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(
        UpdateRestaurantSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), request.RestaurantId);

        var settings = new RestaurantSettings
        {
            OnlineOrderingEnabled   = request.OnlineOrderingEnabled,
            TableOrderingEnabled    = request.TableOrderingEnabled,
            ReservationsEnabled     = request.ReservationsEnabled,
            MaxReservationPartySize = request.MaxReservationPartySize,
            ReservationSlotMinutes  = request.ReservationSlotMinutes,
            OpeningTime             = request.OpeningTime,
            ClosingTime             = request.ClosingTime,
            AutoConfirmOrders       = request.AutoConfirmOrders,
            PrintReceiptByDefault   = request.PrintReceiptByDefault,
            KitchenDisplayEnabled   = request.KitchenDisplayEnabled,
            OrderAlertAfterMinutes  = request.OrderAlertAfterMinutes
        };

        restaurant.UpdateSettings(settings);
        await _restaurantRepository.UpdateAsync(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
