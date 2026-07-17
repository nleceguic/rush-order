using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Hubs;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.Infrastructure.Services;

// Every 2 minutes, recomputes the ETA for every in-flight order (Confirmed /
// Preparing) — kitchen load changes as new orders come in and others finish,
// so an ETA set at order-creation time can go stale. Pushes "EtaUpdated" over
// the existing order-tracking SignalR group when the change is material
// (>= 2 min), so the PWA's tracking page and the customer aren't spammed
// with noise from 30-second fluctuations.
public sealed class KitchenLoadMonitorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private const int MaterialChangeMinutes = 2;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<OrderTrackingHub> _trackingHub;
    private readonly ILogger<KitchenLoadMonitorService> _logger;

    public KitchenLoadMonitorService(
        IServiceScopeFactory scopeFactory, IHubContext<OrderTrackingHub> trackingHub, ILogger<KitchenLoadMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _trackingHub = trackingHub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during kitchen load monitor cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prepTimeService = scope.ServiceProvider.GetRequiredService<IPrepTimeService>();

        var inFlightOrders = await context.Orders
            .IgnoreQueryFilters()
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Preparing)
            .ToListAsync(ct);

        if (inFlightOrders.Count == 0) return;

        var productIds = inFlightOrders.SelectMany(o => o.Items.Select(i => i.ProductId)).Distinct().ToList();
        var prepMinutesByProduct = await context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PreparationMinutes, ct);

        foreach (var order in inFlightOrders)
        {
            try
            {
                var items = order.Items
                    .Select(i => new PrepTimeItem(
                        i.ProductId,
                        prepMinutesByProduct.GetValueOrDefault(i.ProductId, 10),
                        i.Quantity))
                    .ToList();

                var newEtaMinutes = await prepTimeService.GetEtaMinutesAsync(order.RestaurantId, items, ct);
                var newEstimatedReadyAt = DateTimeOffset.UtcNow.AddMinutes(newEtaMinutes);

                var previous = order.EstimatedReadyAt;
                var changedMaterially = previous is null
                    || Math.Abs((newEstimatedReadyAt - previous.Value).TotalMinutes) >= MaterialChangeMinutes;

                if (!changedMaterially) continue;

                order.SetEstimatedReadyAt(newEstimatedReadyAt);
                await context.SaveChangesAsync(ct);

                await _trackingHub.Clients
                    .Group($"order:{order.Id}")
                    .SendAsync("EtaUpdated", order.Id.ToString(), newEstimatedReadyAt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ETA recompute failed for order {OrderId}", order.Id);
            }
        }
    }
}
