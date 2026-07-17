using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.MultiTenancy;

public sealed class MultiTenancyExtendedTests : IntegrationTestBase
{
    public MultiTenancyExtendedTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task TenantA_CannotUpdateProduct_FromTenantB()
    {
        // Register Tenant B and get its product
        var (tenantBClient, tenantBRestaurantId) = await RegisterTenantBAsync();

        Guid tenantBProductId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.IgnoreQueryFilters()
                .FirstAsync(p => p.RestaurantId == tenantBRestaurantId);
            tenantBProductId = product.Id;
        }

        // Tenant A tries to toggle Tenant B's product availability
        var ownerAClient = await Auth.GetOwnerClientAsync();
        var response = await ownerAClient.PatchAsync(
            $"/api/v1/menu/products/{tenantBProductId}/availability", null);

        // EF Core global query filter hides Tenant B's product → 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Tenant A must not see or modify Tenant B's products");
    }

    [Fact]
    public async Task TenantA_CannotAccessTable_FromTenantB()
    {
        // Register Tenant B and get one of its table IDs
        var (_, tenantBRestaurantId) = await RegisterTenantBAsync();

        Guid tenantBTableId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var table = await db.Tables.IgnoreQueryFilters()
                .FirstAsync(t => t.RestaurantId == tenantBRestaurantId);
            tenantBTableId = table.Id;
        }

        // Tenant A queries tables for Tenant B's restaurant
        var ownerAClient = await Auth.GetOwnerClientAsync();
        var response = await ownerAClient.GetAsync(
            $"/api/v1/tables?restaurantId={tenantBRestaurantId}");

        // Either empty list (global filter) or 404 is acceptable — never Tenant B's data
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("data").EnumerateArray().ToList();
            items.Should().BeEmpty("Tenant A must not see Tenant B's tables");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid RestaurantId)> RegisterTenantBAsync()
    {
        var anonClient = Factory.CreateClient();
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        var response = await anonClient.PostAsJsonAsync("/api/v1/onboarding/register", new
        {
            businessName   = $"Tenant B {uniqueSuffix}",
            ownerEmail     = $"owner-b-{uniqueSuffix}@test.com",
            ownerPassword  = "TenantB1234!",
            ownerFirstName = "Owner",
            ownerLastName  = "B",
            phone          = "+34 91 000 0000"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken  = body.GetProperty("accessToken").GetString()!;
        var restaurantId = Guid.Parse(body.GetProperty("restaurantId").GetString()!);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return (client, restaurantId);
    }
}
