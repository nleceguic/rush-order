namespace RushOrder.Application.Menu.DTOs;

public record RestaurantInfoDto(
    string Name,
    string? Logo,
    string? CoverImage,
    string? WelcomeMessage);

public record PublicMenuProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string? ImageUrl,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Tags,
    bool IsAvailable,
    int PreparationMinutes);

public record PublicMenuCategoryDto(
    Guid Id,
    string Name,
    string? Image,
    IReadOnlyList<PublicMenuProductDto> Products);

public record PromotionDto(Guid Id, string Name, string Description);

public record PublicMenuDto(
    Guid RestaurantId,
    RestaurantInfoDto Restaurant,
    IReadOnlyList<PublicMenuCategoryDto> Categories,
    IReadOnlyList<PromotionDto> ActivePromotions);
