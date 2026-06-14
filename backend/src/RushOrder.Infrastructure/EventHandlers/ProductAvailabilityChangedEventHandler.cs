using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RushOrder.Domain.Events;
using RushOrder.Infrastructure.Hubs;

namespace RushOrder.Infrastructure.EventHandlers;

public sealed class ProductAvailabilityChangedEventHandler : INotificationHandler<ProductAvailabilityChangedEvent>
{
    private readonly IHubContext<RestaurantHub> _restaurantHub;
    private readonly ILogger<ProductAvailabilityChangedEventHandler> _logger;

    public ProductAvailabilityChangedEventHandler(
        IHubContext<RestaurantHub> restaurantHub,
        ILogger<ProductAvailabilityChangedEventHandler> logger)
    {
        _restaurantHub = restaurantHub;
        _logger = logger;
    }

    public async Task Handle(ProductAvailabilityChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _restaurantHub.Clients
                .Group($"restaurant:{notification.RestaurantId}")
                .SendAsync(
                    "ProductAvailabilityChanged",
                    notification.ProductId.ToString(),
                    notification.IsAvailable,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ProductAvailabilityChanged SignalR event for product {ProductId}.", notification.ProductId);
        }
    }
}
