using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.API.Common;
using RushOrder.Application.Categories.Commands;
using RushOrder.Application.Categories.DTOs;
using RushOrder.Application.Categories.Queries;
using RushOrder.Application.Menu.DTOs;
using RushOrder.Application.Menu.Queries;
using RushOrder.Application.Products.Commands;
using RushOrder.Application.Products.DTOs;
using RushOrder.Application.Products.Queries;

namespace RushOrder.API.Controllers;

[Route("api/v1/menu")]
public sealed class MenuController : ApiController
{
    // ── Public menu ───────────────────────────────────────────────────────────

    // GET /api/v1/menu/public/{qrToken}
    [HttpGet("public/{qrToken}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicMenuDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicMenu(string qrToken, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetPublicMenuQuery(qrToken), ct);
        if (result is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=30";
        return OkData(result);
    }

    // ── Categories ────────────────────────────────────────────────────────────

    // GET /api/v1/menu/categories?restaurantId=...
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetCategories([FromQuery] Guid restaurantId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(new GetCategoriesQuery(restaurantId), ct);
            return OkData(result);
        });

    // POST /api/v1/menu/categories
    [HttpPost("categories")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var id = await Mediator.Send(
                new CreateCategoryCommand(req.RestaurantId, req.Name, req.Description, req.ImageUrl, req.SortOrder), ct);
            return CreatedData(id, $"/api/v1/menu/categories/{id}");
        });

    // PUT /api/v1/menu/categories/{id}
    [HttpPut("categories/{id:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(
                new UpdateCategoryCommand(id, req.Name, req.Description, req.ImageUrl, req.SortOrder), ct);
            return NoContent();
        });

    // DELETE /api/v1/menu/categories/{id}
    [HttpDelete("categories/{id:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new DeleteCategoryCommand(id), ct);
            return NoContent();
        });

    // ── Products ──────────────────────────────────────────────────────────────

    // GET /api/v1/menu/products?restaurantId=...&categoryId=...&page=1&pageSize=20
    [HttpGet("products")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductSummaryDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetProducts(
        [FromQuery] Guid restaurantId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? onlyAvailable,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            var result = await Mediator.Send(
                new GetProductsQuery(restaurantId, categoryId, onlyAvailable, page, pageSize), ct);
            return OkPaged(result);
        });

    // POST /api/v1/menu/products
    [HttpPost("products")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var id = await Mediator.Send(new CreateProductCommand(
                req.CategoryId, req.Name, req.Description, req.Price,
                req.ImageUrl, req.Allergens ?? [], req.Tags ?? [],
                req.PreparationMinutes), ct);
            return CreatedData(id, $"/api/v1/menu/products/{id}");
        });

    // PUT /api/v1/menu/products/{id}
    [HttpPut("products/{id:guid}")]
    [Authorize(Roles = "Admin,Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest req,
        CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new UpdateProductCommand(
                id, req.Name, req.Description, req.Price,
                req.ImageUrl, req.Allergens, req.Tags,
                req.PreparationMinutes, req.SortOrder), ct);
            return NoContent();
        });

    // PATCH /api/v1/menu/products/{id}/availability
    [HttpPatch("products/{id:guid}/availability")]
    [Authorize(Roles = "Admin,Owner,Manager,Waiter")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> ToggleAvailability(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var isAvailable = await Mediator.Send(new ToggleProductAvailabilityCommand(id), ct);
            return OkData(isAvailable);
        });

    // DELETE /api/v1/menu/products/{id}
    [HttpDelete("products/{id:guid}")]
    [Authorize(Roles = "Admin,Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new DeleteProductCommand(id), ct);
            return NoContent();
        });

    // ── Request DTOs ─────────────────────────────────────────────────────────

    public sealed record CreateCategoryRequest(
        Guid RestaurantId,
        string Name,
        string? Description,
        string? ImageUrl,
        int SortOrder = 0);

    public sealed record UpdateCategoryRequest(
        string? Name,
        string? Description,
        string? ImageUrl,
        int? SortOrder);

    public sealed record CreateProductRequest(
        Guid CategoryId,
        string Name,
        string? Description,
        decimal Price,
        string? ImageUrl,
        IReadOnlyList<string>? Allergens,
        IReadOnlyList<string>? Tags,
        int PreparationMinutes = 10);

    public sealed record UpdateProductRequest(
        string? Name,
        string? Description,
        decimal? Price,
        string? ImageUrl,
        IReadOnlyList<string>? Allergens,
        IReadOnlyList<string>? Tags,
        int? PreparationMinutes,
        int? SortOrder);
}
