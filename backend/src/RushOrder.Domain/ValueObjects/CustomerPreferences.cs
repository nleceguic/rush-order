namespace RushOrder.Domain.ValueObjects;

public sealed record CustomerPreferences
{
    public IReadOnlyList<string> DietaryRestrictions { get; init; } = [];
    public IReadOnlyList<string> Allergies { get; init; } = [];
    public string PreferredLanguage { get; init; } = "es";
    public bool MarketingOptIn { get; init; } = false;
    public bool PushNotificationsEnabled { get; init; } = true;

    public static CustomerPreferences Default => new();
}
