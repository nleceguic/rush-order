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

namespace RushOrder.API.IntegrationTests.Payments;

public sealed class PaymentTests : IntegrationTestBase
{
    public PaymentTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task CreatePaymentIntent_ForValidOrder_ReturnsClientSecret()
    {
        // Create a fresh order to get a valid orderId
        var ownerClient = await Auth.GetOwnerClientAsync();
        var orderId = await CreateTestOrderAsync(ownerClient);

        // Payment intent is AllowAnonymous
        var anonClient = Factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync("/api/v1/payments/intent", new
        {
            orderId,
            method     = "Card",
            customerId = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetProperty("clientSecret").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("paymentIntentId").GetString().Should().StartWith("pi_test_");
    }

    [Fact]
    public async Task CreatePaymentIntent_ForNonExistentOrder_Returns404()
    {
        var anonClient = Factory.CreateClient();

        var response = await anonClient.PostAsJsonAsync("/api/v1/payments/intent", new
        {
            orderId    = Guid.NewGuid(),
            method     = "Card",
            customerId = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_WithValidSignature_Returns200()
    {
        var rawBody = BuildWebhookPayload("payment_intent.succeeded", new
        {
            id             = "pi_test_valid_sig_123",
            @object        = "payment_intent",
            amount         = 2000,
            currency       = "eur",
            status         = "succeeded"
        });

        var signature = BuildStripeSignature(rawBody, TestConstants.StripeWebhookSecret);
        var client    = Factory.CreateClient();

        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", signature);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = content
        };
        request.Headers.Add("Stripe-Signature", signature);

        var response = await client.SendAsync(request);

        // The event type is handled (payment_intent.succeeded → tries to find payment by ID)
        // Since the payment doesn't exist in DB, the handler logs a warning and returns.
        // The important thing is that the signature is valid → 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_Returns400()
    {
        var rawBody   = BuildWebhookPayload("payment_intent.succeeded", new { id = "pi_test_bad_sig" });
        var client    = Factory.CreateClient();

        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = content
        };
        request.Headers.Add("Stripe-Signature", "t=1234567890,v1=invalidsignature");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_WithMissingSignatureHeader_Returns400()
    {
        var rawBody = BuildWebhookPayload("payment_intent.succeeded", new { id = "pi_test_no_header" });
        var client  = Factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/payments/webhook",
            new StringContent(rawBody, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateTestOrderAsync(HttpClient ownerClient)
    {
        using var scope = Factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var restaurant  = await db.Restaurants.IgnoreQueryFilters().FirstAsync();
        var table       = await db.Tables.IgnoreQueryFilters()
                              .FirstAsync(t => t.RestaurantId == restaurant.Id);
        var product     = await db.Products.IgnoreQueryFilters()
                              .FirstAsync(p => p.RestaurantId == restaurant.Id && p.IsAvailable);

        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId    = table.Id,
            customerId = (Guid?)null,
            items      = new[] { new { productId = product.Id, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes      = (string?)null,
            source     = "Manual"
        });

        createResp.EnsureSuccessStatusCode();
        var body = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("data").GetProperty("orderId").GetString()!);
    }

    private static string BuildWebhookPayload(string eventType, object dataObject)
    {
        var json = JsonSerializer.Serialize(new
        {
            id   = $"evt_test_{Guid.NewGuid():N}",
            type = eventType,
            data = new { @object = dataObject }
        });
        return json;
    }

    /// <summary>
    /// Computes a Stripe-compatible webhook signature header.
    /// Format: "t={unix_timestamp},v1={hmac_sha256_hex}"
    /// </summary>
    private static string BuildStripeSignature(string payload, string secret)
    {
        var timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        var secretBytes   = Encoding.UTF8.GetBytes(secret);
        var payloadBytes  = Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var sig  = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={timestamp},v1={sig}";
    }
}
