using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.SignalR;

public sealed class SignalRTests : IntegrationTestBase
{
    public SignalRTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateOrder_TriggersSignalR_OrderReceived_Event()
    {
        var (ownerClient, accessToken, _) = await Auth.LoginAsync(
            TestConstants.OwnerEmail, TestConstants.DemoPassword);

        Guid restaurantId;
        Guid tableId;
        Guid productId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var restaurant = await db.Restaurants.IgnoreQueryFilters().FirstAsync();
            restaurantId = restaurant.Id;

            var table = await db.Tables.IgnoreQueryFilters()
                .Where(t => t.RestaurantId == restaurant.Id)
                .FirstAsync();
            tableId = table.Id;

            var product = await db.Products.IgnoreQueryFilters()
                .Where(p => p.RestaurantId == restaurant.Id && p.IsAvailable)
                .FirstAsync();
            productId = product.Id;
        }

        // ── Connect SignalR client using the in-process server handler ────────
        var handler = Factory.Server.CreateHandler();
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/ws/restaurant", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        var eventReceived = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<JsonElement>("OrderReceived", payload =>
            eventReceived.TrySetResult(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinRestaurantAsync", restaurantId.ToString());

        // ── Place order via HTTP ──────────────────────────────────────────────
        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes  = (string?)null,
            source = "QR"
        });
        createResp.EnsureSuccessStatusCode();

        // ── Wait for the hub event (max 5 seconds) ───────────────────────────
        var completedFirst = await Task.WhenAny(
            eventReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        completedFirst.Should().BeSameAs(eventReceived.Task,
            "OrderPlacedEventHandler should broadcast 'OrderReceived' to restaurant:{id} group within 5 seconds");

        var eventPayload = await eventReceived.Task;
        eventPayload.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the event payload must not be empty");
    }
}
