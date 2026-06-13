using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

public sealed class Category : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsDeleted { get; private set; }

    private Category() { }

    private Category(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string? description,
        string? imageUrl,
        int sortOrder) : base(tenantId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        RestaurantId = restaurantId;
        Name = name.Trim();
        Description = description?.Trim();
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
    }

    public static Category Create(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string? description = null,
        string? imageUrl = null,
        int sortOrder = 0)
        => new(tenantId, restaurantId, name, description, imageUrl, sortOrder);

    public void UpdateDetails(string name, string? description, string? imageUrl, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
