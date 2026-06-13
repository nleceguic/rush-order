namespace RushOrder.Application.Tables.DTOs;

public record TableFloorPlanDto(
    Guid Id,
    string Name,
    int Capacity,
    string? Zone,
    string Status,
    double? PositionX,
    double? PositionY,
    int ActiveOrderCount,
    string? CurrentOrderNumber);
