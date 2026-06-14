using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.Commands;
using RushOrder.Application.Tables.DTOs;
using RushOrder.Application.Tables.Queries;
using RushOrder.Domain.Enums;

namespace RushOrder.API.Controllers;

[Route("api/v1/tables")]
public sealed class TablesController : ApiController
{
    private readonly IQrCodeImageService _qrImageService;

    public TablesController(IQrCodeImageService qrImageService)
        => _qrImageService = qrImageService;

    // GET /api/v1/tables?restaurantId=...
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TableDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTables([FromQuery] Guid restaurantId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetTablesQuery(restaurantId), ct);
            return OkData(result);
        });

    // GET /api/v1/tables/floorplan?restaurantId=...
    [HttpGet("floorplan")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TableFloorPlanDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetFloorPlan([FromQuery] Guid restaurantId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetTableFloorPlanQuery(restaurantId), ct);
            return OkData(result);
        });

    // POST /api/v1/tables
    [HttpPost]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<CreateTableResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateTable([FromBody] CreateTableRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new CreateTableCommand(
                req.RestaurantId, req.Name, req.Capacity, req.Zone,
                req.PositionX, req.PositionY), ct);
            return CreatedData(result, $"/api/v1/tables/{result.TableId}");
        });

    // PUT /api/v1/tables/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateTable(
        Guid id,
        [FromBody] UpdateTableRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new UpdateTableCommand(
                id, req.Name, req.Capacity, req.Zone, req.PositionX, req.PositionY), ct);
            return NoContent();
        });

    // DELETE /api/v1/tables/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DeleteTable(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new DeleteTableCommand(id), ct);
            return NoContent();
        });

    // GET /api/v1/tables/{id}/qr  — returns PNG image
    [HttpGet("{id:guid}/qr")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrCode(Guid id, CancellationToken ct)
    {
        var table = await Mediator.Send(new GetTableByIdQuery(id), ct);
        if (table is null) return NotFoundResponse($"Table {id} not found.");

        var png = _qrImageService.GeneratePng(table.QrUrl);
        return File(png, "image/png", $"table-{table.Name}-qr.png");
    }

    // POST /api/v1/tables/{id}/regenerate-qr
    [HttpPost("{id:guid}/regenerate-qr")]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(typeof(ApiResponse<RegenerateQrResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> RegenerateQr(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new RegenerateQrCommand(id), ct);
            return OkData(result);
        });

    // ── Request DTOs ─────────────────────────────────────────────────────────

    public sealed record CreateTableRequest(
        Guid RestaurantId,
        string Name,
        int Capacity,
        string? Zone = null,
        double? PositionX = null,
        double? PositionY = null);

    public sealed record UpdateTableRequest(
        string? Name,
        int? Capacity,
        string? Zone,
        double? PositionX,
        double? PositionY);
}
