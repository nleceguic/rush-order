using RushOrder.Application.Menu.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public interface IMenuCacheService
{
    Task<PublicMenuDto?> GetMenuAsync(string qrToken, CancellationToken cancellationToken = default);
    Task SetMenuAsync(string qrToken, PublicMenuDto menu, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task InvalidateMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
