using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Infrastructure.Settings;

namespace RushOrder.Infrastructure.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IRsaKeyProvider _rsaKeyProvider;
    private readonly JwtSettings _settings;

    public JwtTokenService(IRsaKeyProvider rsaKeyProvider, IOptions<JwtSettings> settings)
    {
        _rsaKeyProvider = rsaKeyProvider;
        _settings = settings.Value;
    }

    public AccessTokenResult GenerateAccessToken(User user, Guid tenantId)
    {
        var jti = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tid", tenantId.ToString()),
            new("restaurant_ids", System.Text.Json.JsonSerializer.Serialize(user.RestaurantIds))
        };

        var signingKey = new RsaSecurityKey(_rsaKeyProvider.GetPrivateKey());
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return new AccessTokenResult(token, jti, expires);
    }

    public AccessTokenResult GenerateImpersonationToken(Guid adminUserId, string adminEmail, Guid targetTenantId)
    {
        var jti = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(10);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUserId.ToString()),
            new(JwtRegisteredClaimNames.Email, adminEmail),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.Role, "Admin"),
            new("tid", targetTenantId.ToString()),
            new("impersonating", targetTenantId.ToString()),
            new("restaurant_ids", "[]")
        };

        var signingKey = new RsaSecurityKey(_rsaKeyProvider.GetPrivateKey());
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return new AccessTokenResult(token, jti, expires);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(_rsaKeyProvider.GetPublicKey()),
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
