using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public static class MockDashboardData
{
    public static DashboardKpi Kpi() => new(
        RevenueToday: 1247.50m,
        RevenueYesterday: 1111.20m,
        RevenueByHour: [0m, 0m, 0m, 82m, 156m, 312m, 423m, 274m],
        OrdersWaiting: 3,
        OrdersPreparing: 5,
        OrdersReady: 2,
        TablesOccupied: 8,
        TablesTotal: 12,
        AvgOccupancyMinutes: 42.5,
        AvgTicketToday: 34.20m,
        AvgTicketYesterday: 33.40m);

    public static IReadOnlyList<AlertDto> Alerts() =>
    [
        new(Guid.NewGuid(), "Stock bajo: Vino Rioja Reserva (3 botellas)", AlertSeverity.Warning,
            null, "Product", DateTimeOffset.Now.AddMinutes(-14)),
        new(Guid.NewGuid(), "Cocina: Pedido #A-047 lleva +25 min en preparación", AlertSeverity.Critical,
            "A-047", "Order", DateTimeOffset.Now.AddMinutes(-7)),
        new(Guid.NewGuid(), "Reserva en 30 min — Mesa 4, García Martínez, 6 personas", AlertSeverity.Info,
            null, "Reservation", DateTimeOffset.Now.AddMinutes(-2)),
    ];

    public static IReadOnlyList<ReservationDto> Reservations() =>
    [
        new(Guid.NewGuid(), "García Martínez", 6, DateTimeOffset.Now.AddMinutes(30), "Cumpleaños"),
        new(Guid.NewGuid(), "Fernández López", 2, DateTimeOffset.Now.AddHours(1), null),
        new(Guid.NewGuid(), "Rodriguez & Co", 8, DateTimeOffset.Now.AddHours(1).AddMinutes(30), "Menú empresarial"),
    ];
}
