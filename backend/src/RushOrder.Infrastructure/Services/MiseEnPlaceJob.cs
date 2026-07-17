using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Hubs;

namespace RushOrder.Infrastructure.Services;

// Every day at 08:00 UTC, tells the manager what today's demand forecast
// expects to sell most ("Hoy se espera vender: X chuletones, Y raciones de
// croquetas..."), so mise en place can be sized against it.
//
// The spec calls for "notificación push a la PWA del encargado" — but there
// is no manager-facing PWA with a web push subscription anywhere in this
// codebase (only the customer PWA exists, and only for order tracking). The
// desktop app IS what managers actually run, and it's already wired to
// RestaurantHub with a live toast/notification pipeline (see AlertsWidget /
// AlertTriggered), so this delivers the same alert there via SignalR
// instead of literal browser push.
public sealed class MiseEnPlaceJob : BackgroundService
{
    private const int RunHourUtc = 8;
    private const int TopProductCount = 5;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RestaurantHub> _hub;
    private readonly ILogger<MiseEnPlaceJob> _logger;
    private DateOnly? _lastRunDate;

    public MiseEnPlaceJob(IServiceScopeFactory scopeFactory, IHubContext<RestaurantHub> hub, ILogger<MiseEnPlaceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
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
                    await RunAsync(today, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during mise en place alert cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDemandForecastRepository>();

        var restaurants = await repository.GetActiveRestaurantsAsync(ct);

        foreach (var restaurant in restaurants)
        {
            try
            {
                var rows = await repository.GetForecastRowsAsync(
                    restaurant.TenantId, restaurant.RestaurantId, today, productId: null, ct);

                if (rows.Count == 0) continue;

                var topProducts = rows
                    .GroupBy(r => r.Name)
                    .Select(g => new { Name = g.Key, Quantity = g.Sum(r => r.PredictedQuantity) })
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => p.Quantity)
                    .Take(TopProductCount)
                    .ToList();

                if (topProducts.Count == 0) continue;

                var message = "Hoy se espera vender: " +
                    string.Join(", ", topProducts.Select(p => $"{Math.Round(p.Quantity)} {p.Name}")) + ".";

                await _hub.Clients
                    .Group($"restaurant:{restaurant.RestaurantId}")
                    .SendAsync("MiseEnPlaceAlert", message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mise en place alert failed for restaurant {RestaurantId}", restaurant.RestaurantId);
            }
        }
    }
}
