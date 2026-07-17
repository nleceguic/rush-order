namespace RushOrder.Desktop.Models;

public sealed record TopForecastProduct(Guid ProductId, string Name, decimal PredictedQuantity);

public sealed record ForecastSummary(decimal TotalCovers, int? PeakHour, IReadOnlyList<TopForecastProduct> TopProducts);

public sealed record HourlyForecastPoint(int Hour, decimal PredictedOrders, decimal PredictedRevenue);

public sealed record ProductForecastRow(
    Guid ProductId, string Name, decimal PredictedQuantity, decimal RecommendedPrepQuantity, string Confidence);

public sealed record DemandForecastResult(
    ForecastSummary Summary, IReadOnlyList<HourlyForecastPoint> Hourly, IReadOnlyList<ProductForecastRow> Products);

public sealed record KitchenEta(decimal? AverageMinutes, int SampleSize);
