using System.Text.RegularExpressions;

namespace RushOrder.Domain.ValueObjects;

public sealed record Email
{
    private static readonly Regex Pattern = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    public string Value { get; }

    private Email() => Value = string.Empty; // EF Core

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalised = value.Trim().ToLowerInvariant();
        if (!Pattern.IsMatch(normalised))
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
        Value = normalised;
    }

    public static implicit operator Email(string value) => new(value);
    public static implicit operator string(Email email) => email.Value;

    public override string ToString() => Value;
}
