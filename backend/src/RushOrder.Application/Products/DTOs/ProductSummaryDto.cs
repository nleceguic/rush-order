namespace RushOrder.Application.Products.DTOs;

public record ProductSummaryDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string? ImageUrl,
    bool IsAvailable,
    int SortOrder,
    int PreparationMinutes);
