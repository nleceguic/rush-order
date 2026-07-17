using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Entities;

// Append-only event log for A/B test tracking. Not a TenantEntity because it's
// written from fully anonymous PWA requests (no JWT / tenant context yet) —
// RestaurantId is the isolation key here, resolved server-side from the
// public restaurantId the client already sends. TenantId is still stored
// (mirrors the other tenant-scoped tables' RLS setup) but resolved by the
// handler from the restaurant, not from ICurrentTenantService.
public sealed class ExperimentResult
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string ExperimentKey { get; private set; } = string.Empty;
    public ExperimentVariant Variant { get; private set; }
    public string DeviceFingerprint { get; private set; } = string.Empty;
    public ExperimentEventType EventType { get; private set; }
    public Guid? OrderId { get; private set; }
    public decimal? CartTotal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ExperimentResult() { } // EF Core

    private ExperimentResult(
        Guid tenantId,
        Guid restaurantId,
        string experimentKey,
        ExperimentVariant variant,
        string deviceFingerprint,
        ExperimentEventType eventType,
        Guid? orderId,
        decimal? cartTotal)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFingerprint);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        RestaurantId = restaurantId;
        ExperimentKey = experimentKey.Trim().ToLowerInvariant();
        Variant = variant;
        DeviceFingerprint = deviceFingerprint;
        EventType = eventType;
        OrderId = orderId;
        CartTotal = cartTotal;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static ExperimentResult Create(
        Guid tenantId,
        Guid restaurantId,
        string experimentKey,
        ExperimentVariant variant,
        string deviceFingerprint,
        ExperimentEventType eventType,
        Guid? orderId = null,
        decimal? cartTotal = null)
        => new(tenantId, restaurantId, experimentKey, variant, deviceFingerprint, eventType, orderId, cartTotal);
}
