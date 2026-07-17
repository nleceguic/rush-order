using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Forecasting;

namespace RushOrder.Infrastructure.Services;

// Runs daily at 06:00 UTC: for every active restaurant, pulls 90 days of
// history, computes the next 7 days' hourly demand forecast per product
// (DemandForecastEngine), and replaces the demand_forecasts rows for that
// window. See DemandForecastEngine for the actual formula.
//
// Gates on UTC 06:00 rather than each restaurant's local time — a fully
// per-timezone trigger would need per-restaurant scheduling, which is more
// machinery than a single daily batch job needs; the forecast content
// itself IS computed in each restaurant's local timezone (see
// GetHistoricalSalesAsync's AT TIME ZONE), only the *trigger* time is UTC.
public sealed class DemandForecastJob : BackgroundService
{
    private const int RunHourUtc = 6;
    private const int ForecastDays = 7;
    private const int HistoryDays = 90;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DemandForecastJob> _logger;
    private DateOnly? _lastRunDate;

    public DemandForecastJob(IServiceScopeFactory scopeFactory, ILogger<DemandForecastJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                if (now.Hour == RunHourUtc && _lastRunDate != today)
                {
                    _lastRunDate = today;
                    await RunAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during demand forecast cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDemandForecastRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<DemandForecastEngine>();

        var restaurants = await repository.GetActiveRestaurantsAsync(ct);
        _logger.LogInformation("Running demand forecast for {Count} active restaurants", restaurants.Count);

        foreach (var restaurant in restaurants)
        {
            try
            {
                await RunForRestaurantAsync(repository, engine, restaurant, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Demand forecast failed for restaurant {RestaurantId}", restaurant.RestaurantId);
            }
        }
    }

    private static async Task RunForRestaurantAsync(
        IDemandForecastRepository repository, DemandForecastEngine engine, ActiveRestaurantRow restaurant, CancellationToken ct)
    {
        var products = await repository.GetActiveProductsAsync(restaurant.TenantId, restaurant.RestaurantId, ct);
        if (products.Count == 0) return;

        var since = DateTimeOffset.UtcNow.AddDays(-HistoryDays);
        var history = await repository.GetHistoricalSalesAsync(
            restaurant.TenantId, restaurant.RestaurantId, since, restaurant.Timezone, ct);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var openingTime = TimeOnly.TryParse(restaurant.OpeningTime, out var open) ? open : new TimeOnly(9, 0);
        var closingTime = TimeOnly.TryParse(restaurant.ClosingTime, out var close) ? close : new TimeOnly(23, 0);

        var forecasts = await engine.BuildForecastsAsync(
            restaurant.TenantId, restaurant.RestaurantId, products, history,
            startDate, ForecastDays, openingTime, closingTime, ct);

        await repository.ReplaceForecastsAsync(
            restaurant.TenantId, restaurant.RestaurantId,
            startDate, startDate.AddDays(ForecastDays - 1), forecasts, ct);
    }
}
