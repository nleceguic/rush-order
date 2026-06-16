namespace RushOrder.Desktop.Models;

public sealed record CategoryDto(
    Guid   Id,
    string Name,
    int    SortOrder,
    bool   IsActive,
    int    ProductCount,
    int    UnavailableCount);

public sealed record MenuProductDto(
    Guid    Id,
    string  Name,
    Guid    CategoryId,
    string  CategoryName,
    decimal Price,
    bool    IsAvailable,
    string? ImageUrl,
    string? Description,
    int     PrepTimeMinutes,
    IReadOnlyList<string>         Allergens,
    IReadOnlyList<string>         Tags,
    bool    TrackStock,
    int?    StockQuantity,
    int?    StockAlertThreshold,
    IReadOnlyList<ProductVariant> Variants,
    IReadOnlyList<ModifierGroup>  ModifierGroups,
    int     SortOrder,
    string? InternalRef,
    string? InternalNotes)
{
    public bool IsLowStock => TrackStock && StockQuantity.HasValue &&
                              StockAlertThreshold.HasValue &&
                              StockQuantity.Value <= StockAlertThreshold.Value;
}

public sealed record ProductVariant(
    Guid    Id,
    string  Name,
    decimal ExtraPrice,
    bool    IsActive);

public sealed record ModifierGroup(
    Guid   Id,
    string Name,
    bool   Required,
    int    MinSelections,
    int    MaxSelections,
    IReadOnlyList<ModifierOption> Options);

public sealed record ModifierOption(
    Guid    Id,
    string  Name,
    decimal ExtraPrice,
    bool    IsActive);

public sealed record SaveProductRequest(
    Guid?   Id,
    string  Name,
    Guid    CategoryId,
    decimal Price,
    bool    IsAvailable,
    string? Description,
    int     PrepTimeMinutes,
    IReadOnlyList<string>         Allergens,
    IReadOnlyList<string>         Tags,
    bool    TrackStock,
    int?    StockQuantity,
    int?    StockAlertThreshold,
    IReadOnlyList<ProductVariant> Variants,
    IReadOnlyList<ModifierGroup>  ModifierGroups,
    int     SortOrder,
    string? InternalRef,
    string? InternalNotes);

public sealed record SaveCategoryRequest(
    Guid?  Id,
    string Name,
    int    SortOrder,
    bool   IsActive);

public static class MenuConstants
{
    public static readonly string[] Allergens14 =
    [
        "Gluten", "Crustáceos", "Huevos", "Pescado", "Cacahuetes",
        "Soja", "Lácteos", "Frutos secos", "Apio", "Mostaza",
        "Sésamo", "Sulfitos", "Altramuces", "Moluscos",
    ];

    public static readonly string[] DietaryTags =
    [
        "Vegetariano", "Vegano", "Sin Gluten", "Sin Lactosa",
        "Picante", "Nuevo", "Popular", "Sin Azúcar",
    ];
}
