using RushOrder.Application.Recommendations.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public interface IRecommendationService
{
    // FASE COLD START (<100 pedidos): reglas de negocio (más vendido hoy, tag chef,
    // maridaje manual). FASE INTERMEDIA (100-1000): + co-occurrence SQL sobre el
    // carrito. FASE AVANZADA (>1000): ver docs/product/recommendations-ml-plan.md —
    // hasta que exista ese modelo, cae de vuelta en la fase intermedia.
    Task<IReadOnlyList<ProductRecommendationDto>> GetRecommendationsAsync(
        Guid restaurantId,
        Guid? customerId,
        IReadOnlyCollection<Guid> currentCartProductIds,
        CancellationToken ct = default);
}
