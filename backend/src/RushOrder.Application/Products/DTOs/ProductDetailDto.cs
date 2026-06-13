namespace RushOrder.Application.Products.DTOs;

public record ProductDetailDto(
    Guid Id,
    Guid CategoryId,
    Guid RestaurantId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string? ImageUrl,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Tags,
    bool IsAvailable,
    bool StockTracking,
    int? StockQuantity,
    int SortOrder,
    int PreparationMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
