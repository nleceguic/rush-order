using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Onboarding.Commands;
using RushOrder.Application.Onboarding.DTOs;

namespace RushOrder.API.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;

    public OnboardingController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterTenantResult>> Register(
        [FromBody] RegisterTenantRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RegisterTenantCommand(
            request.BusinessName,
            request.OwnerEmail,
            request.OwnerPassword,
            request.OwnerFirstName,
            request.OwnerLastName,
            request.Phone), ct);

        return Created($"/api/v1/restaurants/{result.RestaurantId}", result);
    }
}
