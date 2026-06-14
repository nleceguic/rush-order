using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Analytics.Queries;

public record GetTablePerformanceQuery(
    Guid RestaurantId,
    DateTimeOffset From,
    DateTimeOffset To) : IQuery<IReadOnlyList<TablePerformanceDto>>;

public sealed class GetTablePerformanceQueryHandler
    : IRequestHandler<GetTablePerformanceQuery, IReadOnlyList<TablePerformanceDto>>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GetTablePerformanceQueryHandler(IAnalyticsRepository analytics, IDistributedCache cache)
    {
        _analytics = analytics;
        _cache = cache;
    }

    public async Task<IReadOnlyList<TablePerformanceDto>> Handle(
        GetTablePerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"analytics:tables:{request.RestaurantId}:{request.From:yyyyMMdd}:{request.To:yyyyMMdd}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<IReadOnlyList<TablePerformanceDto>>(cached, JsonOpts)!;

        var result = await _analytics.GetTablePerformanceAsync(
            request.RestaurantId, request.From, request.To, cancellationToken);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);

        return result;
    }
}
