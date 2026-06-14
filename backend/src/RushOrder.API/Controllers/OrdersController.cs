using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Orders.Commands;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Application.Orders.Queries;
using RushOrder.Domain.Enums;

namespace RushOrder.API.Controllers;

[Route("api/v1/orders")]
public sealed class OrdersController : ApiController
{
    // POST /api/v1/orders
    // AllowAnonymous: QR-sourced orders don't require auth; the handler enforces tenant via JWT if present
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var items = req.Items
                .Select(i => new CreateOrderItemInput(i.ProductId, i.Quantity, i.Notes, i.Modifiers))
                .ToList()
                .AsReadOnly();

            var result = await Mediator.Send(
                new CreateOrderCommand(req.TableId, req.CustomerId, items, req.Notes, req.Source), ct);

            return CreatedData(result, $"/api/v1/orders/{result.OrderId}");
        });

    // GET /api/v1/orders?restaurantId=...&status=...&page=1&pageSize=20
    [HttpGet]
    [Authorize(Roles = "Admin,Owner,Manager,Waiter")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OrderSummaryDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetOrders(
        [FromQuery] Guid restaurantId,
        [FromQuery] OrderStatus? status,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(
                new GetOrdersByRestaurantQuery(restaurantId, status, dateFrom, dateTo, page, pageSize), ct);
            return OkPaged(result);
        });

    // GET /api/v1/orders/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetOrderByIdQuery(id), ct);
            return result is null
                ? NotFoundResponse($"Order {id} not found.")
                : OkData(result);
        });

    // PATCH /api/v1/orders/{id}/status
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Owner,Manager,Waiter,Kitchen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new UpdateOrderStatusCommand(id, req.Status, req.Note), ct);
            return NoContent();
        });

    // POST /api/v1/orders/{id}/items
    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> AddItem(
        Guid id,
        [FromBody] AddItemRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new AddItemToOrderCommand(id, req.ProductId, req.Quantity, req.Notes), ct);
            return NoContent();
        });

    // DELETE /api/v1/orders/{id}/items/{itemId}
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new RemoveOrderItemCommand(id, itemId), ct);
            return NoContent();
        });

    // POST /api/v1/orders/{id}/cancel
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelOrderRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new CancelOrderCommand(id, req.Reason), ct);
            return NoContent();
        });

    // ── Request DTOs ─────────────────────────────────────────────────────────

    public sealed record OrderItemRequest(
        Guid ProductId,
        int Quantity,
        string? Notes = null,
        IEnumerable<string>? Modifiers = null);

    public sealed record CreateOrderRequest(
        Guid TableId,
        Guid? CustomerId,
        IReadOnlyList<OrderItemRequest> Items,
        string? Notes,
        OrderSource Source);

    public sealed record UpdateStatusRequest(OrderStatus Status, string? Note);
    public sealed record AddItemRequest(Guid ProductId, int Quantity, string? Notes);
    public sealed record CancelOrderRequest(string Reason);
}
