using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class RedisAuthCacheService : IAuthCacheService
{
    private readonly IDistributedCache _cache;

    public RedisAuthCacheService(IDistributedCache cache) => _cache = cache;

    public async Task BlockJtiAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
        => await _cache.SetStringAsync(JtiKey(jti), "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);

    public async Task<bool> IsJtiBlockedAsync(string jti, CancellationToken ct = default)
        => await _cache.GetStringAsync(JtiKey(jti), ct) is not null;

    public async Task<string> StoreMfaPendingAsync(Guid userId, CancellationToken ct = default)
    {
        var token = GenerateToken(32);
        await _cache.SetStringAsync(MfaKey(token), userId.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
        return token;
    }

    public async Task<Guid?> GetMfaPendingUserIdAsync(string tempToken, CancellationToken ct = default)
    {
        var value = await _cache.GetStringAsync(MfaKey(tempToken), ct);
        return value is not null && Guid.TryParse(value, out var id) ? id : null;
    }

    public Task RemoveMfaPendingAsync(string tempToken, CancellationToken ct = default)
        => _cache.RemoveAsync(MfaKey(tempToken), ct);

    public async Task<string> StorePasswordResetAsync(Guid userId, CancellationToken ct = default)
    {
        var rawToken = GenerateToken(32);
        var hash = HashToken(rawToken);
        await _cache.SetStringAsync(PasswordResetKey(hash), userId.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, ct);
        return rawToken;
    }

    public async Task<Guid?> GetPasswordResetUserIdAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        var value = await _cache.GetStringAsync(PasswordResetKey(hash), ct);
        return value is not null && Guid.TryParse(value, out var id) ? id : null;
    }

    public Task RemovePasswordResetAsync(string rawToken, CancellationToken ct = default)
        => _cache.RemoveAsync(PasswordResetKey(HashToken(rawToken)), ct);

    private static string GenerateToken(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string JtiKey(string jti) => $"jti:{jti}";
    private static string MfaKey(string token) => $"mfa:{token}";
    private static string PasswordResetKey(string hash) => $"pwdreset:{hash}";
}
