using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

// Manual "maridaje" rule configured by the restaurant, e.g. Croquetas -> Cerveza de la casa.
// Used by the recommendation engine at every data-maturity phase (cold start, intermediate,
// advanced) as the highest-priority signal, since it's an explicit merchandising decision.
public sealed class ProductPairingRule : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public Guid SourceProductId { get; private set; }
    public Guid TargetProductId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ProductPairingRule() { } // EF Core

    private ProductPairingRule(
        Guid tenantId,
        Guid restaurantId,
        Guid sourceProductId,
        Guid targetProductId) : base(tenantId)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        if (sourceProductId == Guid.Empty) throw new ArgumentException("SourceProductId cannot be empty.", nameof(sourceProductId));
        if (targetProductId == Guid.Empty) throw new ArgumentException("TargetProductId cannot be empty.", nameof(targetProductId));
        if (sourceProductId == targetProductId) throw new ArgumentException("A product cannot be paired with itself.", nameof(targetProductId));

        RestaurantId = restaurantId;
        SourceProductId = sourceProductId;
        TargetProductId = targetProductId;
    }

    public static ProductPairingRule Create(
        Guid tenantId, Guid restaurantId, Guid sourceProductId, Guid targetProductId)
        => new(tenantId, restaurantId, sourceProductId, targetProductId);

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
