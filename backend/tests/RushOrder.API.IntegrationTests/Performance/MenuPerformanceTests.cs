using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.Performance;

public sealed class MenuPerformanceTests : IntegrationTestBase
{
    public MenuPerformanceTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPublicMenu_ResponseTime_UnderBaseline()
    {
        string qrCode;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            qrCode = await db.Tables.IgnoreQueryFilters()
                .Select(t => t.QrCode)
                .FirstAsync();
        }

        var client = Factory.CreateClient();
        var url    = $"/api/v1/menu/public/{qrCode}";

        // Warm up: ensure Redis cache is populated before measuring
        for (var i = 0; i < 3; i++)
            await client.GetAsync(url);

        // Measure 100 requests (sequential to avoid flooding the in-process server)
        var timings = new List<TimeSpan>(100);
        for (var i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await client.GetAsync(url);
            sw.Stop();
            resp.EnsureSuccessStatusCode();
            timings.Add(sw.Elapsed);
        }

        var sorted = timings.OrderBy(t => t).ToList();
        // P95 = the value at the 95th-percentile position (index 94 of 100)
        var p95 = sorted[94];

        // Generous threshold for Docker/CI overhead; cached responses should be << 50 ms
        p95.Should().BeLessThan(TimeSpan.FromMilliseconds(300),
            $"P95 of 100 cached GetPublicMenu requests must be < 300 ms (actual P95 = {p95.TotalMilliseconds:F1} ms)");
    }
}
