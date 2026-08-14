using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RushOrder.Infrastructure.Services;

namespace RushOrder.API.IntegrationTests.Identity;

// Plain unit test — CurrentTenantService only depends on IHttpContextAccessor,
// which is faked directly here. No database, no ApiFactory needed.
public sealed class CurrentTenantServiceTests
{
    private static CurrentTenantService BuildService(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new CurrentTenantService(accessor);
    }

    [Fact]
    public void Constructor_QrSessionClaims_SetsIsQrSessionAndTableId()
    {
        var tenantId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim("table_id", tableId.ToString()),
            new Claim("token_use", "qr_session"),
        ], authenticationType: "Bearer");

        var service = BuildService(new ClaimsPrincipal(identity));

        service.IsQrSession.Should().BeTrue();
        service.QrSessionTableId.Should().Be(tableId);
        service.TenantId.Should().Be(tenantId);
        service.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Constructor_StaffClaims_IsQrSessionFalseAndTableIdNull()
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Owner"),
        ], authenticationType: "Bearer");

        var service = BuildService(new ClaimsPrincipal(identity));

        service.IsQrSession.Should().BeFalse();
        service.QrSessionTableId.Should().BeNull();
        service.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Constructor_NoClaims_AllFalseOrNull()
    {
        var service = BuildService(new ClaimsPrincipal());

        service.IsAuthenticated.Should().BeFalse();
        service.IsQrSession.Should().BeFalse();
        service.QrSessionTableId.Should().BeNull();
        service.TenantId.Should().BeNull();
    }
}
