namespace RushOrder.Application.Analytics.DTOs;

public sealed record ProductPerformanceDto(
    Guid ProductId,
    string Name,
    string Category,
    int QuantitySold,
    decimal Revenue,
    decimal? AvgRating,
    string Trend,
    decimal? MarginEstimate);
