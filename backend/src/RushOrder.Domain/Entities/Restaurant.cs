using RushOrder.Domain.Common;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Restaurant : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string? CoverUrl { get; private set; }
    public string Timezone { get; private set; } = "Europe/Madrid";
    public string Currency { get; private set; } = "EUR";
    public decimal TaxRate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public RestaurantSettings Settings { get; private set; } = RestaurantSettings.Default;
    public string? StripeAccountId { get; private set; }

    private Restaurant() { } // EF Core

    private Restaurant(
        Guid tenantId,
        string name,
        string address,
        string phone,
        string email,
        decimal taxRate,
        string timezone,
        string currency) : base(tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (taxRate < 0 || taxRate > 1)
            throw new ArgumentException("TaxRate must be between 0 and 1.", nameof(taxRate));

        Name = name.Trim();
        Address = address.Trim();
        Phone = phone.Trim();
        Email = email.Trim().ToLowerInvariant();
        TaxRate = taxRate;
        Timezone = string.IsNullOrWhiteSpace(timezone) ? "Europe/Madrid" : timezone;
        Currency = string.IsNullOrWhiteSpace(currency) ? "EUR" : currency.ToUpperInvariant();
    }

    public static Restaurant Create(
        Guid tenantId,
        string name,
        string address,
        string phone,
        string email,
        decimal taxRate = 0.21m,
        string timezone = "Europe/Madrid",
        string currency = "EUR")
        => new(tenantId, name, address, phone, email, taxRate, timezone, currency);

    public void UpdateProfile(string name, string address, string phone, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Address = address.Trim();
        Phone = phone.Trim();
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetImages(string? logoUrl, string? coverUrl)
    {
        LogoUrl = logoUrl;
        CoverUrl = coverUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSettings(RestaurantSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStripeAccountId(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        StripeAccountId = accountId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
