using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.API.IntegrationTests.Identity;

// Resolves the real JwtTokenService/FileRsaKeyProvider from the DI container —
// no HTTP call needed, this exercises the token shape directly.
public sealed class JwtTokenServiceQrSessionTests : IntegrationTestBase
{
    public JwtTokenServiceQrSessionTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public void GenerateQrSessionToken_HasExpectedClaimsAndNoPersonalData()
    {
        using var scope = Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var tableId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var result = tokenService.GenerateQrSessionToken(tableId, restaurantId, tenantId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == "tid").Value.Should().Be(tenantId.ToString());
        jwt.Claims.First(c => c.Type == "table_id").Value.Should().Be(tableId.ToString());
        jwt.Claims.First(c => c.Type == "restaurant_id").Value.Should().Be(restaurantId.ToString());
        jwt.Claims.First(c => c.Type == "token_use").Value.Should().Be("qr_session");

        jwt.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub).Should().BeFalse();
        jwt.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email).Should().BeFalse();
        jwt.Claims.Any(c => c.Type == ClaimTypes.Role).Should().BeFalse();

        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(3), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateQrSessionToken_ProducesTokenThatValidatesAgainstTheSameService()
    {
        using var scope = Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var result = tokenService.GenerateQrSessionToken(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var principal = tokenService.ValidateToken(result.Token);

        principal.Should().NotBeNull();
    }
}
