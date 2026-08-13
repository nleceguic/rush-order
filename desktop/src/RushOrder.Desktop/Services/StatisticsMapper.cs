using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Services;

internal static class StatisticsMapper
{
    internal static StatisticsDto Map(
        DateOnly from, DateOnly to,
        BackendSalesDto sales,
        IReadOnlyList<BackendProductPerformanceDto> products,
        IReadOnlyList<BackendWaiterPerformanceDto> waiters)
    {
        var hourly = sales.Series
            .GroupBy(s => s.Date.Hour)
            .Select(g => new HourlyRevenuePoint(g.Key, g.Sum(s => s.Revenue)))
            .OrderBy(h => h.Hour)
            .ToList();

        var top = products
            .OrderByDescending(p => p.Revenue)
            .Take(10)
            .Select(p => new TopProductPoint(p.Name, p.QuantitySold, p.Revenue))
            .ToList();

        // Backend's WaiterPerformanceDto has no avg-service-time field, so
        // WaiterStatsRow.AvgMinutes is always 0 from this path.
        var waiterRows = waiters
            .Select(w => new WaiterStatsRow(w.Name, w.OrdersServed, w.Revenue, 0, w.AvgTicket))
            .ToList();

        return new StatisticsDto(
            from, to, hourly, top,
            PaymentMethods: [], // no backend endpoint for payment-method breakdown yet
            waiterRows,
            TotalRevenue: sales.Totals.Revenue,
            TotalOrders: sales.Totals.Orders);
    }
}

// Matches backend's SalesDto (Analytics/DTOs/SalesDto.cs).
internal sealed record BackendSalesDto(IReadOnlyList<BackendSalesSeriesPoint> Series, BackendSalesTotals Totals);
internal sealed record BackendSalesSeriesPoint(DateTimeOffset Date, decimal Revenue, int Orders, int Covers);
internal sealed record BackendSalesTotals(decimal Revenue, int Orders, decimal AvgTicket, DateTimeOffset? BestDay, DateTimeOffset? WorstDay);

// Matches backend's ProductPerformanceDto (Analytics/DTOs/ProductPerformanceDto.cs).
internal sealed record BackendProductPerformanceDto(
    Guid ProductId, string Name, string Category, int QuantitySold, decimal Revenue,
    decimal? AvgRating, string Trend, decimal? MarginEstimate);

// Matches backend's WaiterPerformanceDto (Analytics/DTOs/WaiterPerformanceDto.cs) —
// no avg-service-time field, so WaiterStatsRow.AvgMinutes is always 0 from this path.
internal sealed record BackendWaiterPerformanceDto(
    Guid WaiterId, string Name, int OrdersServed, decimal? AvgRating, decimal Revenue, decimal AvgTicket);
