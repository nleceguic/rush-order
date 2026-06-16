namespace RushOrder.Desktop.Models;

public sealed record DashboardKpi(
    decimal RevenueToday,
    decimal RevenueYesterday,
    IReadOnlyList<decimal> RevenueByHour,   // last 8 hours
    int OrdersWaiting,
    int OrdersPreparing,
    int OrdersReady,
    int TablesOccupied,
    int TablesTotal,
    double AvgOccupancyMinutes,
    decimal AvgTicketToday,
    decimal AvgTicketYesterday);

public sealed record AlertDto(
    Guid Id,
    string Message,
    AlertSeverity Severity,
    string? ResourceId,
    string ResourceType,
    DateTimeOffset OccurredAt);

public enum AlertSeverity { Info, Warning, Critical }

public sealed record ReservationDto(
    Guid Id,
    string CustomerName,
    int PartySize,
    DateTimeOffset ReservationTime,
    string? Notes);

public sealed record TableDto(
    Guid Id,
    int Number,
    int Capacity,
    TableState State,
    TableShapeType ShapeType,
    float X,
    float Y,
    float Width,
    float Height,
    bool HasPendingOrder,
    DateTimeOffset? OccupiedSince,
    string? CurrentWaiter);

public enum TableState     { Free, Occupied, Reserved, Cleaning }
public enum TableShapeType { Rectangular, Circular }

public sealed record ActiveOrderSummary(
    Guid   OrderId,
    string OrderNumber,
    int    ItemCount,
    decimal Total,
    string Status,
    DateTimeOffset PlacedAt);

public sealed record SavePositionRequest(Guid Id, float X, float Y);
