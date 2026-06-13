namespace RushOrder.Application.Categories.DTOs;

public record CategoryDto(
    Guid Id,
    Guid RestaurantId,
    string Name,
    string? Description,
    string? ImageUrl,
    int SortOrder);
