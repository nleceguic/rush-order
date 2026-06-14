using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Analytics.Queries;

public record GetSalesQuery(
    Guid RestaurantId,
    DateTimeOffset From,
    DateTimeOffset To,
    string GroupBy) : IQuery<SalesDto>;

public sealed class GetSalesQueryHandler : IRequestHandler<GetSalesQuery, SalesDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GetSalesQueryHandler(IAnalyticsRepository analytics, IDistributedCache cache)
    {
        _analytics = analytics;
        _cache = cache;
    }

    public async Task<SalesDto> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"analytics:sales:{request.RestaurantId}:{request.From:yyyyMMddHH}:{request.To:yyyyMMddHH}:{request.GroupBy}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<SalesDto>(cached, JsonOpts)!;

        var result = await _analytics.GetSalesAsync(
            request.RestaurantId, request.From, request.To, request.GroupBy, cancellationToken);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);

        return result;
    }
}
