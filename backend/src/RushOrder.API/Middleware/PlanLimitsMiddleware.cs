using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.Middleware;

public sealed class PlanLimitsMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> _excludedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/swagger", "/ws",
        "/api/v1/auth",
        "/api/v1/onboarding",
        "/api/v1/subscriptions/plans",
        "/api/v1/admin"
    };

    private static readonly Dictionary<string, string> _featureRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "/api/v1/analytics", "analytics" },
        { "/api/v1/reservations", "reservations" },
        { "/api/v1/inventory", "inventory" },
    };

    public PlanLimitsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcluded(path) || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (context.User.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        var tidClaim = context.User.FindFirst("tid")?.Value;
        if (!Guid.TryParse(tidClaim, out var tenantId))
        {
            await _next(context);
            return;
        }

        var cache = context.RequestServices.GetRequiredService<IDistributedCache>();
        var db = context.RequestServices.GetRequiredService<AppDbContext>();

        var tenantStatus = await GetTenantStatusAsync(tenantId, cache, db, context.RequestAborted);

        if (tenantStatus is TenantStatus.Suspended or TenantStatus.TrialExpired)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                title = "Payment Required",
                status = 402,
                detail = tenantStatus == TenantStatus.Suspended
                    ? "Your account is suspended. Please update your subscription."
                    : "Your trial has expired. Please upgrade to a paid plan."
            }), context.RequestAborted);
            return;
        }

        foreach (var (routePrefix, feature) in _featureRoutes)
        {
            if (path.StartsWith(routePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var hasFeature = await HasPlanFeatureAsync(tenantId, feature, cache, db, context.RequestAborted);
                if (!hasFeature)
                {
                    context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        title = "Upgrade Required",
                        status = 402,
                        detail = $"The '{feature}' module is not included in your current plan."
                    }), context.RequestAborted);
                    return;
                }
                break;
            }
        }

        await _next(context);
    }

    private static bool IsExcluded(string path)
        => _excludedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static async Task<TenantStatus?> GetTenantStatusAsync(
        Guid tenantId, IDistributedCache cache, AppDbContext db, CancellationToken ct)
    {
        var cacheKey = $"tenant:status:{tenantId}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null && Enum.TryParse<TenantStatus>(cached, out var status))
            return status;

        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return null;

        if (tenant.Status == TenantStatus.Trial
            && tenant.TrialEndsAt.HasValue
            && tenant.TrialEndsAt.Value < DateTimeOffset.UtcNow)
        {
            tenant.MarkTrialExpired();
            await db.SaveChangesAsync(ct);
        }

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
        await cache.SetStringAsync(cacheKey, tenant.Status.ToString(), options, ct);
        return tenant.Status;
    }

    private static async Task<bool> HasPlanFeatureAsync(
        Guid tenantId, string feature, IDistributedCache cache, AppDbContext db, CancellationToken ct)
    {
        var cacheKey = $"tenant:features:{tenantId}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        IReadOnlyList<string>? features = null;

        if (cached is not null)
        {
            features = JsonSerializer.Deserialize<List<string>>(cached);
        }
        else
        {
            var subscription = await db.Set<Domain.Entities.Subscription>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

            if (subscription is not null)
            {
                var plan = await db.Set<Domain.Entities.Plan>()
                    .FindAsync([subscription.PlanId], ct);
                features = plan?.Features ?? [];
            }

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            };
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(features ?? []), options, ct);
        }

        return features?.Contains(feature, StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
