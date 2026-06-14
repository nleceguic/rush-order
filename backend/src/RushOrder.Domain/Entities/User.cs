using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class User : TenantEntity
{
    private readonly List<Guid> _restaurantIds = [];

    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public IReadOnlyList<Guid> RestaurantIds => _restaurantIds.AsReadOnly();
    public bool IsActive { get; private set; } = true;
    public string? MfaSecret { get; private set; }
    public string? PendingMfaSecret { get; private set; }
    public string? MfaBackupCodesJson { get; private set; }
    public bool MfaEnabled => MfaSecret is not null;
    public DateTimeOffset? LastLoginAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    private User() { } // EF Core

    private User(
        Guid tenantId,
        Email email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role) : base(tenantId)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Role = role;
    }

    public static User Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role)
        => new(tenantId, new Email(email), passwordHash, firstName, lastName, role);

    public void AddRestaurant(Guid restaurantId)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        if (!_restaurantIds.Contains(restaurantId))
            _restaurantIds.Add(restaurantId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveRestaurant(Guid restaurantId)
    {
        _restaurantIds.Remove(restaurantId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePasswordHash(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);
        PasswordHash = newHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnableMfa(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        MfaSecret = secret;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DisableMfa()
    {
        MfaSecret = null;
        PendingMfaSecret = null;
        MfaBackupCodesJson = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPendingMfaSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        PendingMfaSecret = secret;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ActivatePendingMfa(IReadOnlyList<string> backupCodeHashes)
    {
        if (PendingMfaSecret is null)
            throw new InvalidOperationException("No pending MFA secret to activate.");
        EnableMfa(PendingMfaSecret);
        PendingMfaSecret = null;
        MfaBackupCodesJson = System.Text.Json.JsonSerializer.Serialize(backupCodeHashes);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
