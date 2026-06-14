using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Analytics.Queries;

public record GetProductPerformanceQuery(
    Guid RestaurantId,
    DateTimeOffset From,
    DateTimeOffset To) : IQuery<IReadOnlyList<ProductPerformanceDto>>;

public sealed class GetProductPerformanceQueryHandler
    : IRequestHandler<GetProductPerformanceQuery, IReadOnlyList<ProductPerformanceDto>>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GetProductPerformanceQueryHandler(IAnalyticsRepository analytics, IDistributedCache cache)
    {
        _analytics = analytics;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ProductPerformanceDto>> Handle(
        GetProductPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"analytics:products:{request.RestaurantId}:{request.From:yyyyMMdd}:{request.To:yyyyMMdd}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<IReadOnlyList<ProductPerformanceDto>>(cached, JsonOpts)!;

        var result = await _analytics.GetProductPerformanceAsync(
            request.RestaurantId, request.From, request.To, cancellationToken);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);

        return result;
    }
}
