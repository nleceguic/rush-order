using FluentAssertions;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Domain.ValueObjects;
using RushOrder.Tests.Common.Builders;

namespace RushOrder.Domain.Tests.Orders;

public sealed class OrderTests
{
    // ── Create ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_OrderNumber_HasCorrectFormat()
    {
        var order = new OrderBuilder().Build();

        order.OrderNumber.Should().MatchRegex(@"^#\d{4}-\d{4}$");
    }

    [Fact]
    public void Create_RaisesOrderPlacedEvent()
    {
        var order = new OrderBuilder().Build();

        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedEvent);
    }

    [Fact]
    public void Create_WithSequenceNumber42_OrderNumberEnds0042()
    {
        var order = new OrderBuilder().WithSequenceNumber(42).Build();

        order.OrderNumber.Should().EndWith("-0042");
    }

    // ── AddItem ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddItem_SingleItem_RecalculatesTotalsWithTax()
    {
        // taxRate=10%, price=10 EUR, qty=2 → Subtotal=20, Tax=2, Total=22
        var order = new OrderBuilder().WithTaxRate(0.10m).Build();
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Agua", new Money(10m, "EUR"), 2);

        order.Subtotal.Amount.Should().Be(20m);
        order.TaxAmount.Amount.Should().Be(2m);
        order.Total.Amount.Should().Be(22m);
    }

    [Fact]
    public void AddItem_NegativeQuantity_ThrowsArgumentException()
    {
        var order = new OrderBuilder().Build();

        var act = () => order.AddItem(Guid.NewGuid(), "Item", new Money(5m, "EUR"), -1);

        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void AddItem_ZeroQuantity_ThrowsArgumentException()
    {
        var order = new OrderBuilder().Build();

        var act = () => order.AddItem(Guid.NewGuid(), "Item", new Money(5m, "EUR"), 0);

        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void AddItem_SameProductAndNotes_AccumulatesQuantity()
    {
        var order = new OrderBuilder().Build();
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Café", new Money(1.50m, "EUR"), 2, "sin azúcar");
        order.AddItem(productId, "Café", new Money(1.50m, "EUR"), 3, "sin azúcar");

        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public void AddItem_DifferentNotes_CreatesSeperateItems()
    {
        var order = new OrderBuilder().Build();
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Café", new Money(1.50m, "EUR"), 1, "con leche");
        order.AddItem(productId, "Café", new Money(1.50m, "EUR"), 1, "sin leche");

        order.Items.Should().HaveCount(2);
    }

    // ── Confirm ────────────────────────────────────────────────────────────────

    [Fact]
    public void Confirm_FromPending_StatusBecomesConfirmed()
    {
        var order = new OrderBuilder().Build();

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_FromPreparing_ThrowsInvalidOperationException()
    {
        var order = new OrderBuilder().Build();
        order.Confirm();
        order.StartPreparation();

        var act = () => order.Confirm();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Cancel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromPending_SetsCancellationReasonAndRaisesEvent()
    {
        var order = new OrderBuilder().Build();
        order.ClearDomainEvents();

        order.Cancel("Cliente se fue");

        order.CancellationReason.Should().Be("Cliente se fue");
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.OfType<OrderStatusChangedEvent>()
            .Should().ContainSingle(ev => ev.NewStatus == OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromPaid_ThrowsInvalidOperationException()
    {
        var order = new OrderBuilder().Build();
        order.Confirm();
        order.Pay();

        var act = () => order.Cancel("razón");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromCancelled_ThrowsInvalidOperationException()
    {
        var order = new OrderBuilder().Build();
        order.Cancel("primera vez");

        var act = () => order.Cancel("segunda vez");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Pay ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pay_FromServed_StatusBecomesPaid()
    {
        var order = new OrderBuilder().Build();
        order.Confirm();
        order.StartPreparation();
        order.MarkReady();
        order.MarkServed();

        order.Pay();

        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Pay_FromConfirmed_StatusBecomesPaid()
    {
        // Pay is allowed from Confirmed too (direct payment at ordering)
        var order = new OrderBuilder().Build();
        order.Confirm();

        order.Pay();

        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Pay_FromPending_ThrowsInvalidOperationException()
    {
        var order = new OrderBuilder().Build();

        var act = () => order.Pay();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── CalculateTotals ────────────────────────────────────────────────────────

    [Fact]
    public void CalculateTotals_WithTaxRate10Percent_CorrectTotals()
    {
        // 10 items at 10 EUR each, taxRate=10%
        var order = new OrderBuilder().WithTaxRate(0.10m).Build();
        order.AddItem(Guid.NewGuid(), "Producto", new Money(10m, "EUR"), 10);

        order.Subtotal.Amount.Should().Be(100m);
        order.TaxAmount.Amount.Should().Be(10m);
        order.Total.Amount.Should().Be(110m);
    }

    [Fact]
    public void CalculateTotals_WithDiscount_DiscountReducesTotal()
    {
        // 20 EUR subtotal, 10% tax = 2 EUR, discount = 5 EUR → Total = 17 EUR
        var order = new OrderBuilder().WithTaxRate(0.10m).Build();
        order.AddItem(Guid.NewGuid(), "Producto", new Money(20m, "EUR"), 1);
        order.ApplyDiscount(new Money(5m, "EUR"));

        order.Subtotal.Amount.Should().Be(20m);
        order.TaxAmount.Amount.Should().Be(2m);
        order.DiscountAmount.Amount.Should().Be(5m);
        order.Total.Amount.Should().Be(17m);
    }

    [Fact]
    public void CalculateTotals_MultipleItems_SumsAllLineTotals()
    {
        var order = new OrderBuilder().WithTaxRate(0m).Build();
        order.AddItem(Guid.NewGuid(), "A", new Money(5m, "EUR"), 2);
        order.AddItem(Guid.NewGuid(), "B", new Money(3m, "EUR"), 1);

        order.Subtotal.Amount.Should().Be(13m);
        order.Total.Amount.Should().Be(13m);
    }
}
