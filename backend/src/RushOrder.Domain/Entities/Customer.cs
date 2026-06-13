using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Customer : TenantEntity
{
    public Email? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string DeviceFingerprint { get; private set; } = string.Empty;
    public int LoyaltyPoints { get; private set; }
    public LoyaltyLevel LoyaltyLevel { get; private set; } = LoyaltyLevel.Bronze;
    public CustomerPreferences Preferences { get; private set; } = CustomerPreferences.Default;
    public Money TotalSpent { get; private set; } = Money.Zero("EUR");

    public string DisplayName => FirstName is not null
        ? $"{FirstName} {LastName}".Trim()
        : Email?.Value ?? Phone ?? "Guest";

    private Customer() { } // EF Core

    private Customer(Guid tenantId, string deviceFingerprint, string currency) : base(tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFingerprint);
        DeviceFingerprint = deviceFingerprint;
        TotalSpent = Money.Zero(currency);
    }

    public static Customer Create(Guid tenantId, string deviceFingerprint, string currency = "EUR")
        => new(tenantId, deviceFingerprint, currency);

    public void UpdateProfile(string? firstName, string? lastName, string? email, string? phone)
    {
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        Email = email is not null ? new Email(email) : null;
        Phone = phone?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddLoyaltyPoints(int points)
    {
        if (points <= 0) throw new ArgumentException("Points must be positive.", nameof(points));
        LoyaltyPoints += points;
        UpdateLoyaltyLevel();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RedeemPoints(int points)
    {
        if (points <= 0) throw new ArgumentException("Points must be positive.", nameof(points));
        if (points > LoyaltyPoints)
            throw new InvalidOperationException($"Insufficient loyalty points. Available: {LoyaltyPoints}, requested: {points}.");
        LoyaltyPoints -= points;
        UpdateLoyaltyLevel();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddSpend(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        TotalSpent += amount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePreferences(CustomerPreferences preferences)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssociateDevice(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        DeviceFingerprint = fingerprint;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void UpdateLoyaltyLevel()
    {
        LoyaltyLevel = LoyaltyPoints switch
        {
            >= 5000 => LoyaltyLevel.Platinum,
            >= 2000 => LoyaltyLevel.Gold,
            >= 500  => LoyaltyLevel.Silver,
            _       => LoyaltyLevel.Bronze
        };
    }
}
