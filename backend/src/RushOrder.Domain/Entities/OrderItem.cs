using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class OrderItem
{
    private readonly List<string> _modifiers = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyList<string> Modifiers => _modifiers.AsReadOnly();

    public Money LineTotal => UnitPrice * Quantity;

    private OrderItem() { } // EF Core

    internal OrderItem(
        Guid productId,
        string name,
        Money unitPrice,
        int quantity,
        string? notes,
        IEnumerable<string>? modifiers)
    {
        if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(unitPrice);
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        Id = Guid.NewGuid();
        ProductId = productId;
        Name = name.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
        Notes = notes?.Trim();
        if (modifiers is not null) _modifiers.AddRange(modifiers.Where(m => !string.IsNullOrWhiteSpace(m)));
    }

    internal void UpdateQuantity(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        Quantity = quantity;
    }
}
