namespace RushOrder.Application.Analytics.DTOs;

public sealed record TablePerformanceDto(
    Guid TableId,
    string Name,
    string? Zone,
    decimal OccupancyPercent,
    double AvgTurnoverMinutes,
    decimal Revenue,
    int OrdersServed);
