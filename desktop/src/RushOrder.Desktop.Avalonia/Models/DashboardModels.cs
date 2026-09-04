namespace RushOrder.Desktop.Avalonia.Models;

public sealed record DashboardKpi(
    decimal RevenueToday,
    decimal RevenueYesterday,
    IReadOnlyList<decimal> RevenueByHour,
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
