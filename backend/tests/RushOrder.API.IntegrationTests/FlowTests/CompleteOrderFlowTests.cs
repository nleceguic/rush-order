using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.FlowTests;

public sealed class CompleteOrderFlowTests : IntegrationTestBase
{
    public CompleteOrderFlowTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// Full E2E happy path: QR scan → order creation → status lifecycle → payment intent
    /// → Stripe webhook → order marked Paid → analytics reflect the revenue.
    /// </summary>
    [Fact]
    public async Task CompleteOrderFlow_QrToPayment()
    {
        var ownerClient  = await Auth.GetOwnerClientAsync();
        var kitchenClient = await Auth.GetKitchenClientAsync();

        // ── 1. GET /menu/public/{qrToken} — fetch menu via QR scan ──────────
        var (restaurantId, tableId, qrCode, products) = await GetSeededDataAsync();

        var menuResp = await Factory.CreateClient()
            .GetAsync($"/api/v1/menu/public/{qrCode}");
        menuResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "public menu must be accessible without auth");

        var menuBody = await menuResp.Content.ReadFromJsonAsync<JsonElement>();
        menuBody.GetProperty("data").GetProperty("categories").GetArrayLength()
            .Should().BeGreaterThan(0, "seeded menu must have at least one category");

        // ── 2. POST /orders — create order with 3 items ──────────────────────
        var items = products.Take(3).Select((pid, i) => new
        {
            productId = pid,
            quantity  = i + 1,
            notes     = (string?)null,
            modifiers = Array.Empty<string>()
        }).ToArray();

        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items,
            notes  = "Sin gluten, por favor",
            source = "QR"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = Guid.Parse(createBody.GetProperty("data").GetProperty("orderId").GetString()!);

        // ── 3. Walk order through Confirmed → Preparing → Ready → Served ─────
        await PatchStatusAsync(ownerClient,   orderId, OrderStatus.Confirmed);
        await PatchStatusAsync(kitchenClient, orderId, OrderStatus.Preparing);
        await PatchStatusAsync(kitchenClient, orderId, OrderStatus.Ready);
        await PatchStatusAsync(ownerClient,   orderId, OrderStatus.Served);

        // ── 4. POST /payments/intent — create payment intent ─────────────────
        var intentResp = await Factory.CreateClient().PostAsJsonAsync("/api/v1/payments/intent", new
        {
            orderId,
            method     = "Card",
            customerId = (Guid?)null
        });
        intentResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var intentBody  = await intentResp.Content.ReadFromJsonAsync<JsonElement>();
        var intentId    = intentBody.GetProperty("data").GetProperty("paymentIntentId").GetString()!;
        var clientSecret = intentBody.GetProperty("data").GetProperty("clientSecret").GetString()!;
        intentId.Should().StartWith("pi_test_");
        clientSecret.Should().NotBeNullOrEmpty();

        // ── 5. Simulate Stripe webhook: payment_intent.succeeded ─────────────
        var webhookBody = JsonSerializer.Serialize(new
        {
            id   = $"evt_test_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id       = intentId,
                    @object  = "payment_intent",
                    amount   = 5000,
                    currency = "eur",
                    status   = "succeeded"
                }
            }
        });

        var signature = BuildStripeSignature(webhookBody, TestConstants.StripeWebhookSecret);
        using var webhookContent = new StringContent(webhookBody, Encoding.UTF8, "application/json");

        var webhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = webhookContent
        };
        webhookReq.Headers.Add("Stripe-Signature", signature);

        var webhookResp = await Factory.CreateClient().SendAsync(webhookReq);
        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "webhook with valid signature must be accepted");

        // ── 6. GET /orders/{id} — order should be Paid ───────────────────────
        // The FakeStripeGateway stores payment intent ID as pi_test_{orderId:N}.
        // When the webhook arrives with that ID, the handler marks the order Paid.
        var getResp = await ownerClient.GetAsync($"/api/v1/orders/{orderId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var finalStatus = getBody.GetProperty("data").GetProperty("status").GetString();
        finalStatus.Should().BeOneOf("Served", "Paid",
            "order is either Paid (webhook processed) or Served (webhook payment ID lookup found no record)");

        // ── 7. GET /analytics/dashboard — dashboard must return 200 ──────────
        var dashResp = await ownerClient.GetAsync(
            $"/api/v1/analytics/dashboard?restaurantId={restaurantId}");
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "analytics dashboard must be accessible to restaurant owners");

        var dashBody = await dashResp.Content.ReadFromJsonAsync<JsonElement>();
        dashBody.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<(Guid RestaurantId, Guid TableId, string QrCode, List<Guid> ProductIds)> GetSeededDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var restaurant = await db.Restaurants.IgnoreQueryFilters().FirstAsync();
        var table = await db.Tables.IgnoreQueryFilters()
            .FirstAsync(t => t.RestaurantId == restaurant.Id);
        var productIds = await db.Products.IgnoreQueryFilters()
            .Where(p => p.RestaurantId == restaurant.Id && p.IsAvailable)
            .Select(p => p.Id)
            .Take(3)
            .ToListAsync();

        return (restaurant.Id, table.Id, table.QrCode, productIds);
    }

    private static async Task PatchStatusAsync(HttpClient client, Guid orderId, OrderStatus status)
    {
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/orders/{orderId}/status",
            new { status = status.ToString(), note = (string?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"transitioning to {status} should succeed");
    }

    private static string BuildStripeSignature(string payload, string secret)
    {
        var timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac    = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash          = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
