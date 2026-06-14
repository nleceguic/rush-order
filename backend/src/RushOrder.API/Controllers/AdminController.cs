using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Admin.Commands;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Admin.Queries;

namespace RushOrder.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyList<TenantSummaryDto>>> GetTenants(CancellationToken ct)
        => Ok(await _mediator.Send(new GetTenantsQuery(), ct));

    [HttpGet("tenants/{id:guid}")]
    public async Task<ActionResult<TenantDetailDto>> GetTenant(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTenantDetailQuery(id), ct));

    [HttpPost("tenants/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid id, [FromBody] SuspendTenantRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SuspendTenantCommand(id, request.Reason), ct);
        return NoContent();
    }

    [HttpPost("tenants/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ActivateTenantCommand(id), ct);
        return NoContent();
    }

    [HttpPut("tenants/{id:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(
        Guid id, [FromBody] UpdateTenantPlanRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateTenantPlanCommand(id, request.PlanId), ct);
        return NoContent();
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<GlobalMetricsDto>> GetMetrics(CancellationToken ct)
        => Ok(await _mediator.Send(new GetGlobalMetricsQuery(), ct));

    [HttpPost("tenants/{id:guid}/impersonate")]
    public async Task<ActionResult<ImpersonateTokenDto>> Impersonate(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new ImpersonateTenantCommand(id), ct));
}
