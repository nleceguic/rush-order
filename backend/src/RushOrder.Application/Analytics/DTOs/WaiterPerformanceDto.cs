namespace RushOrder.Application.Analytics.DTOs;

public sealed record WaiterPerformanceDto(
    Guid WaiterId,
    string Name,
    int OrdersServed,
    decimal? AvgRating,
    decimal Revenue,
    decimal AvgTicket);
