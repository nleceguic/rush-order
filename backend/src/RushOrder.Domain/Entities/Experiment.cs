using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

// A/B test config. Bucket assignment itself (0-99, hashed from the device
// fingerprint) is computed on demand — see ExperimentBucketing — not persisted;
// this row only holds the variant split and whether the experiment is live.
public sealed class Experiment : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // Percentage (0-100) of buckets assigned to Variant B. The rest get Variant A.
    public int VariantBSplitPercent { get; private set; } = 50;
    public bool IsActive { get; private set; } = true;

    private Experiment() { } // EF Core

    private Experiment(
        Guid tenantId,
        Guid restaurantId,
        string key,
        string name,
        int variantBSplitPercent) : base(tenantId)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (variantBSplitPercent is < 0 or > 100)
            throw new ArgumentException("VariantBSplitPercent must be between 0 and 100.", nameof(variantBSplitPercent));

        RestaurantId = restaurantId;
        Key = key.Trim().ToLowerInvariant();
        Name = name.Trim();
        VariantBSplitPercent = variantBSplitPercent;
    }

    public static Experiment Create(
        Guid tenantId, Guid restaurantId, string key, string name, int variantBSplitPercent = 50, string? description = null)
        => new(tenantId, restaurantId, key, name, variantBSplitPercent) { Description = description?.Trim() };

    public void UpdateSplit(int variantBSplitPercent)
    {
        if (variantBSplitPercent is < 0 or > 100)
            throw new ArgumentException("VariantBSplitPercent must be between 0 and 100.", nameof(variantBSplitPercent));
        VariantBSplitPercent = variantBSplitPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

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
