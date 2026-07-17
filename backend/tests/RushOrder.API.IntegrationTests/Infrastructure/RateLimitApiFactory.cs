using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace RushOrder.API.IntegrationTests.Infrastructure;

/// <summary>
/// Factory variant that enables the global rate limiter (100 req/min per IP).
/// Used exclusively by <see cref="RushOrder.API.IntegrationTests.Authentication.RateLimitTests"/>.
/// </summary>
public sealed class RateLimitApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // The base sets DisableRateLimit = "true". Override with "false" so the
        // PartitionedRateLimiter is active during rate-limit-specific tests.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DisableRateLimit"] = "false"
            }));
    }
}
