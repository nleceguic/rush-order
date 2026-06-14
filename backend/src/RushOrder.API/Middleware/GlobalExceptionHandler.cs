using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Common.Exceptions;

namespace RushOrder.API.Middleware;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        var logLevel = exception is BusinessRuleException or NotFoundException or ValidationException
            ? LogLevel.Warning
            : LogLevel.Error;

        logger.Log(logLevel,
            exception,
            "Unhandled exception. CorrelationId={CorrelationId} Path={Path}",
            correlationId,
            httpContext.Request.Path);

        httpContext.Response.Headers["X-Correlation-ID"] = correlationId;

        var (title, status) = exception switch
        {
            ValidationException         => ("Validation failed", StatusCodes.Status400BadRequest),
            BusinessRuleException       => ("Business rule violation", StatusCodes.Status422UnprocessableEntity),
            NotFoundException           => ("Resource not found", StatusCodes.Status404NotFound),
            ArgumentException           => ("Bad request", StatusCodes.Status400BadRequest),
            KeyNotFoundException        => ("Resource not found", StatusCodes.Status404NotFound),
            UnauthorizedAccessException => ("Unauthorized", StatusCodes.Status401Unauthorized),
            _                           => ("An unexpected error occurred", StatusCodes.Status500InternalServerError)
        };

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title    = title,
                Status   = status,
                Detail   = exception.Message,
                Instance = httpContext.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            },
            cancellationToken);

        return true;
    }
}
