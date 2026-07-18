using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class StatisticsDataService
{
    private readonly AppState _state;
    private readonly ILogger<StatisticsDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public StatisticsDataService(AppState state, ILogger<StatisticsDataService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    // NOTE: backend has no payment-method breakdown endpoint at all — PaymentMethods
    // is always empty until one exists. Real routes are /sales, /products/performance,
    // /waiters/performance (not /products, /waiter as this previously called).
    public async Task<StatisticsDto> GetStatisticsAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
                ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
                : null;

            var rid = _state.CurrentRestaurant?.Id;
            var f   = Uri.EscapeDataString(from.ToDateTime(TimeOnly.MinValue).ToString("O"));
            var t2  = Uri.EscapeDataString(to.ToDateTime(TimeOnly.MaxValue).ToString("O"));
            var baseUrl = "http://localhost:5143/api/v1/analytics";

            var salesTask   = _http.GetStringAsync($"{baseUrl}/sales?restaurantId={rid}&from={f}&to={t2}&groupBy=hour", ct);
            var productTask = _http.GetStringAsync($"{baseUrl}/products/performance?restaurantId={rid}&from={f}&to={t2}", ct);
            var waiterTask  = _http.GetStringAsync($"{baseUrl}/waiters/performance?restaurantId={rid}&from={f}&to={t2}", ct);
            await Task.WhenAll(salesTask, productTask, waiterTask);

            var sales    = JsonConvert.DeserializeObject<ApiEnvelope<BackendSalesDto>>(salesTask.Result)?.Data;
            var products = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendProductPerformanceDto>>>(productTask.Result)?.Data ?? [];
            var waiters  = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendWaiterPerformanceDto>>>(waiterTask.Result)?.Data ?? [];

            if (sales is not null)
            {
                var hourly = sales.Series
                    .GroupBy(s => s.Date.Hour)
                    .Select(g => new HourlyRevenuePoint(g.Key, g.Sum(s => s.Revenue)))
                    .OrderBy(h => h.Hour)
                    .ToList();

                var top = products.OrderByDescending(p => p.Revenue).Take(10)
                    .Select(p => new TopProductPoint(p.Name, p.QuantitySold, p.Revenue)).ToList();

                var waiterRows = waiters.Select(w => new WaiterStatsRow(
                    w.Name, w.OrdersServed, w.Revenue, 0, w.AvgTicket)).ToList();

                return new StatisticsDto(
                    from, to, hourly, top, [], waiterRows,
                    TotalRevenue: sales.Totals.Revenue, TotalOrders: sales.Totals.Orders);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Statistics API unavailable; using mock data");
        }
        return BuildMock(from, to);
    }

    // ── Mock data ─────────────────────────────────────────────────────────────

    private static StatisticsDto BuildMock(DateOnly from, DateOnly to)
    {
        var hourly = new List<HourlyRevenuePoint>
        {
            new(8,  24m),  new(9,  51m),  new(10, 38m),  new(11, 62m),
            new(12, 147m), new(13, 312m), new(14, 398m), new(15, 274m),
            new(16, 89m),  new(17, 61m),  new(18, 103m), new(19, 198m),
            new(20, 421m), new(21, 387m), new(22, 263m),
        };

        var products = new List<TopProductPoint>
        {
            new("Menú del día",      103, 1287.50m),
            new("Café solo",         134,  201.00m),
            new("Gazpacho",           89,  445.00m),
            new("Tortilla española",  68,  646.00m),
            new("Sangría jarra",      58,  696.00m),
            new("Pulpo gallega",      41,  738.00m),
            new("Paella Valencia",    47,  681.50m),
            new("Cochinillo",         32,  704.00m),
            new("Chuletón 400g",      29,  812.00m),
            new("Salmón plancha",     22,  407.00m),
        };

        var payments = new List<PaymentMethodPoint>
        {
            new("Tarjeta",  312, 8427.40m),
            new("Efectivo", 187, 4213.80m),
            new("QR",        54, 1847.60m),
        };

        var waiters = new List<WaiterStatsRow>
        {
            new("Ana García",       198, 5423.80m, 34.2, 27.39m),
            new("Carlos López",     164, 4821.50m, 38.7, 29.40m),
            new("María Rodríguez",  191, 4243.50m, 31.5, 22.22m),
        };

        return new StatisticsDto(
            from, to, hourly, products, payments, waiters,
            TotalRevenue: 14488.80m,
            TotalOrders:  553);
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
