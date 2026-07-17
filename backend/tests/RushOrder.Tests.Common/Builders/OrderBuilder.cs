using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Tests.Common.Builders;

public sealed class OrderBuilder
{
    private Guid   _tenantId      = TenantDefaults.TenantId;
    private Guid   _restaurantId  = TenantDefaults.RestaurantId;
    private Guid   _tableId       = TenantDefaults.TableId;
    private int    _sequenceNumber = 1;
    private OrderSource _source   = OrderSource.Manual;
    private decimal _taxRate      = 0.10m;
    private string  _currency     = "EUR";
    private string? _notes        = null;

    private readonly List<(Guid productId, string name, Money price, int qty, string? notes)> _items = [];

    public OrderBuilder WithTaxRate(decimal taxRate)      { _taxRate = taxRate; return this; }
    public OrderBuilder WithCurrency(string currency)     { _currency = currency; return this; }
    public OrderBuilder WithSequenceNumber(int seq)       { _sequenceNumber = seq; return this; }
    public OrderBuilder WithNotes(string notes)           { _notes = notes; return this; }
    public OrderBuilder WithSource(OrderSource source)    { _source = source; return this; }
    public OrderBuilder WithRestaurant(Guid restaurantId) { _restaurantId = restaurantId; return this; }

    public OrderBuilder WithItem(
        Guid?   productId = null,
        string? name      = null,
        Money?  price     = null,
        int     qty       = 1,
        string? notes     = null)
    {
        _items.Add((
            productId ?? Guid.NewGuid(),
            name ?? "Test Item",
            price ?? new Money(10m, _currency),
            qty,
            notes));
        return this;
    }

    public Order Build()
    {
        var order = Order.Create(
            _tenantId,
            _restaurantId,
            _tableId,
            _sequenceNumber,
            _source,
            _taxRate,
            _currency,
            notes: _notes);

        foreach (var (productId, name, price, qty, notes) in _items)
            order.AddItem(productId, name, price, qty, notes);

        return order;
    }
}
