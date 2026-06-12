using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }
    public bool IsAuthenticated { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        var tidClaim = user?.FindFirstValue("tid");
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId))
            TenantId = tenantId;
    }
}
