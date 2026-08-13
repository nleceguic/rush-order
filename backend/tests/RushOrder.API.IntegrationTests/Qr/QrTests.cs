using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.Qr;

public sealed class QrTests : IntegrationTestBase
{
    public QrTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetByQrCode_WithValidQrCode_Returns200WithTableAndRestaurantData()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/qr/{qrCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetProperty("tableId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("restaurantId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("restaurantName").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("availableLocales").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByQrCode_WithInvalidQrCode_Returns404()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/qr/INVALID_QR_CODE");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByQrCode_ResponseIncludesCacheControlHeader()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/qr/{qrCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().BeGreaterThan(TimeSpan.Zero);
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
