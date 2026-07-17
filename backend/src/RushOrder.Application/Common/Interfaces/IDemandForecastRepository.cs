using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

// One historical (product, local day, hour) sales data point, used by
// DemandForecastEngine to compute base_forecast / seasonality_factor.
public sealed record HistoricalSaleRow(
    Guid ProductId, DateOnly LocalDate, int DayOfWeek, int Hour, int Quantity);

public sealed record ForecastProductRow(Guid ProductId, string Name);

public sealed record ForecastReadRow(
    Guid ProductId, string Name, decimal Price, int Hour, decimal PredictedQuantity, string ConfidenceLevel);

public sealed record ActiveRestaurantRow(
    Guid RestaurantId, Guid TenantId, string Timezone,
    string OpeningTime, string ClosingTime, int KitchenCapacity);

public interface IDemandForecastRepository
{
    Task<IReadOnlyList<ActiveRestaurantRow>> GetActiveRestaurantsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForecastProductRow>> GetActiveProductsAsync(
        Guid tenantId, Guid restaurantId, CancellationToken cancellationToken = default);

    // Raw sales history for the last N days, one row per (product, local day, hour).
    Task<IReadOnlyList<HistoricalSaleRow>> GetHistoricalSalesAsync(
        Guid tenantId, Guid restaurantId, DateTimeOffset since, string timezone, CancellationToken cancellationToken = default);

    // Deletes any existing forecast rows in [fromDate, toDate] for the restaurant, then inserts the new ones.
    Task ReplaceForecastsAsync(
        Guid tenantId, Guid restaurantId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<DemandForecast> forecasts, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForecastReadRow>> GetForecastRowsAsync(
        Guid tenantId, Guid restaurantId, DateOnly date, Guid? productId, CancellationToken cancellationToken = default);
}
