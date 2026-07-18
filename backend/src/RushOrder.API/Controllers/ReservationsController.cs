using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Reservations.Commands;
using RushOrder.Application.Reservations.DTOs;
using RushOrder.Application.Reservations.Queries;
using RushOrder.Domain.Enums;

namespace RushOrder.API.Controllers;

[Route("api/v1/reservations")]
public sealed class ReservationsController : ApiController
{
    // GET /api/v1/reservations?restaurantId=...&date=...&status=...
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReservationDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetReservations(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset date,
        [FromQuery] ReservationStatus? status,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            // Npgsql rejects non-UTC offsets against "timestamp with time zone" columns
            // ("Cannot write DateTimeOffset with Offset=... only offset 0 is supported").
            // Clients send their local offset, so normalize before it reaches EF Core.
            var result = await Mediator.Send(new GetReservationsQuery(restaurantId, date.ToUniversalTime(), status), ct);
            return OkData(result);
        });

    // POST /api/v1/reservations
    // AllowAnonymous: supports public self-booking; tenant flows work with or without JWT
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CreateReservationResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateReservation(
        [FromBody] CreateReservationRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new CreateReservationCommand(
                req.RestaurantId, req.GuestName, req.GuestEmail, req.GuestPhone,
                req.PartySize, req.ReservedAt, req.Notes, req.DurationMinutes), ct);

            return CreatedData(result, $"/api/v1/reservations/{result.ReservationId}");
        });

    // POST /api/v1/reservations/{id}/confirm
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new ConfirmReservationCommand(id), ct);
            return NoContent();
        });

    // POST /api/v1/reservations/{id}/seat
    [HttpPost("{id:guid}/seat")]
    [Authorize(Roles = "Admin,Owner,Manager,Waiter")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Seat(
        Guid id,
        [FromBody] SeatReservationRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new SeatReservationCommand(id, req.TableId), ct);
            return NoContent();
        });

    // POST /api/v1/reservations/{id}/cancel
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelReservationRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new CancelReservationCommand(id, req.Reason), ct);
            return NoContent();
        });

    // POST /api/v1/reservations/{id}/no-show
    [HttpPost("{id:guid}/no-show")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> MarkNoShow(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new MarkNoShowCommand(id), ct);
            return NoContent();
        });

    // ── Request DTOs ─────────────────────────────────────────────────────────

    public sealed record CreateReservationRequest(
        Guid RestaurantId,
        string GuestName,
        string? GuestEmail,
        string GuestPhone,
        int PartySize,
        DateTimeOffset ReservedAt,
        string? Notes = null,
        int DurationMinutes = 90);

    public sealed record SeatReservationRequest(Guid TableId);
    public sealed record CancelReservationRequest(string? Reason);
}
