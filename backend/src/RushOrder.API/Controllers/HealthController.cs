using System.Diagnostics;
using System.Reflection;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RushOrder.Infrastructure.Persistence;
using StackExchange.Redis;
using Stripe;

namespace RushOrder.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(
    AppDbContext db,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    IWebHostEnvironment env,
    IServiceProvider sp) : ControllerBase
{
    private static readonly DateTime StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    // GET /health/detailed — Admin only
    [HttpGet("detailed")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Detailed(CancellationToken ct)
    {
        var dbTask     = CheckDatabaseAsync(ct);
        var redisTask  = CheckRedisAsync();
        var sbTask     = CheckServiceBusAsync(ct);
        var stripeTask = CheckStripeAsync(ct);

        await Task.WhenAll(dbTask, redisTask, sbTask, stripeTask);

        var dbResult     = await dbTask;
        var redisResult  = await redisTask;
        var sbResult     = await sbTask;
        var stripeResult = await stripeTask;

        var allHealthy = dbResult.Healthy && redisResult.Healthy && sbResult.Healthy && stripeResult.Healthy;
        var uptime     = DateTime.UtcNow - StartTime;
        var version    = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        return Ok(new
        {
            status = allHealthy ? "Healthy" : "Degraded",
            components = new
            {
                database   = dbResult.ToDto(),
                redis      = redisResult.ToDto(),
                serviceBus = sbResult.ToDto(),
                stripe     = stripeResult.ToDto(),
            },
            version,
            uptime    = FormatUptime(uptime),
            timestamp = DateTime.UtcNow,
            environment = env.EnvironmentName,
        });
    }

    // ── Database ──────────────────────────────────────────────────────────────

    private async Task<DbCheckResult> CheckDatabaseAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Lightweight connectivity + connection count query
            var connections = await db.Database
                .SqlQuery<int>($"SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database()")
                .FirstOrDefaultAsync(ct);

            sw.Stop();
            return new DbCheckResult(true, $"{sw.ElapsedMilliseconds}ms", connections);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DbCheckResult(false, $"{sw.ElapsedMilliseconds}ms", 0, ex.Message);
        }
    }

    // ── Redis ─────────────────────────────────────────────────────────────────

    private async Task<RedisCheckResult> CheckRedisAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var server = redis.GetServers().FirstOrDefault()
                ?? throw new InvalidOperationException("No Redis server found");

            await redis.GetDatabase().PingAsync();
            sw.Stop();

            var info = await server.InfoAsync("memory");
            var usedMem = info
                .SelectMany(g => g)
                .FirstOrDefault(e => e.Key == "used_memory_human").Value ?? "?";

            return new RedisCheckResult(true, $"{sw.ElapsedMilliseconds}ms", usedMem);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RedisCheckResult(false, $"{sw.ElapsedMilliseconds}ms", "?", ex.Message);
        }
    }

    // ── Service Bus ───────────────────────────────────────────────────────────

    private async Task<SbCheckResult> CheckServiceBusAsync(CancellationToken ct)
    {
        var connStr = configuration["ServiceBus:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connStr))
            return new SbCheckResult(true, 0, "Not configured — skipped");

        try
        {
            var adminClient = sp.GetService<ServiceBusAdministrationClient>();
            if (adminClient is null)
                return new SbCheckResult(true, 0, "Admin client not registered");

            // Check namespace connectivity
            var props = await adminClient.GetNamespacePropertiesAsync(ct);

            // Check pending messages on the orders queue (if configured)
            var queueName = configuration["ServiceBus:OrdersQueueName"];
            long pending = 0;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                var runtime = await adminClient.GetQueueRuntimePropertiesAsync(queueName, ct);
                pending = runtime.Value.ActiveMessageCount;
            }

            return new SbCheckResult(true, pending);
        }
        catch (Exception ex)
        {
            return new SbCheckResult(false, 0, ex.Message);
        }
    }

    // ── Stripe ────────────────────────────────────────────────────────────────

    private static async Task<StripeCheckResult> CheckStripeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
            return new StripeCheckResult(true, "Not configured — skipped");

        try
        {
            // Lightweight call: retrieve balance (no side effects)
            await new BalanceService().GetAsync(cancellationToken: ct);
            return new StripeCheckResult(true);
        }
        catch (Exception ex)
        {
            return new StripeCheckResult(false, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatUptime(TimeSpan t) =>
        $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m";

    // ── Result types ──────────────────────────────────────────────────────────

    private sealed record DbCheckResult(bool Healthy, string ResponseTime, int Connections, string? Error = null)
    {
        public object ToDto() => Error is null
            ? new { status = "Healthy", responseTime = ResponseTime, connections = Connections }
            : new { status = "Unhealthy", responseTime = ResponseTime, connections = Connections, error = Error };
    }

    private sealed record RedisCheckResult(bool Healthy, string ResponseTime, string Memory, string? Error = null)
    {
        public object ToDto() => Error is null
            ? new { status = "Healthy", responseTime = ResponseTime, memory = Memory }
            : new { status = "Unhealthy", responseTime = ResponseTime, memory = Memory, error = Error };
    }

    private sealed record SbCheckResult(bool Healthy, long PendingMessages, string? Error = null)
    {
        public object ToDto() => Error is null
            ? new { status = "Healthy", pendingMessages = PendingMessages }
            : new { status = "Unhealthy", pendingMessages = PendingMessages, error = Error };
    }

    private sealed record StripeCheckResult(bool Healthy, string? Error = null)
    {
        public object ToDto() => Error is null
            ? new { status = "Healthy" }
            : new { status = "Unhealthy", error = Error };
    }
}
