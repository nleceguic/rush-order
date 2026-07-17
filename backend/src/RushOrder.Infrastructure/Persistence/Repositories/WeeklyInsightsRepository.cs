using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class WeeklyInsightsRepository : IWeeklyInsightsRepository
{
    private readonly string _connectionString;

    public WeeklyInsightsRepository(AppDbContext context)
    {
        _connectionString = context.Database.GetConnectionString()!;
    }

    public async Task<IReadOnlyList<RestaurantOwnerRow>> GetActiveRestaurantOwnersAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                r."Id"                                 AS RestaurantId,
                r."TenantId"                            AS TenantId,
                r."Name"                                AS RestaurantName,
                u.email                                 AS OwnerEmail,
                (u."FirstName" || ' ' || u."LastName")  AS OwnerName
            FROM restaurants r
            JOIN users u ON u."TenantId" = r."TenantId" AND u."Role" = 'Owner' AND u."IsActive" = true
            WHERE r."IsActive" = true
            """;

        var rows = await conn.QueryAsync<RestaurantOwnerRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList().AsReadOnly();
    }

    public async Task<WeeklyInsightsDto> BuildInsightsAsync(
        Guid tenantId, Guid restaurantId, DateTimeOffset weekStart, DateTimeOffset weekEnd,
        DateOnly nextSaturday, DateOnly nextSunday, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var lastWeekStart = weekStart.AddDays(-7);
        var lastWeekEnd = weekEnd.AddDays(-7);

        // ── Revenue this week vs last week ──────────────────────────────────
        const string revenueSql = """
            SELECT
                COALESCE(SUM(total_amount) FILTER (WHERE "CreatedAt" >= @weekStart AND "CreatedAt" < @weekEnd), 0)         AS ThisWeek,
                COALESCE(SUM(total_amount) FILTER (WHERE "CreatedAt" >= @lastWeekStart AND "CreatedAt" < @lastWeekEnd), 0) AS LastWeek
            FROM orders
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "Status" <> 'Cancelled'
              AND "CreatedAt" >= @lastWeekStart AND "CreatedAt" < @weekEnd
            """;
        var revenue = await conn.QuerySingleAsync<(decimal ThisWeek, decimal LastWeek)>(
            new CommandDefinition(revenueSql, new { tenantId, restaurantId, weekStart, weekEnd, lastWeekStart, lastWeekEnd }, cancellationToken: cancellationToken));

        var revenueChangePercent = revenue.LastWeek == 0
            ? (revenue.ThisWeek > 0 ? 100m : 0m)
            : Math.Round((revenue.ThisWeek - revenue.LastWeek) / revenue.LastWeek * 100m, 1);

        // ── Product quantities this week / last week (for star + review) ───
        const string productWeekSql = """
            SELECT
                (item->>'ProductId')::uuid AS ProductId,
                item->>'Name'              AS Name,
                SUM((item->>'Quantity')::int) AS Quantity
            FROM orders, jsonb_array_elements(items) AS item
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "Status" <> 'Cancelled'
              AND "CreatedAt" >= @from AND "CreatedAt" < @to AND items IS NOT NULL
            GROUP BY item->>'ProductId', item->>'Name'
            """;
        var thisWeekProducts = (await conn.QueryAsync<(Guid ProductId, string Name, int Quantity)>(
            new CommandDefinition(productWeekSql, new { tenantId, restaurantId, from = weekStart, to = weekEnd }, cancellationToken: cancellationToken))).ToList();
        var lastWeekProducts = (await conn.QueryAsync<(Guid ProductId, string Name, int Quantity)>(
            new CommandDefinition(productWeekSql, new { tenantId, restaurantId, from = lastWeekStart, to = lastWeekEnd }, cancellationToken: cancellationToken))).ToList();

        var starProduct = thisWeekProducts.OrderByDescending(p => p.Quantity).FirstOrDefault();

        // ── Product to review: worst-rated (avg food rating < 3 this week) first, else a >20% sales drop ──
        const string worstRatedSql = """
            WITH low_rated_orders AS (
                SELECT o."Id"
                FROM orders o
                JOIN order_ratings r ON r."OrderId" = o."Id"
                WHERE o."TenantId" = @tenantId AND o."RestaurantId" = @restaurantId
                  AND o."CreatedAt" >= @from AND o."CreatedAt" < @to
                  AND r."FoodRating" < 3
            )
            SELECT item->>'Name' AS Name, COUNT(*) AS Occurrences
            FROM orders o, jsonb_array_elements(o.items) AS item
            WHERE o."Id" IN (SELECT "Id" FROM low_rated_orders)
            GROUP BY item->>'Name'
            ORDER BY Occurrences DESC
            LIMIT 1
            """;
        var worstRated = await conn.QueryFirstOrDefaultAsync<(string Name, int Occurrences)?>(
            new CommandDefinition(worstRatedSql, new { tenantId, restaurantId, from = weekStart, to = weekEnd }, cancellationToken: cancellationToken));

        string? productToReviewName = null;
        string? productToReviewReason = null;

        if (worstRated is not null)
        {
            productToReviewName = worstRated.Value.Name;
            productToReviewReason = $"{worstRated.Value.Occurrences} pedido(s) con nota de comida por debajo de 3★ esta semana";
        }
        else
        {
            var biggestDrop = thisWeekProducts
                .Select(p =>
                {
                    var lastQty = lastWeekProducts.FirstOrDefault(l => l.ProductId == p.ProductId).Quantity;
                    var dropPercent = lastQty == 0 ? 0m : Math.Round((decimal)(lastQty - p.Quantity) / lastQty * 100m, 1);
                    return (p.Name, DropPercent: dropPercent);
                })
                .Where(p => p.DropPercent > 20m)
                .OrderByDescending(p => p.DropPercent)
                .FirstOrDefault();

            if (biggestDrop.Name is not null)
            {
                productToReviewName = biggestDrop.Name;
                productToReviewReason = $"Ventas bajaron un {biggestDrop.DropPercent}% frente a la semana anterior";
            }
        }

        // ── Peak hour + best day this week ──────────────────────────────────
        const string peakHourSql = """
            SELECT EXTRACT(HOUR FROM "CreatedAt")::int AS Hour, COUNT(*) AS Orders
            FROM orders
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "Status" <> 'Cancelled'
              AND "CreatedAt" >= @weekStart AND "CreatedAt" < @weekEnd
            GROUP BY Hour ORDER BY Orders DESC LIMIT 1
            """;
        var peakHour = await conn.QueryFirstOrDefaultAsync<(int Hour, int Orders)?>(
            new CommandDefinition(peakHourSql, new { tenantId, restaurantId, weekStart, weekEnd }, cancellationToken: cancellationToken));

        const string bestDaySql = """
            SELECT "CreatedAt"::date AS Day, SUM(total_amount) AS Revenue
            FROM orders
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "Status" <> 'Cancelled'
              AND "CreatedAt" >= @weekStart AND "CreatedAt" < @weekEnd
            GROUP BY Day ORDER BY Revenue DESC LIMIT 1
            """;
        var bestDay = await conn.QueryFirstOrDefaultAsync<(DateTime Day, decimal Revenue)?>(
            new CommandDefinition(bestDaySql, new { tenantId, restaurantId, weekStart, weekEnd }, cancellationToken: cancellationToken));

        // ── Upcoming weekend: reservations + demand forecast ────────────────
        const string weekendReservationsSql = """
            SELECT COUNT(*)
            FROM reservations
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId
              AND "Status" = 1 -- Confirmed (see ReservationStatus enum)
              AND "ReservedAt"::date IN (@saturday, @sunday)
            """;
        var weekendReservations = await conn.QuerySingleOrDefaultAsync<int>(
            new CommandDefinition(weekendReservationsSql, new { tenantId, restaurantId, saturday = nextSaturday, sunday = nextSunday }, cancellationToken: cancellationToken));

        const string weekendForecastSql = """
            SELECT p."Name" AS Name, SUM(f."PredictedQuantity") AS Quantity
            FROM demand_forecasts f
            JOIN products p ON p."Id" = f."ProductId" AND p."TenantId" = @tenantId
            WHERE f."TenantId" = @tenantId AND f."RestaurantId" = @restaurantId
              AND f."ForecastDate" IN (@saturday, @sunday)
            GROUP BY p."Name"
            ORDER BY Quantity DESC
            LIMIT 3
            """;
        var weekendForecast = (await conn.QueryAsync<(string Name, decimal Quantity)>(
            new CommandDefinition(weekendForecastSql, new { tenantId, restaurantId, saturday = nextSaturday, sunday = nextSunday }, cancellationToken: cancellationToken))).ToList();

        return new WeeklyInsightsDto(
            revenue.ThisWeek,
            revenue.LastWeek,
            revenueChangePercent,
            starProduct.Name,
            starProduct.Quantity,
            productToReviewName,
            productToReviewReason,
            peakHour?.Hour,
            bestDay is null ? null : DateOnly.FromDateTime(bestDay.Value.Day),
            bestDay?.Revenue ?? 0,
            weekendReservations,
            weekendForecast.Select(p => p.Name).ToList().AsReadOnly());
    }
}
