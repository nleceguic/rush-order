namespace RushOrder.API.Middleware;

/// <summary>
/// Adds OWASP-recommended HTTP security headers to every response.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            // ── Always-on headers ──────────────────────────────────────────────
            h["X-Content-Type-Options"]  = "nosniff";
            h["X-Frame-Options"]         = "DENY";
            h["X-XSS-Protection"]        = "0";           // rely on CSP; legacy header disabled
            h["Referrer-Policy"]         = "strict-origin-when-cross-origin";
            h["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";

            // ── HTTPS-only ─────────────────────────────────────────────────────
            if (context.Request.IsHttps)
                h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

            // ── CSP — restrictive for API (JSON responses only) ────────────────
            // Dev/Staging: relax CSP to allow Swagger UI inline scripts + CDN assets
            if (env.IsProduction())
            {
                h["Content-Security-Policy"] =
                    "default-src 'none'; " +
                    "frame-ancestors 'none'";
            }
            else
            {
                // Swagger UI needs 'unsafe-inline' for its own script bootstrapping
                h["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net; " +
                    "style-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net; " +
                    "img-src 'self' data: https:; " +
                    "frame-ancestors 'none'";
            }

            // Remove server-disclosure headers
            h.Remove("Server");
            h.Remove("X-Powered-By");
            h.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await next(context);
    }
}
