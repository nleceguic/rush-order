using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.Menu;

public sealed class PublicMenuTests : IntegrationTestBase
{
    public PublicMenuTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPublicMenu_WithValidQrCode_Returns200WithMenuData()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/menu/public/{qrCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetProperty("restaurantId").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("categories").GetArrayLength().Should().BeGreaterThan(0,
            "seeder creates at least 4 categories with products");
    }

    [Fact]
    public async Task GetPublicMenu_WithInvalidQrCode_Returns404()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/menu/public/INVALID_QR_CODE");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPublicMenu_ResponseIncludesCacheControlHeader()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/menu/public/{qrCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetPublicMenu_SecondRequest_ServesFromCache()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        // First request — populates the Redis cache
        var first = await client.GetAsync($"/api/v1/menu/public/{qrCode}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second request — should be served from cache (same data, no error)
        var second = await client.GetAsync($"/api/v1/menu/public/{qrCode}");
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody  = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        secondBody.Should().Be(firstBody, "cached response must be identical to original");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<string> GetSeededQrCodeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var table = await db.Tables.IgnoreQueryFilters().FirstAsync();
        return table.QrCode;
    }
}
