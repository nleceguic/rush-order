namespace RushOrder.Application.Common.Interfaces;

// A raw scored candidate coming out of one recommendation signal (top-selling,
// chef-recommended, co-occurrence, manual pairing). RecommendationService merges
// candidates from multiple signals into the final ranked list.
public sealed record RecommendationCandidate(
    Guid ProductId,
    string Name,
    string? ImageUrl,
    decimal Price,
    string Currency,
    decimal Weight);

public interface IRecommendationRepository
{
    // Completed (non-cancelled) order count for the restaurant — decides which
    // phase (cold start / intermediate) the engine runs in.
    Task<int> GetCompletedOrderCountAsync(Guid tenantId, Guid restaurantId, CancellationToken ct = default);

    // FASE COLD START — "Más pedidos hoy"
    Task<IReadOnlyList<RecommendationCandidate>> GetTopSellingTodayAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> excludeProductIds, int take, CancellationToken ct = default);

    // FASE COLD START — "El chef recomienda" (tag Popular o Recommended)
    Task<IReadOnlyList<RecommendationCandidate>> GetChefRecommendedAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> excludeProductIds, int take, CancellationToken ct = default);

    // FASE INTERMEDIA — co-occurrence matrix seeded by the cart's products.
    // Orders store their line items as a JSONB column (see Order.Items /
    // AppDbContext), not a normalized order_items table, so this is a
    // jsonb_array_elements-based adaptation of the classic co-occurrence join.
    Task<IReadOnlyList<RecommendationCandidate>> GetCoOccurringAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> cartProductIds, int take, CancellationToken ct = default);

    // Manual "maridaje" rules configured by the restaurant — highest priority, all phases.
    Task<IReadOnlyList<RecommendationCandidate>> GetManualPairingsAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> cartProductIds, CancellationToken ct = default);
}
