namespace RushOrder.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public bool IsZero => Amount == 0m;

    private Money() => (Amount, Currency) = (0, "EUR"); // EF Core

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount = decimal.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        if (a.Amount < b.Amount)
            throw new InvalidOperationException("Subtraction would result in negative money.");
        return new(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
        => new(money.Amount * multiplier, money.Currency);

    public static bool operator >(Money a, Money b) { EnsureSameCurrency(a, b); return a.Amount > b.Amount; }
    public static bool operator <(Money a, Money b) { EnsureSameCurrency(a, b); return a.Amount < b.Amount; }
    public static bool operator >=(Money a, Money b) { EnsureSameCurrency(a, b); return a.Amount >= b.Amount; }
    public static bool operator <=(Money a, Money b) { EnsureSameCurrency(a, b); return a.Amount <= b.Amount; }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on different currencies: {a.Currency} and {b.Currency}.");
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
