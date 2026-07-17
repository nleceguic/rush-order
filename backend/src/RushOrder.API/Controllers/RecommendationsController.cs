using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Recommendations.Commands;
using RushOrder.Application.Recommendations.DTOs;
using RushOrder.Application.Recommendations.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/recommendations")]
public sealed class RecommendationsController : ApiController
{
    // GET /api/v1/recommendations?restaurantId=&productIds=&customerId=
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RecommendationsResponse>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetRecommendations(
        [FromQuery] Guid restaurantId,
        [FromQuery] string? productIds,
        [FromQuery] Guid? customerId,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var cartProductIds = ParseProductIds(productIds);
            var result = await Mediator.Send(new GetRecommendationsQuery(restaurantId, customerId, cartProductIds), ct);
            return OkData(new RecommendationsResponse(result));
        });

    // ── Admin: reglas de maridaje manuales ──────────────────────────────────

    // GET /api/v1/recommendations/pairing-rules?restaurantId=
    [HttpGet("pairing-rules")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PairingRuleDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPairingRules([FromQuery] Guid restaurantId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetPairingRulesQuery(restaurantId), ct);
            return OkData(result);
        });

    // POST /api/v1/recommendations/pairing-rules
    [HttpPost("pairing-rules")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreatePairingRule([FromBody] CreatePairingRuleRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var id = await Mediator.Send(
                new CreatePairingRuleCommand(req.RestaurantId, req.SourceProductId, req.TargetProductId), ct);
            return CreatedData(id, $"/api/v1/recommendations/pairing-rules/{id}");
        });

    // DELETE /api/v1/recommendations/pairing-rules/{id}
    [HttpDelete("pairing-rules/{id:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeletePairingRule(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new DeletePairingRuleCommand(id), ct);
            return NoContent();
        });

    private static Guid[] ParseProductIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
    }

    // ── Request/response DTOs ────────────────────────────────────────────────

    public sealed record RecommendationsResponse(IReadOnlyList<ProductRecommendationDto> Recommendations);
    public sealed record CreatePairingRuleRequest(Guid RestaurantId, Guid SourceProductId, Guid TargetProductId);
}
