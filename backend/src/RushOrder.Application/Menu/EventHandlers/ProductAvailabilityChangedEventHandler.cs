using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Events;

namespace RushOrder.Application.Menu.EventHandlers;

public sealed class ProductAvailabilityChangedEventHandler
    : INotificationHandler<ProductAvailabilityChangedEvent>
{
    private readonly IMenuCacheService _cacheService;

    public ProductAvailabilityChangedEventHandler(IMenuCacheService cacheService)
        => _cacheService = cacheService;

    public async Task Handle(ProductAvailabilityChangedEvent notification, CancellationToken cancellationToken)
        => await _cacheService.InvalidateMenuAsync(notification.RestaurantId, cancellationToken);
}
