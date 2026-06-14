using System.Security.Claims;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public record AccessTokenResult(string Token, string Jti, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(User user, Guid tenantId);
    AccessTokenResult GenerateImpersonationToken(Guid adminUserId, string adminEmail, Guid targetTenantId);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
