namespace RushOrder.Application.Restaurants.DTOs;

public record RestaurantSettingsDto(
    bool OnlineOrderingEnabled,
    bool TableOrderingEnabled,
    bool ReservationsEnabled,
    int MaxReservationPartySize,
    int ReservationSlotMinutes,
    string OpeningTime,
    string ClosingTime,
    bool AutoConfirmOrders,
    bool PrintReceiptByDefault,
    bool KitchenDisplayEnabled,
    int OrderAlertAfterMinutes);

public record RestaurantDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Address,
    string Phone,
    string Email,
    string? LogoUrl,
    string? CoverUrl,
    string Timezone,
    string Currency,
    decimal TaxRate,
    bool IsActive,
    RestaurantSettingsDto Settings,
    DateTimeOffset CreatedAt);
