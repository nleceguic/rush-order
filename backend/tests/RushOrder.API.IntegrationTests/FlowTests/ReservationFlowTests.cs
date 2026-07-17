using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.API.IntegrationTests.FlowTests;

public sealed class ReservationFlowTests : IntegrationTestBase
{
    public ReservationFlowTests(ApiFactory factory) : base(factory) { }

    /// <summary>
    /// Full reservation journey: book → email captured → confirm → seat → table Occupied.
    /// </summary>
    [Fact]
    public async Task ReservationFlow_BookToSeated()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();

        Guid restaurantId;
        Guid tableId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var restaurant = await db.Restaurants.IgnoreQueryFilters().FirstAsync();
            restaurantId = restaurant.Id;
            tableId = await db.Tables.IgnoreQueryFilters()
                .Where(t => t.RestaurantId == restaurantId)
                .Select(t => t.Id)
                .FirstAsync();
        }

        // ── 1. POST /reservations — anonymous guest self-books ────────────────
        var anonClient = Factory.CreateClient();
        var createResp = await anonClient.PostAsJsonAsync("/api/v1/reservations", new
        {
            restaurantId,
            guestName    = "Ana García",
            guestEmail   = "ana.garcia@example.com",
            guestPhone   = "+34 666 000 001",
            partySize    = 2,
            reservedAt   = DateTimeOffset.UtcNow.AddDays(1).ToString("o"),
            notes        = "Cumpleaños",
            durationMinutes = 90
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created,
            "anonymous reservation must succeed");

        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var reservationId = Guid.Parse(createBody.GetProperty("data").GetProperty("reservationId").GetString()!);

        // ── 2. Verify confirmation email was captured ─────────────────────────
        Factory.NotificationCapture.ReservationConfirmationCount.Should().Be(1,
            "a confirmation email must be dispatched after booking");

        // ── 3. POST /reservations/{id}/confirm — owner confirms ───────────────
        var confirmResp = await ownerClient.PostAsync(
            $"/api/v1/reservations/{reservationId}/confirm", null);
        confirmResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "owner must be able to confirm a pending reservation");

        // ── 4. POST /reservations/{id}/seat — seat the guest at a table ──────
        var seatResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/reservations/{reservationId}/seat", new { tableId });
        seatResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "seating must succeed for an available table");

        // ── 5. GET /tables — confirm table is now Occupied ───────────────────
        var tablesResp = await ownerClient.GetAsync(
            $"/api/v1/tables?restaurantId={restaurantId}");
        tablesResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var tablesBody = await tablesResp.Content.ReadFromJsonAsync<JsonElement>();
        var tables = tablesBody.GetProperty("data").EnumerateArray().ToList();

        var seatedTable = tables.FirstOrDefault(t =>
            Guid.Parse(t.GetProperty("id").GetString()!) == tableId);
        seatedTable.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the seated table must appear in the restaurant's table list");
        seatedTable.GetProperty("status").GetString().Should().Be(nameof(TableStatus.Occupied),
            "seating a reservation should mark the table as Occupied");
    }
}
