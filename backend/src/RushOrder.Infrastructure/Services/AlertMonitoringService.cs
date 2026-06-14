using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Infrastructure.Hubs;
using RushOrder.Infrastructure.Persistence.Repositories;

namespace RushOrder.Infrastructure.Services;

public sealed class AlertMonitoringService : BackgroundService
{
    private readonly IHubContext<RestaurantHub> _hub;
    private readonly string _connectionString;
    private readonly ILogger<AlertMonitoringService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public AlertMonitoringService(
        IHubContext<RestaurantHub> hub,
        IConfiguration configuration,
        ILogger<AlertMonitoringService> logger)
    {
        _hub = hub;
        _connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is not configured.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app fully start before running the first check
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAlertsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert monitoring cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAlertsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Get all active restaurants across all tenants (no tenant filter — background service)
        const string restaurantsQuery = """
            SELECT "Id" AS RestaurantId, "TenantId"
            FROM restaurants
            WHERE "IsActive" = true
            """;

        var restaurants = await conn.QueryAsync<(Guid RestaurantId, Guid TenantId)>(
            new CommandDefinition(restaurantsQuery, cancellationToken: ct));

        foreach (var (restaurantId, tenantId) in restaurants)
        {
            try
            {
                var alerts = await AnalyticsRepository.GetActiveAlertsForRestaurantAsync(
                    conn, restaurantId, tenantId, ct);

                if (alerts.Count > 0)
                {
                    await _hub.Clients
                        .Group($"restaurant:{restaurantId}")
                        .SendAsync("AlertTriggered", alerts, ct);

                    _logger.LogDebug(
                        "Sent {Count} alert(s) to restaurant {RestaurantId}",
                        alerts.Count, restaurantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alert check failed for restaurant {RestaurantId}", restaurantId);
            }
        }
    }
}
