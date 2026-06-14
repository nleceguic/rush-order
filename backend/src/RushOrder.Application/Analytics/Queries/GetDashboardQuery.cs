using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Analytics.Queries;

public record GetDashboardQuery(Guid RestaurantId, DateOnly? Date = null) : IQuery<DashboardDto>;

public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GetDashboardQueryHandler(IAnalyticsRepository analytics, IDistributedCache cache)
    {
        _analytics = analytics;
        _cache = cache;
    }

    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var cacheKey = $"analytics:dashboard:{request.RestaurantId}:{date:yyyy-MM-dd}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<DashboardDto>(cached, JsonOpts)!;

        var result = await _analytics.GetDashboardAsync(request.RestaurantId, date, cancellationToken);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);

        return result;
    }
}
