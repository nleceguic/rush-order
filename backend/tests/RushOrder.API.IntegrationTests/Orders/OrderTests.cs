using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.Orders;

public sealed class OrderTests : IntegrationTestBase
{
    public OrderTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateOrder_AsOwner_WithQrSource_Returns201()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();
        var (restaurantId, tableId, productId) = await GetSeededIdsAsync();

        var response = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 2, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = "Sin gluten",
            source = "QR"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("orderId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateOrder_WithoutAuth_Returns401()
    {
        var anonClient = Factory.CreateClient();
        var (_, tableId, productId) = await GetSeededIdsAsync();

        var response = await anonClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = (string?)null,
            source = "QR"
        });

        // Handler requires TenantId from JWT — anonymous request → UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrder_WithUnavailableProduct_Returns422()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();
        var (restaurantId, tableId, _) = await GetSeededIdsAsync();

        // Mark the first product unavailable, then try to order it
        Guid unavailableProductId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.IgnoreQueryFilters()
                .FirstAsync(p => p.RestaurantId == restaurantId);
            product.SetAvailability(false);
            await db.SaveChangesAsync();
            unavailableProductId = product.Id;
        }

        var response = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId = unavailableProductId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = (string?)null,
            source = "QR"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateOrder_WithNonExistentTable_Returns404()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();

        var (_, _, productId) = await GetSeededIdsAsync();

        var response = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId = Guid.NewGuid(), // non-existent
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = (string?)null,
            source = "QR"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_AsOwner_ReturnsSeededOrders()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();
        var (restaurantId, _, _) = await GetSeededIdsAsync();

        var response = await ownerClient.GetAsync(
            $"/api/v1/orders?restaurantId={restaurantId}&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();
        items.Should().NotBeEmpty("dev seeder creates 5 sample orders");
    }

    [Fact]
    public async Task GetOrders_AsKitchen_RequiresStatusFilter()
    {
        var kitchenClient = await Auth.GetKitchenClientAsync();
        var (restaurantId, _, _) = await GetSeededIdsAsync();

        // Kitchen staff can see active (non-completed) orders
        var response = await kitchenClient.GetAsync(
            $"/api/v1/orders?restaurantId={restaurantId}&status=Preparing&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();
        items.Should().NotBeEmpty("seeder creates one Preparing order");
        items.Should().AllSatisfy(item =>
            item.GetProperty("status").GetString().Should().Be("Preparing"));
    }

    [Fact]
    public async Task UpdateOrderStatus_FullLifecycle_WorksFromPendingToPaid()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();
        var (restaurantId, tableId, productId) = await GetSeededIdsAsync();

        // Create a fresh order
        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = (string?)null,
            source = "Manual"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = Guid.Parse(createBody.GetProperty("data").GetProperty("orderId").GetString()!);

        // Walk through the full lifecycle
        await PatchStatusAsync(ownerClient, orderId, OrderStatus.Confirmed);
        await PatchStatusAsync(ownerClient, orderId, OrderStatus.Preparing);
        await PatchStatusAsync(ownerClient, orderId, OrderStatus.Ready);
        await PatchStatusAsync(ownerClient, orderId, OrderStatus.Served);
        await PatchStatusAsync(ownerClient, orderId, OrderStatus.Paid);

        // Verify final state
        var getResp = await ownerClient.GetAsync($"/api/v1/orders/{orderId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        getBody.GetProperty("data").GetProperty("status").GetString().Should().Be("Paid");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid RestaurantId, Guid TableId, Guid ProductId)> GetSeededIdsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var restaurant = await db.Restaurants.IgnoreQueryFilters().FirstAsync();

        var table = await db.Tables.IgnoreQueryFilters()
            .Where(t => t.RestaurantId == restaurant.Id)
            .FirstAsync();

        var product = await db.Products.IgnoreQueryFilters()
            .Where(p => p.RestaurantId == restaurant.Id && p.IsAvailable)
            .FirstAsync();

        return (restaurant.Id, table.Id, product.Id);
    }

    private static async Task PatchStatusAsync(HttpClient client, Guid orderId, OrderStatus status)
    {
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/orders/{orderId}/status",
            new { status = status.ToString(), note = (string?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent, $"transitioning to {status} should succeed");
    }
}
