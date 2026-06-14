using RushOrder.Domain.Common;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Plan : BaseEntity
{
    private readonly List<string> _features = [];

    public string Name { get; private set; } = string.Empty;
    public Money PriceMonthly { get; private set; } = Money.Zero("EUR");
    public Money PriceYearly { get; private set; } = Money.Zero("EUR");
    public int MaxTables { get; private set; }
    public int MaxUsers { get; private set; }
    public int MaxRestaurants { get; private set; }
    public IReadOnlyList<string> Features => _features.AsReadOnly();
    public bool IsActive { get; private set; } = true;
    public string? StripePriceIdMonthly { get; private set; }
    public string? StripePriceIdYearly { get; private set; }

    private Plan() { }

    private Plan(
        string name,
        Money priceMonthly,
        Money priceYearly,
        int maxTables,
        int maxUsers,
        int maxRestaurants,
        IEnumerable<string> features)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        PriceMonthly = priceMonthly ?? throw new ArgumentNullException(nameof(priceMonthly));
        PriceYearly = priceYearly ?? throw new ArgumentNullException(nameof(priceYearly));
        MaxTables = maxTables;
        MaxUsers = maxUsers;
        MaxRestaurants = maxRestaurants;
        _features.AddRange(features ?? []);
        IsActive = true;
    }

    public static Plan Create(
        string name,
        Money priceMonthly,
        Money priceYearly,
        int maxTables,
        int maxUsers,
        int maxRestaurants,
        IEnumerable<string> features)
        => new(name, priceMonthly, priceYearly, maxTables, maxUsers, maxRestaurants, features);

    public void SetStripePriceIds(string? monthlyId, string? yearlyId)
    {
        StripePriceIdMonthly = monthlyId;
        StripePriceIdYearly = yearlyId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasFeature(string feature)
        => _features.Contains(feature, StringComparer.OrdinalIgnoreCase);
}
