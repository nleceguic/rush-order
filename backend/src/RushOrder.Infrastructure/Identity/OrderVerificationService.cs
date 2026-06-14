using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Identity;

public sealed class OrderVerificationService : IOrderVerificationService
{
    private readonly byte[] _keyBytes;

    public OrderVerificationService(IConfiguration configuration)
    {
        var secret = configuration["OrderTracking:TokenSecret"]
            ?? configuration["Jwt:Issuer"]   // fallback to an existing secret
            ?? "rush-order-tracking-fallback-secret";
        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateToken(Guid orderId)
    {
        var payload = Encoding.UTF8.GetBytes(orderId.ToString("N"));
        var hash = HMACSHA256.HashData(_keyBytes, payload);
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');   // URL-safe base64
    }

    public bool VerifyToken(Guid orderId, string token)
    {
        var expected = GenerateToken(orderId);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token));
    }
}
