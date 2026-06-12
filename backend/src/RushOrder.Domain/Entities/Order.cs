using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Order : TenantEntity
{
    private readonly List<OrderItem> _items = [];

    public Guid RestaurantId { get; private set; }
    public Guid TableId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? WaiterId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public OrderSource Source { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public Money Subtotal { get; private set; } = null!;
    public Money TaxAmount { get; private set; } = null!;
    public Money DiscountAmount { get; private set; } = null!;
    public Money TipAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTimeOffset? EstimatedReadyAt { get; private set; }
    public decimal TaxRate { get; private set; }
    public string? CancellationReason { get; private set; }

    private Order() { } // EF Core

    private Order(
        Guid tenantId,
        Guid restaurantId,
        Guid tableId,
        Guid? customerId,
        Guid? waiterId,
        string orderNumber,
        OrderSource source,
        decimal taxRate,
        string currency) : base(tenantId)
    {
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        if (tableId == Guid.Empty) throw new ArgumentException("TableId cannot be empty.", nameof(tableId));
        if (taxRate < 0 || taxRate > 1) throw new ArgumentException("TaxRate must be between 0 and 1.", nameof(taxRate));

        RestaurantId = restaurantId;
        TableId = tableId;
        CustomerId = customerId;
        WaiterId = waiterId;
        OrderNumber = orderNumber;
        Source = source;
        TaxRate = taxRate;

        Subtotal = Money.Zero(currency);
        TaxAmount = Money.Zero(currency);
        DiscountAmount = Money.Zero(currency);
        TipAmount = Money.Zero(currency);
        Total = Money.Zero(currency);
    }

    public static Order Create(
        Guid tenantId,
        Guid restaurantId,
        Guid tableId,
        int sequenceNumber,
        OrderSource source,
        decimal taxRate = 0.21m,
        string currency = "EUR",
        Guid? customerId = null,
        Guid? waiterId = null,
        string? notes = null)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var orderNumber = $"#{year}-{sequenceNumber:D4}";
        var order = new Order(tenantId, restaurantId, tableId, customerId, waiterId, orderNumber, source, taxRate, currency)
        {
            Notes = notes?.Trim()
        };
        order.RaiseDomainEvent(new OrderPlacedEvent(order.Id, tableId, restaurantId, orderNumber));
        return order;
    }

    // --- State transitions ---

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending);
        SetStatus(OrderStatus.Confirmed);
    }

    public void StartPreparation()
    {
        EnsureStatus(OrderStatus.Confirmed);
        SetStatus(OrderStatus.Preparing);
    }

    public void MarkReady(DateTimeOffset? estimatedAt = null)
    {
        EnsureStatus(OrderStatus.Preparing);
        EstimatedReadyAt = estimatedAt;
        SetStatus(OrderStatus.Ready);
    }

    public void MarkServed()
    {
        EnsureStatus(OrderStatus.Ready);
        SetStatus(OrderStatus.Served);
    }

    public void Pay()
    {
        if (Status != OrderStatus.Served && Status != OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot pay an order in status {Status}.");
        SetStatus(OrderStatus.Paid);
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel an order in status {Status}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        CancellationReason = reason;
        SetStatus(OrderStatus.Cancelled);
    }

    // --- Item management ---

    public OrderItem AddItem(
        Guid productId,
        string name,
        Money unitPrice,
        int quantity,
        string? notes = null,
        IEnumerable<string>? modifiers = null)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot add items to an order in status {Status}.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId && i.Notes == notes);
        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
            CalculateTotals();
            UpdatedAt = DateTimeOffset.UtcNow;
            return existing;
        }

        var item = new OrderItem(productId, name, unitPrice, quantity, notes, modifiers);
        _items.Add(item);
        CalculateTotals();
        UpdatedAt = DateTimeOffset.UtcNow;
        return item;
    }

    public void RemoveItem(Guid orderItemId)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot remove items from an order in status {Status}.");

        var item = _items.FirstOrDefault(i => i.Id == orderItemId)
            ?? throw new InvalidOperationException($"Item {orderItemId} not found in order.");
        _items.Remove(item);
        CalculateTotals();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTip(Money tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        TipAmount = tip;
        CalculateTotals();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyDiscount(Money discount)
    {
        ArgumentNullException.ThrowIfNull(discount);
        DiscountAmount = discount;
        CalculateTotals();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CalculateTotals()
    {
        var currency = Subtotal.Currency;
        Subtotal = _items.Aggregate(
            Money.Zero(currency),
            (acc, item) => acc + item.LineTotal);
        TaxAmount = new Money(Math.Round(Subtotal.Amount * TaxRate, 2), currency);
        Total = Subtotal + TaxAmount - DiscountAmount + TipAmount;
    }

    // --- Private helpers ---

    private void SetStatus(OrderStatus newStatus)
    {
        var previous = Status;
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new OrderStatusChangedEvent(Id, previous, newStatus));
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Expected order status {expected}, but was {Status}.");
    }
}
