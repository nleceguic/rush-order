using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.MultiTenancy;

public sealed class MultiTenancyTests : IntegrationTestBase
{
    public MultiTenancyTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetOrders_TenantB_CannotSeeOrdersOfTenantA()
    {
        // Register a second tenant (Tenant B)
        var (tenantBClient, tenantBRestaurantId) = await RegisterTenantBAsync();

        // Tenant A's restaurant ID
        Guid tenantARestaurantId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantARestaurantId = (await db.Restaurants.IgnoreQueryFilters()
                .FirstAsync(r => r.Id != tenantBRestaurantId)).Id;
        }

        // Tenant B tries to list orders from Tenant A's restaurant
        var response = await tenantBClient.GetAsync(
            $"/api/v1/orders?restaurantId={tenantARestaurantId}&page=1&pageSize=20");

        // Should either return empty (EF query filter: tenant B's filter returns nothing from tenant A)
        // or 200 with zero items
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("data").EnumerateArray().ToList();
            items.Should().BeEmpty("tenant B must not see tenant A's orders");
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task GetOrders_TenantA_OnlySeesOwnOrders()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();

        Guid tenantARestaurantId;
        int tenantAOrderCount;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var restaurant = await db.Restaurants.IgnoreQueryFilters().FirstAsync();
            tenantARestaurantId = restaurant.Id;
            tenantAOrderCount = await db.Orders.IgnoreQueryFilters()
                .CountAsync(o => o.RestaurantId == tenantARestaurantId);
        }

        var response = await ownerClient.GetAsync(
            $"/api/v1/orders?restaurantId={tenantARestaurantId}&page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();
        items.Should().HaveCount(tenantAOrderCount,
            "owner must see exactly their own tenant's orders");
    }

    [Fact]
    public async Task GetRestaurants_ReturnsOnlyCurrentTenantRestaurants()
    {
        // Register Tenant B so there are 2 tenants in the DB
        await RegisterTenantBAsync();

        var ownerClient = await Auth.GetOwnerClientAsync();
        var response = await ownerClient.GetAsync("/api/v1/restaurants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();

        items.Should().HaveCount(1, "Tenant A has exactly one restaurant (rincon-chef)");
    }

    [Fact]
    public async Task AdminClient_CanSeeAllTenants()
    {
        await RegisterTenantBAsync();

        var adminClient = await Auth.GetAdminClientAsync();
        var response = await adminClient.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").EnumerateArray().ToList();

        items.Should().HaveCountGreaterThanOrEqualTo(2,
            "admin must see all tenants: rincon-chef + system + tenant B");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid RestaurantId)> RegisterTenantBAsync()
    {
        var anonClient = Factory.CreateClient();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var response = await anonClient.PostAsJsonAsync("/api/v1/onboarding/register", new
        {
            businessName   = $"Tenant B Restaurant {uniqueSuffix}",
            ownerEmail     = $"owner-b-{uniqueSuffix}@test.com",
            ownerPassword  = "TenantB1234!",
            ownerFirstName = "Owner",
            ownerLastName  = "B",
            phone          = "+34 91 000 0000"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "onboarding should succeed for a fresh tenant");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken  = body.GetProperty("accessToken").GetString()!;
        var restaurantId = Guid.Parse(body.GetProperty("restaurantId").GetString()!);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return (client, restaurantId);
    }
}
