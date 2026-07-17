using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Recommendations.DTOs;

namespace RushOrder.Application.Recommendations.Queries;

public record GetRecommendationsQuery(
    Guid RestaurantId,
    Guid? CustomerId,
    IReadOnlyList<Guid> CartProductIds) : IQuery<IReadOnlyList<ProductRecommendationDto>>;

public sealed class GetRecommendationsQueryHandler
    : IRequestHandler<GetRecommendationsQuery, IReadOnlyList<ProductRecommendationDto>>
{
    private readonly IRecommendationService _recommendationService;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GetRecommendationsQueryHandler(IRecommendationService recommendationService, IDistributedCache cache)
    {
        _recommendationService = recommendationService;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ProductRecommendationDto>> Handle(
        GetRecommendationsQuery request, CancellationToken cancellationToken)
    {
        // Cache varies by restaurant + cart signature only — not by customer
        // ("Cache Redis: TTL 5 minutos, varía por restaurante, no por cliente en esta fase").
        var cartSignature = request.CartProductIds.Count == 0
            ? "empty"
            : string.Join(",", request.CartProductIds.OrderBy(id => id));
        var cacheKey = $"recommendations:{request.RestaurantId}:{cartSignature}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<ProductRecommendationDto>>(cached, JsonOpts)!;

        var result = await _recommendationService.GetRecommendationsAsync(
            request.RestaurantId, request.CustomerId, request.CartProductIds, cancellationToken);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);

        return result;
    }
}
