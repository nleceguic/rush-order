using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Experiments.Commands;
using RushOrder.Application.Experiments.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/experiments")]
public sealed class ExperimentsController : ApiController
{
    // GET /api/v1/experiments/{key}/assignment?restaurantId=&deviceFingerprint=
    // Bucket (0-99) hashed from deviceFingerprint decides the variant — same
    // fingerprint always gets the same variant, no assignment is persisted.
    [HttpGet("{key}/assignment")]
    [AllowAnonymous]
    public Task<IActionResult> GetAssignment(
        string key,
        [FromQuery] Guid restaurantId,
        [FromQuery] string deviceFingerprint,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetExperimentAssignmentQuery(restaurantId, key, deviceFingerprint), ct);
            return OkData(result);
        });

    // POST /api/v1/experiments/events
    // Append-only: Exposure (rail shown), SuggestionAdded (a suggested product
    // was added to the cart), OrderCompleted (final cart total for the session).
    [HttpPost("events")]
    [AllowAnonymous]
    public Task<IActionResult> RecordEvent([FromBody] RecordEventRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new RecordExperimentEventCommand(
                req.RestaurantId, req.ExperimentKey, req.Variant, req.DeviceFingerprint,
                req.EventType, req.OrderId, req.CartTotal), ct);
            return NoContent();
        });

    // GET /api/v1/experiments/{key}/results?restaurantId=
    [HttpGet("{key}/results")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    public Task<IActionResult> GetResults(string key, [FromQuery] Guid restaurantId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetExperimentResultsQuery(restaurantId, key), ct);
            return OkData(result);
        });

    public sealed record RecordEventRequest(
        Guid RestaurantId,
        string ExperimentKey,
        string Variant,
        string DeviceFingerprint,
        string EventType,
        Guid? OrderId,
        decimal? CartTotal);
}
