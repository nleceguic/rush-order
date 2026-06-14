using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Restaurants.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Restaurants.Queries;

public record GetRestaurantsQuery : IQuery<List<RestaurantDto>>;

public sealed class GetRestaurantsQueryHandler : IRequestHandler<GetRestaurantsQuery, List<RestaurantDto>>
{
    private readonly IRestaurantRepository _restaurantRepository;

    public GetRestaurantsQueryHandler(IRestaurantRepository restaurantRepository)
        => _restaurantRepository = restaurantRepository;

    public async Task<List<RestaurantDto>> Handle(GetRestaurantsQuery request, CancellationToken cancellationToken)
    {
        var restaurants = await _restaurantRepository.GetAllAsync(cancellationToken);
        return restaurants.Select(ToDto).ToList();
    }

    internal static RestaurantDto ToDto(Restaurant r) => new(
        r.Id, r.TenantId, r.Name, r.Address, r.Phone, r.Email,
        r.LogoUrl, r.CoverUrl, r.Timezone, r.Currency, r.TaxRate, r.IsActive,
        new RestaurantSettingsDto(
            r.Settings.OnlineOrderingEnabled,
            r.Settings.TableOrderingEnabled,
            r.Settings.ReservationsEnabled,
            r.Settings.MaxReservationPartySize,
            r.Settings.ReservationSlotMinutes,
            r.Settings.OpeningTime,
            r.Settings.ClosingTime,
            r.Settings.AutoConfirmOrders,
            r.Settings.PrintReceiptByDefault,
            r.Settings.KitchenDisplayEnabled,
            r.Settings.OrderAlertAfterMinutes),
        r.CreatedAt);
}
