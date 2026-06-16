namespace RushOrder.Desktop.Models;

public sealed record HourlyRevenuePoint(int Hour, decimal Revenue);
public sealed record TopProductPoint(string Name, int Quantity, decimal Revenue);
public sealed record PaymentMethodPoint(string Method, int Count, decimal Amount);
public sealed record WaiterStatsRow(string Name, int Orders, decimal Revenue, double AvgMinutes, decimal AvgTicket);

public sealed record StatisticsDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<HourlyRevenuePoint> HourlyRevenue,
    IReadOnlyList<TopProductPoint>    TopProducts,
    IReadOnlyList<PaymentMethodPoint> PaymentMethods,
    IReadOnlyList<WaiterStatsRow>     WaiterStats,
    decimal TotalRevenue,
    int     TotalOrders);
