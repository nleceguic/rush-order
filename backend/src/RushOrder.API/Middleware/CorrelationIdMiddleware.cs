using System.Diagnostics;
using Serilog.Context;

namespace RushOrder.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        // Use incoming ID, fall back to current trace ID, or generate a new one
        var correlationId = context.Request.Headers[Header].FirstOrDefault()
                            ?? Activity.Current?.TraceId.ToString()
                            ?? Guid.NewGuid().ToString("N")[..16];

        context.Items["CorrelationId"] = correlationId;

        // Push to Serilog so every log within this request carries it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Echo back in response before headers are sent
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.TryAdd(Header, correlationId);
                return Task.CompletedTask;
            });

            await next(context);
        }

        // Tag the OTel span (Activity may be set after pipeline execution begins)
        Activity.Current?.SetTag("http.correlation_id", correlationId);
        Activity.Current?.SetBaggage("correlation-id", correlationId);
    }
}
