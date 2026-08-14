using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }
    public bool IsAuthenticated { get; }
    public bool IsQrSession { get; }
    public Guid? QrSessionTableId { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        var tidClaim = user?.FindFirstValue("tid");
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId))
            TenantId = tenantId;

        IsQrSession = user?.FindFirstValue("token_use") == "qr_session";

        var tableIdClaim = user?.FindFirstValue("table_id");
        if (tableIdClaim is not null && Guid.TryParse(tableIdClaim, out var tableId))
            QrSessionTableId = tableId;
    }
}
