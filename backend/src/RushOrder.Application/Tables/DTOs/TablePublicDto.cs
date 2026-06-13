namespace RushOrder.Application.Tables.DTOs;

public record TablePublicDto(
    Guid Id,
    string Name,
    int Capacity,
    string? Zone,
    Guid RestaurantId,
    string RestaurantName,
    string Currency);
