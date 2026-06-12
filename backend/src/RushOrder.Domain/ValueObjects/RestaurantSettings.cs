namespace RushOrder.Domain.ValueObjects;

public sealed record RestaurantSettings
{
    public bool OnlineOrderingEnabled { get; init; } = true;
    public bool TableOrderingEnabled { get; init; } = true;
    public bool ReservationsEnabled { get; init; } = true;
    public int MaxReservationPartySize { get; init; } = 20;
    public int ReservationSlotMinutes { get; init; } = 30;
    public string OpeningTime { get; init; } = "09:00";
    public string ClosingTime { get; init; } = "23:00";
    public bool AutoConfirmOrders { get; init; } = false;
    public bool PrintReceiptByDefault { get; init; } = true;
    public bool KitchenDisplayEnabled { get; init; } = false;
    public int OrderAlertAfterMinutes { get; init; } = 15;

    public static RestaurantSettings Default => new();
}
