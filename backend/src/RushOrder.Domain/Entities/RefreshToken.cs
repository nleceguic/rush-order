using System.Security.Cryptography;
using System.Text;
using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string CreatedByIp { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, string createdByIp, Guid familyId)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        FamilyId = familyId;
    }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string createdByIp,
        Guid? familyId = null)
        => new(userId, tokenHash, expiresAt, createdByIp, familyId ?? Guid.NewGuid());

    public static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    public void Revoke(string? replacedByTokenHash = null)
    {
        IsRevoked = true;
        ReplacedByTokenHash = replacedByTokenHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
