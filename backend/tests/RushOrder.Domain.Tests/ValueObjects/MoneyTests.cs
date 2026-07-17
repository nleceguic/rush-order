using FluentAssertions;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Addition_SameCurrency_ReturnsSum()
    {
        var result = new Money(10m, "EUR") + new Money(5m, "EUR");

        result.Amount.Should().Be(15m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Addition_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var act = () => new Money(10m, "EUR") + new Money(5m, "USD");

        act.Should().Throw<InvalidOperationException>().WithMessage("*currencies*");
    }

    [Fact]
    public void IsZero_ZeroAmount_ReturnsTrue()
    {
        new Money(0m, "EUR").IsZero.Should().BeTrue();
    }

    [Fact]
    public void IsZero_NonZeroAmount_ReturnsFalse()
    {
        new Money(0.01m, "EUR").IsZero.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NegativeAmount_ThrowsArgumentException()
    {
        var act = () => new Money(-1m, "EUR");

        act.Should().Throw<ArgumentException>().WithMessage("*negative*");
    }

    [Fact]
    public void Constructor_EmptyCurrency_ThrowsArgumentException()
    {
        var act = () => new Money(10m, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhitespaceCurrency_ThrowsArgumentException()
    {
        var act = () => new Money(10m, "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NormalisesToUppercase()
    {
        var money = new Money(5m, "eur");

        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Subtraction_SameCurrency_ReturnsCorrectResult()
    {
        var result = new Money(10m, "EUR") - new Money(3m, "EUR");

        result.Amount.Should().Be(7m);
    }

    [Fact]
    public void Subtraction_WouldBeNegative_ThrowsInvalidOperationException()
    {
        var act = () => new Money(3m, "EUR") - new Money(10m, "EUR");

        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }

    [Fact]
    public void Zero_Factory_ReturnsZeroMoney()
    {
        var zero = Money.Zero("USD");

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("USD");
        zero.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Multiplication_ReturnsCorrectResult()
    {
        var result = new Money(10m, "EUR") * 3m;

        result.Amount.Should().Be(30m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        new Money(10m, "EUR").Should().NotBe(new Money(11m, "EUR"));
    }
}
