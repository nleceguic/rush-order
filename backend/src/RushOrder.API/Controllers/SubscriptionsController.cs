using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Subscriptions.Commands;
using RushOrder.Application.Subscriptions.DTOs;
using RushOrder.Application.Subscriptions.Queries;

namespace RushOrder.API.Controllers;

[ApiController]
[Route("api/v1/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("current")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<CurrentSubscriptionDto>> GetCurrent(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCurrentSubscriptionQuery(), ct));

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> GetPlans(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAvailablePlansQuery(), ct));

    [HttpPost("upgrade")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<CheckoutSessionDto>> Upgrade(
        [FromBody] CreateCheckoutSessionRequest request, CancellationToken ct)
    {
        var successUrl = $"{Request.Scheme}://{Request.Host}/subscriptions/success";
        var cancelUrl = $"{Request.Scheme}://{Request.Host}/subscriptions";

        var result = await _mediator.Send(new CreateCheckoutSessionCommand(
            request.PlanId,
            request.BillingPeriod,
            successUrl,
            cancelUrl), ct);

        return Ok(result);
    }

    [HttpPost("cancel")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequest request, CancellationToken ct)
    {
        await _mediator.Send(new CancelSubscriptionCommand(request.Immediately), ct);
        return NoContent();
    }

    [HttpGet("billing-history")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<IReadOnlyList<BillingInvoiceDto>>> GetBillingHistory(CancellationToken ct)
        => Ok(await _mediator.Send(new GetBillingHistoryQuery(), ct));

    [HttpGet("usage")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<UsageDto>> GetUsage(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUsageQuery(), ct));
}
