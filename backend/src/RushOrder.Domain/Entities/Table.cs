using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;

namespace RushOrder.Domain.Entities;

public sealed class Table : TenantEntity
{
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public string? Zone { get; private set; }
    public string QrCode { get; private set; } = string.Empty;
    public string QrUrl { get; private set; } = string.Empty;
    public TableStatus Status { get; private set; } = TableStatus.Free;
    public double? PositionX { get; private set; }
    public double? PositionY { get; private set; }

    private Table() { } // EF Core

    private Table(
        Guid tenantId,
        Guid restaurantId,
        string name,
        int capacity,
        string? zone,
        string baseUrl) : base(tenantId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        RestaurantId = restaurantId;
        Name = name.Trim();
        Capacity = capacity;
        Zone = zone?.Trim();
        QrCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        QrUrl = $"{baseUrl.TrimEnd('/')}/{QrCode}";
    }

    public static Table Create(
        Guid tenantId,
        Guid restaurantId,
        string name,
        int capacity,
        string? zone = null,
        string baseUrl = "https://menu.rushorder.app/t")
        => new(tenantId, restaurantId, name, capacity, zone, baseUrl);

    public void SetPosition(double x, double y)
    {
        PositionX = x;
        PositionY = y;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(TableStatus newStatus)
    {
        if (Status == newStatus) return;
        var previous = Status;
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TableStatusChangedEvent(Id, RestaurantId, newStatus));
    }

    public void MarkOccupied() => SetStatus(TableStatus.Occupied);
    public void MarkFree() => SetStatus(TableStatus.Free);
    public void MarkCleaning() => SetStatus(TableStatus.Cleaning);
    public void MarkReserved() => SetStatus(TableStatus.Reserved);

    public void UpdateDetails(string name, int capacity, string? zone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
        Name = name.Trim();
        Capacity = capacity;
        Zone = zone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
