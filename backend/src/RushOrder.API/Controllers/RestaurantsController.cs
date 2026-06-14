using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Restaurants.Commands;
using RushOrder.Application.Restaurants.DTOs;
using RushOrder.Application.Restaurants.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/restaurants")]
public sealed class RestaurantsController : ApiController
{
    // GET /api/v1/restaurants — returns only the restaurants for the current tenant (EF query filter)
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RestaurantDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetRestaurants(CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetRestaurantsQuery(), ct);
            return OkData(result);
        });

    // POST /api/v1/restaurants
    [HttpPost]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateRestaurant(
        [FromBody] CreateRestaurantRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var id = await Mediator.Send(new CreateRestaurantCommand(
                req.Name, req.Address, req.Phone, req.Email,
                req.TaxRate, req.Timezone, req.Currency), ct);
            return CreatedData(id, $"/api/v1/restaurants/{id}");
        });

    // PUT /api/v1/restaurants/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> UpdateRestaurant(
        Guid id,
        [FromBody] UpdateRestaurantRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new UpdateRestaurantCommand(
                id, req.Name, req.Address, req.Phone, req.Email,
                req.LogoUrl, req.CoverUrl), ct);
            return NoContent();
        });

    // PATCH /api/v1/restaurants/{id}/settings
    [HttpPatch("{id:guid}/settings")]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> UpdateSettings(
        Guid id,
        [FromBody] UpdateRestaurantSettingsRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new UpdateRestaurantSettingsCommand(
                id,
                req.OnlineOrderingEnabled,
                req.TableOrderingEnabled,
                req.ReservationsEnabled,
                req.MaxReservationPartySize,
                req.ReservationSlotMinutes,
                req.OpeningTime,
                req.ClosingTime,
                req.AutoConfirmOrders,
                req.PrintReceiptByDefault,
                req.KitchenDisplayEnabled,
                req.OrderAlertAfterMinutes), ct);
            return NoContent();
        });

    // ── Request DTOs ─────────────────────────────────────────────────────────

    public sealed record CreateRestaurantRequest(
        string Name,
        string Address,
        string Phone,
        string Email,
        decimal TaxRate = 0.21m,
        string Timezone = "Europe/Madrid",
        string Currency = "EUR");

    public sealed record UpdateRestaurantRequest(
        string Name,
        string Address,
        string Phone,
        string Email,
        string? LogoUrl = null,
        string? CoverUrl = null);

    public sealed record UpdateRestaurantSettingsRequest(
        bool OnlineOrderingEnabled,
        bool TableOrderingEnabled,
        bool ReservationsEnabled,
        int MaxReservationPartySize,
        int ReservationSlotMinutes,
        string OpeningTime,
        string ClosingTime,
        bool AutoConfirmOrders,
        bool PrintReceiptByDefault,
        bool KitchenDisplayEnabled,
        int OrderAlertAfterMinutes);
}
