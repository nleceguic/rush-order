using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class PrepTimeService : IPrepTimeService
{
    private const int HistoryDays = 30;
    private const int MinimumEtaMinutes = 5;

    private readonly IPrepTimeRepository _prepTimeRepository;
    private readonly IRestaurantRepository _restaurants;

    public PrepTimeService(IPrepTimeRepository prepTimeRepository, IRestaurantRepository restaurants)
    {
        _prepTimeRepository = prepTimeRepository;
        _restaurants = restaurants;
    }

    public async Task<int> GetEtaMinutesAsync(
        Guid restaurantId, IReadOnlyList<PrepTimeItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return MinimumEtaMinutes;

        var restaurant = await _restaurants.GetByIdAsync(restaurantId, cancellationToken);
        if (restaurant is null) return MinimumEtaMinutes;

        var since = DateTimeOffset.UtcNow.AddDays(-HistoryDays);
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var historicalAverages = await _prepTimeRepository.GetAveragePrepMinutesAsync(
            restaurant.TenantId, restaurantId, productIds, since, cancellationToken);

        // Per distinct dish, not multiplied by quantity — most kitchens cook
        // multiple units of the same dish in parallel (same grill/fryer batch),
        // so 2x croquetas isn't ~2x the wait.
        var basePrepMinutes = items
            .GroupBy(i => i.ProductId)
            .Sum(g => historicalAverages.TryGetValue(g.Key, out var avg) ? avg : g.First().PreparationMinutes);

        var inPreparation = await _prepTimeRepository.GetOrdersInPreparationCountAsync(
            restaurant.TenantId, restaurantId, cancellationToken);
        var capacity = Math.Max(1, restaurant.Settings.KitchenCapacity);

        // Floor of 1.0: an idle kitchen doesn't cook faster than the base
        // estimate, it just isn't slowed down by a queue.
        var kitchenLoadFactor = Math.Max(1.0m, (decimal)inPreparation / capacity);

        var etaMinutes = (int)Math.Ceiling(basePrepMinutes * kitchenLoadFactor);
        return Math.Max(MinimumEtaMinutes, etaMinutes);
    }
}
