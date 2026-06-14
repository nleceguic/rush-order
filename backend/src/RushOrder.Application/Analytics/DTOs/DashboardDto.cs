namespace RushOrder.Application.Analytics.DTOs;

public sealed record DashboardDto(
    RevenueStat Revenue,
    OrderStat Orders,
    CoverStat Covers,
    TicketStat AvgTicket,
    IReadOnlyList<TopProductDto> TopProducts,
    TableOccupancyStat TableOccupancy,
    IReadOnlyList<ActiveAlertDto> ActiveAlerts);

public sealed record RevenueStat(decimal Today, decimal Yesterday, decimal ChangePercent);

public sealed record OrderStat(int Total, int Pending, int InProgress, int Completed);

public sealed record CoverStat(int Total, decimal AvgPerTable);

public sealed record TicketStat(decimal Value, decimal ChangePercent);

public sealed record TopProductDto(string Name, int Quantity, decimal Revenue);

public sealed record TableOccupancyStat(decimal Percentage, double AvgDuration);

public sealed record ActiveAlertDto(string Type, string Message, string Severity);
