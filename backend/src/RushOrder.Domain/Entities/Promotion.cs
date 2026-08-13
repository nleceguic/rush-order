using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

// Free-text promotion (name + description), no product/category association and no
// discount rules yet — deliberately minimal for the first version. See ticket notes:
// segmentation, coupons, and pairing with specific products are out of scope for now.
public sealed class Promotion : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Promotion() { } // EF Core

    private Promotion(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string? description,
        DateTimeOffset startDate,
        DateTimeOffset endDate) : base(tenantId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (endDate < startDate)
            throw new ArgumentException("EndDate cannot be before StartDate.", nameof(endDate));

        RestaurantId = restaurantId;
        Name = name.Trim();
        Description = description?.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    public static Promotion Create(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string? description,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
        => new(tenantId, restaurantId, name, description, startDate, endDate);

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
