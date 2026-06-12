using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RushOrder.API.Filters;

internal sealed class AuditActionFilter(ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var sw = Stopwatch.StartNew();
        var executed = await next();
        sw.Stop();

        var http = context.HttpContext;
        var statusCode = executed.Exception is not null && !executed.ExceptionHandled
            ? StatusCodes.Status500InternalServerError
            : http.Response.StatusCode;

        logger.LogInformation(
            "[AUDIT] {Method} {Path} | user={UserId} | tenant={TenantId} | status={StatusCode} | duration={DurationMs}ms",
            http.Request.Method,
            http.Request.Path.Value,
            http.User.FindFirst("sub")?.Value ?? "anonymous",
            http.User.FindFirst("tenant_id")?.Value ?? "-",
            statusCode,
            sw.ElapsedMilliseconds);
    }
}
