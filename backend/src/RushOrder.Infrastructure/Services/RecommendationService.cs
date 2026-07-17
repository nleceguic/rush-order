using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Recommendations.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Services;

// 3-phase recommendation engine (see docs/product/recommendations-ml-plan.md for
// the FASE AVANZADA / ML plan):
//
//   FASE COLD START  (<100 pedidos)   -> reglas de negocio: más vendido hoy + tag chef
//   FASE INTERMEDIA  (100-1000)       -> + collaborative filtering (co-occurrence SQL)
//   FASE AVANZADA    (>1000)          -> Azure ML / LightGBM — no implementado todavía,
//                                         cae de vuelta en la fase intermedia hasta entonces.
//
// Reglas manuales de maridaje (ProductPairingRule) tienen prioridad máxima en
// todas las fases, seguidas de la señal específica de la fase, y se completa
// con "más vendido hoy" / "el chef recomienda" hasta llegar a MaxRecommendations.
//
// This is called from the fully anonymous GET /recommendations endpoint, so it
// resolves restaurantId -> tenantId itself (mirrors GetPublicMenuQueryHandler)
// instead of requiring a pre-authenticated tenant context.
public sealed class RecommendationService : IRecommendationService
{
    private const int MaxRecommendations = 6;
    private const int ColdStartOrderThreshold = 100;

    private static class Reasons
    {
        public const string TopSellingToday = "Muy pedido hoy";
        public const string PerfectMatch = "Perfecto con tu selección";
        public const string ChefRecommends = "El chef recomienda";
        public const string AlsoOrderedByOthers = "Los clientes también piden";
    }

    private readonly IRecommendationRepository _repository;
    private readonly IRestaurantRepository _restaurants;

    public RecommendationService(IRecommendationRepository repository, IRestaurantRepository restaurants)
    {
        _repository = repository;
        _restaurants = restaurants;
    }

    public async Task<IReadOnlyList<ProductRecommendationDto>> GetRecommendationsAsync(
        Guid restaurantId,
        Guid? customerId,
        IReadOnlyCollection<Guid> currentCartProductIds,
        CancellationToken ct = default)
    {
        var restaurant = await _restaurants.GetByIdAsync(restaurantId, ct)
            ?? throw new NotFoundException(nameof(Restaurant), restaurantId);
        var tenantId = restaurant.TenantId;

        var ranked = new List<(RecommendationCandidate Candidate, string Reason, decimal PriorityBoost)>();
        var seen = new HashSet<Guid>(currentCartProductIds);

        void AddCandidates(IEnumerable<RecommendationCandidate> candidates, string reason, decimal priorityBoost)
        {
            foreach (var candidate in candidates)
            {
                if (!seen.Add(candidate.ProductId)) continue; // dedupe: first signal to claim a product wins
                ranked.Add((candidate, reason, priorityBoost));
            }
        }

        // Manual pairing rules — highest priority, every phase.
        if (currentCartProductIds.Count > 0)
        {
            var pairings = await _repository.GetManualPairingsAsync(tenantId, restaurantId, currentCartProductIds, ct);
            AddCandidates(pairings, Reasons.PerfectMatch, priorityBoost: 1000m);
        }

        var orderCount = await _repository.GetCompletedOrderCountAsync(tenantId, restaurantId, ct);

        if (orderCount >= ColdStartOrderThreshold && currentCartProductIds.Count > 0)
        {
            // FASE INTERMEDIA (and, until the ML model exists, FASE AVANZADA too):
            // collaborative filtering seeded by the cart.
            var coOccurring = await _repository.GetCoOccurringAsync(
                tenantId, restaurantId, currentCartProductIds, MaxRecommendations, ct);
            AddCandidates(coOccurring, Reasons.AlsoOrderedByOthers, priorityBoost: 100m);
        }

        // FASE COLD START signals also serve as the fallback/fill for every other
        // phase once cart-aware signals run out.
        if (ranked.Count < MaxRecommendations)
        {
            var topToday = await _repository.GetTopSellingTodayAsync(
                tenantId, restaurantId, seen, MaxRecommendations - ranked.Count, ct);
            AddCandidates(topToday, Reasons.TopSellingToday, priorityBoost: 10m);
        }

        if (ranked.Count < MaxRecommendations)
        {
            var chefRecommended = await _repository.GetChefRecommendedAsync(
                tenantId, restaurantId, seen, MaxRecommendations - ranked.Count, ct);
            AddCandidates(chefRecommended, Reasons.ChefRecommends, priorityBoost: 1m);
        }

        return ranked
            .OrderByDescending(r => r.PriorityBoost + r.Candidate.Weight)
            .Take(MaxRecommendations)
            .Select(r => new ProductRecommendationDto(
                r.Candidate.ProductId,
                r.Candidate.Name,
                r.Candidate.ImageUrl,
                r.Candidate.Price,
                r.Candidate.Currency,
                r.Reason,
                ConfidenceScore: Math.Min(1m, (r.PriorityBoost + r.Candidate.Weight) / 1000m)))
            .ToList()
            .AsReadOnly();
    }
}
