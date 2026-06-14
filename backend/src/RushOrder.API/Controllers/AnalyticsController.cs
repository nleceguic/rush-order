using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Analytics.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/analytics")]
[Authorize(Roles = "Admin,Owner,Manager")]
public sealed class AnalyticsController : ApiController
{
    // GET /api/v1/analytics/dashboard?restaurantId=...&date=2026-06-14
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<DashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetDashboard(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateOnly? date = null,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetDashboardQuery(restaurantId, date), ct);
            return OkData(result);
        });

    // GET /api/v1/analytics/sales?restaurantId=...&from=...&to=...&groupBy=day
    [HttpGet("sales")]
    [ProducesResponseType(typeof(ApiResponse<SalesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetSales(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string groupBy = "day",
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            if (!new[] { "hour", "day", "week", "month" }.Contains(groupBy))
                return ValidationErrorResponse(["groupBy must be one of: hour, day, week, month"]);

            var result = await Mediator.Send(new GetSalesQuery(restaurantId, from, to, groupBy), ct);
            return OkData(result);
        });

    // GET /api/v1/analytics/products/performance?restaurantId=...&from=...&to=...
    [HttpGet("products/performance")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductPerformanceDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetProductPerformance(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetProductPerformanceQuery(restaurantId, from, to), ct);
            return OkData(result);
        });

    // GET /api/v1/analytics/tables/performance?restaurantId=...&from=...&to=...
    [HttpGet("tables/performance")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TablePerformanceDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTablePerformance(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetTablePerformanceQuery(restaurantId, from, to), ct);
            return OkData(result);
        });

    // GET /api/v1/analytics/waiters/performance?restaurantId=...&from=...&to=...
    [HttpGet("waiters/performance")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WaiterPerformanceDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetWaiterPerformance(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetWaiterPerformanceQuery(restaurantId, from, to), ct);
            return OkData(result);
        });

    // GET /api/v1/analytics/export/sales?restaurantId=...&from=...&to=...
    [HttpGet("export/sales")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportSales(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = await Mediator.Send(new ExportSalesQuery(restaurantId, from, to), ct);
            var fileName = $"ventas_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception)
        {
            return ErrorResponse("Error al generar el archivo Excel.");
        }
    }
}
