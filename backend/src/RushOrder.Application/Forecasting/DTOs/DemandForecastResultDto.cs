namespace RushOrder.Application.Forecasting.DTOs;

public sealed record TopForecastProductDto(Guid ProductId, string Name, decimal PredictedQuantity);

public sealed record DemandForecastSummaryDto(
    decimal TotalCovers, int? PeakHour, IReadOnlyList<TopForecastProductDto> TopProducts);

public sealed record HourlyForecastDto(int Hour, decimal PredictedOrders, decimal PredictedRevenue);

// Confidence is the worst (lowest) confidence among that product's hourly
// rows for the day — a conservative pick for a red/yellow/green traffic
// light: any gap in the history shows as reduced confidence for the day.
public sealed record ProductForecastDto(
    Guid ProductId, string Name, decimal PredictedQuantity, decimal RecommendedPrepQuantity, string Confidence);

public sealed record DemandForecastResultDto(
    DemandForecastSummaryDto Summary,
    IReadOnlyList<HourlyForecastDto> Hourly,
    IReadOnlyList<ProductForecastDto> Products);
