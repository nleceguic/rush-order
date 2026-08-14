using System.Net;
using System.Net.Http.Json;
using RushOrder.API.IntegrationTests.Infrastructure;

namespace RushOrder.API.IntegrationTests.Authentication;

/// <summary>
/// Uses a dedicated factory that enables the global rate limiter (100 req/min per IP),
/// kept as its own IClassFixture (distinct DisableRateLimit config) rather than sharing
/// ApiFactory. Still placed in the "Integration" collection — not for fixture sharing,
/// but because xUnit runs different collections in parallel by default, and two
/// WebApplicationFactory&lt;Program&gt; instances building hosts for the same entry point
/// concurrently race inside HostFactoryResolver ("entry point exited without ever
/// building an IHost" for whichever one loses). Same collection means sequential
/// execution relative to the rest of this assembly's integration tests.
/// xUnit calls RateLimitApiFactory.InitializeAsync/DisposeAsync automatically via IAsyncLifetime.
/// </summary>
[Collection("Integration")]
public sealed class RateLimitTests : IClassFixture<RateLimitApiFactory>
{
    private readonly RateLimitApiFactory _factory;

    public RateLimitTests(RateLimitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RateLimiter_Returns429_WhenBurstLimitExceeded()
    {
        var client = _factory.CreateClient();

        // Fire 101 concurrent requests — the FixedWindowLimiter (100 req/min, QueueLimit=0)
        // must reject at least one with 429.
        var tasks = Enumerable.Range(0, 101)
            .Select(_ => client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email    = TestConstants.OwnerEmail,
                password = "AnyPasswordTriggerRateLimit1!"
            }))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "101 concurrent requests to the same IP partition must trigger the 100 req/min limiter");
    }
}
