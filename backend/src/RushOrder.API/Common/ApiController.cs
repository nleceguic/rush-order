using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Models;

namespace RushOrder.API.Common;

[ApiController]
[Authorize]
public abstract class ApiController : ControllerBase
{
    private IMediator? _mediator;

    protected IMediator Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not in token."));

    // ── Success helpers ────────────────────────────────────────────────────────

    protected IActionResult OkData<T>(T data) =>
        Ok(ApiResponse<T>.Ok(data));

    protected IActionResult OkPaged<T>(PagedResult<T> paged) =>
        Ok(ApiResponse<IReadOnlyList<T>>.Ok(
            paged.Items,
            new PaginationMeta
            {
                Page       = paged.PageNumber,
                PageSize   = paged.PageSize,
                TotalCount = paged.TotalCount,
                TotalPages = paged.TotalPages
            }));

    protected IActionResult CreatedData<T>(T data, string location) =>
        Created(location, ApiResponse<T>.Ok(data));

    // ── Error helpers ──────────────────────────────────────────────────────────

    protected IActionResult ErrorResponse(string message, params string[] errors) =>
        UnprocessableEntity(ApiResponse<object>.Fail(message, errors.Length > 0 ? errors : null));

    protected IActionResult NotFoundResponse(string message) =>
        NotFound(ApiResponse<object>.Fail(message));

    protected IActionResult ValidationErrorResponse(string[] errors) =>
        BadRequest(ApiResponse<object>.Fail("Validation failed.", errors));

    // ── Execution wrapper — maps domain exceptions to HTTP responses ──────────

    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (NotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            return ErrorResponse(ex.Message);
        }
        catch (ValidationException ex)
        {
            return ValidationErrorResponse(
                ex.Errors.Select(e => e.ErrorMessage).ToArray());
        }
    }
}
