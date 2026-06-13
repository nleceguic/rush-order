using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Entities;

public sealed class Reservation : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public Guid? TableId { get; private set; }
    public string GuestName { get; private set; } = string.Empty;
    public string? GuestEmail { get; private set; }
    public string GuestPhone { get; private set; } = string.Empty;
    public int PartySize { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public int DurationMinutes { get; private set; } = 90;
    public ReservationStatus Status { get; private set; } = ReservationStatus.Pending;
    public string ConfirmationCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? PreOrder { get; private set; }
    public string? CancellationReason { get; private set; }

    public DateTimeOffset EndsAt => ReservedAt.AddMinutes(DurationMinutes);

    private Reservation() { }

    private Reservation(
        Guid tenantId,
        Guid restaurantId,
        string guestName,
        string? guestEmail,
        string guestPhone,
        int partySize,
        DateTimeOffset reservedAt,
        string confirmationCode,
        string? notes,
        int durationMinutes) : base(tenantId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(guestName);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationCode);
        if (partySize <= 0)
            throw new ArgumentException("PartySize must be greater than zero.", nameof(partySize));

        RestaurantId = restaurantId;
        GuestName = guestName.Trim();
        GuestEmail = guestEmail?.Trim();
        GuestPhone = guestPhone.Trim();
        PartySize = partySize;
        ReservedAt = reservedAt;
        DurationMinutes = durationMinutes > 0 ? durationMinutes : 90;
        ConfirmationCode = confirmationCode.ToUpperInvariant();
        Notes = notes?.Trim();
    }

    public static Reservation Create(
        Guid tenantId,
        Guid restaurantId,
        string guestName,
        string? guestEmail,
        string guestPhone,
        int partySize,
        DateTimeOffset reservedAt,
        string confirmationCode,
        string? notes = null,
        int durationMinutes = 90)
        => new(tenantId, restaurantId, guestName, guestEmail, guestPhone,
               partySize, reservedAt, confirmationCode, notes, durationMinutes);

    public void Confirm()
    {
        if (Status != ReservationStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm a reservation with status {Status}.");
        Status = ReservationStatus.Confirmed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Seat(Guid tableId)
    {
        if (Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            throw new InvalidOperationException($"Cannot seat a reservation with status {Status}.");
        if (tableId == Guid.Empty)
            throw new ArgumentException("TableId cannot be empty.", nameof(tableId));
        TableId = tableId;
        Status = ReservationStatus.Seated;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status is ReservationStatus.Seated or ReservationStatus.NoShow or ReservationStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a reservation with status {Status}.");
        Status = ReservationStatus.Cancelled;
        CancellationReason = reason?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkNoShow()
    {
        if (Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            throw new InvalidOperationException($"Cannot mark no-show for a reservation with status {Status}.");
        Status = ReservationStatus.NoShow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
