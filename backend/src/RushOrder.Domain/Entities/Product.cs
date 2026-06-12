using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Product : TenantEntity
{
    private readonly List<string> _allergens = [];
    private readonly List<ProductTag> _tags = [];

    public Guid CategoryId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Price { get; private set; } = null!;
    public string? ImageUrl { get; private set; }
    public IReadOnlyList<string> Allergens => _allergens.AsReadOnly();
    public IReadOnlyList<ProductTag> Tags => _tags.AsReadOnly();
    public bool IsAvailable { get; private set; } = true;
    public bool StockTracking { get; private set; }
    public int? StockQuantity { get; private set; }
    public int SortOrder { get; private set; }
    public int PreparationMinutes { get; private set; }

    private Product() { } // EF Core

    private Product(
        Guid tenantId,
        Guid restaurantId,
        Guid categoryId,
        string name,
        Money price,
        int preparationMinutes) : base(tenantId)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        if (categoryId == Guid.Empty) throw new ArgumentException("CategoryId cannot be empty.", nameof(categoryId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(price);
        if (preparationMinutes < 0) throw new ArgumentException("PreparationMinutes cannot be negative.", nameof(preparationMinutes));

        RestaurantId = restaurantId;
        CategoryId = categoryId;
        Name = name.Trim();
        Price = price;
        PreparationMinutes = preparationMinutes;
    }

    public static Product Create(
        Guid tenantId,
        Guid restaurantId,
        Guid categoryId,
        string name,
        Money price,
        int preparationMinutes = 10)
        => new(tenantId, restaurantId, categoryId, name, price, preparationMinutes);

    public void UpdateDetails(string name, string? description, Money price, int preparationMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(price);
        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        PreparationMinutes = preparationMinutes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAvailability(bool isAvailable)
    {
        if (IsAvailable == isAvailable) return;
        IsAvailable = isAvailable;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new ProductAvailabilityChangedEvent(Id, RestaurantId, isAvailable));
    }

    public void EnableStockTracking(int initialQuantity)
    {
        if (initialQuantity < 0) throw new ArgumentException("Initial quantity cannot be negative.", nameof(initialQuantity));
        StockTracking = true;
        StockQuantity = initialQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AdjustStock(int delta)
    {
        if (!StockTracking)
            throw new InvalidOperationException("Stock tracking is not enabled for this product.");
        var newQty = (StockQuantity ?? 0) + delta;
        if (newQty < 0) throw new InvalidOperationException("Stock quantity cannot go below zero.");
        StockQuantity = newQty;
        if (StockQuantity == 0) SetAvailability(false);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddAllergen(string allergen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allergen);
        if (!_allergens.Contains(allergen)) _allergens.Add(allergen.Trim());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveAllergen(string allergen)
    {
        _allergens.Remove(allergen);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddTag(ProductTag tag)
    {
        if (!_tags.Contains(tag)) _tags.Add(tag);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveTag(ProductTag tag)
    {
        _tags.Remove(tag);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSortOrder(int order)
    {
        SortOrder = order;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetImage(string? imageUrl)
    {
        ImageUrl = imageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
