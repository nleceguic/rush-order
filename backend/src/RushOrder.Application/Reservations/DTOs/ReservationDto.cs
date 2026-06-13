namespace RushOrder.Application.Reservations.DTOs;

public record ReservationDto(
    Guid Id,
    Guid RestaurantId,
    Guid? TableId,
    string GuestName,
    string? GuestEmail,
    string GuestPhone,
    int PartySize,
    DateTimeOffset ReservedAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    string Status,
    string ConfirmationCode,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset CreatedAt);
