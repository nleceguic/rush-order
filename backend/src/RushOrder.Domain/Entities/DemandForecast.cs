using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Entities;

// One row per (product, forecast date, hour) — regenerated nightly by
// DemandForecastJob for the next 7 days. Confidence reflects how many of the
// last 4 matching weeks actually had sales data to average.
public sealed class DemandForecast : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateOnly ForecastDate { get; private set; }
    public int ForecastHour { get; private set; }
    public decimal PredictedQuantity { get; private set; }
    public ForecastConfidence ConfidenceLevel { get; private set; }

    private DemandForecast() { } // EF Core

    private DemandForecast(
        Guid tenantId, Guid restaurantId, Guid productId,
        DateOnly forecastDate, int forecastHour,
        decimal predictedQuantity, ForecastConfidence confidenceLevel) : base(tenantId)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        if (forecastHour is < 0 or > 23) throw new ArgumentException("ForecastHour must be between 0 and 23.", nameof(forecastHour));

        RestaurantId = restaurantId;
        ProductId = productId;
        ForecastDate = forecastDate;
        ForecastHour = forecastHour;
        PredictedQuantity = Math.Max(0, predictedQuantity);
        ConfidenceLevel = confidenceLevel;
    }

    public static DemandForecast Create(
        Guid tenantId, Guid restaurantId, Guid productId,
        DateOnly forecastDate, int forecastHour,
        decimal predictedQuantity, ForecastConfidence confidenceLevel)
        => new(tenantId, restaurantId, productId, forecastDate, forecastHour, predictedQuantity, confidenceLevel);
}
