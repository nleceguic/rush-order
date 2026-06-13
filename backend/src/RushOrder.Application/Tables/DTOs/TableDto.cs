namespace RushOrder.Application.Tables.DTOs;

public record TableDto(
    Guid Id,
    Guid RestaurantId,
    string Name,
    int Capacity,
    string? Zone,
    string QrCode,
    string QrUrl,
    string Status,
    double? PositionX,
    double? PositionY,
    DateTimeOffset CreatedAt);
