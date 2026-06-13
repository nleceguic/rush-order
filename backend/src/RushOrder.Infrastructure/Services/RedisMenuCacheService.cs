using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Menu.DTOs;

namespace RushOrder.Infrastructure.Services;

public sealed class RedisMenuCacheService : IMenuCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisMenuCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<PublicMenuDto?> GetMenuAsync(string qrToken, CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(MenuKey(qrToken), cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<PublicMenuDto>(json, JsonOptions);
    }

    public async Task SetMenuAsync(
        string qrToken,
        PublicMenuDto menu,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var opts = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
        };
        await _cache.SetStringAsync(MenuKey(qrToken), JsonSerializer.Serialize(menu, JsonOptions), opts, cancellationToken);
        await AddToRestaurantIndexAsync(qrToken, menu.RestaurantId, cancellationToken);
    }

    public async Task InvalidateMenuAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var indexKey = IndexKey(restaurantId);
        var indexJson = await _cache.GetStringAsync(indexKey, cancellationToken);
        if (indexJson is null) return;

        var tokens = JsonSerializer.Deserialize<List<string>>(indexJson, JsonOptions) ?? [];
        foreach (var token in tokens)
            await _cache.RemoveAsync(MenuKey(token), cancellationToken);

        await _cache.RemoveAsync(indexKey, cancellationToken);
    }

    private async Task AddToRestaurantIndexAsync(string qrToken, Guid restaurantId, CancellationToken cancellationToken)
    {
        var indexKey = IndexKey(restaurantId);
        var existing = await _cache.GetStringAsync(indexKey, cancellationToken);
        var tokens = existing is not null
            ? JsonSerializer.Deserialize<List<string>>(existing, JsonOptions) ?? []
            : new List<string>();

        if (!tokens.Contains(qrToken))
        {
            tokens.Add(qrToken);
            await _cache.SetStringAsync(indexKey, JsonSerializer.Serialize(tokens, JsonOptions), cancellationToken);
        }
    }

    private static string MenuKey(string qrToken) => $"menu:qr:{qrToken}";
    private static string IndexKey(Guid restaurantId) => $"menu:idx:{restaurantId}";
}
