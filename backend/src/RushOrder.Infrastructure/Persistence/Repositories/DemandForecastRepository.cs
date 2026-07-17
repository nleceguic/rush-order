using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class DemandForecastRepository : IDemandForecastRepository
{
    private readonly AppDbContext _context;
    private readonly string _connectionString;

    public DemandForecastRepository(AppDbContext context)
    {
        _context = context;
        _connectionString = context.Database.GetConnectionString()!;
    }

    public async Task<IReadOnlyList<ActiveRestaurantRow>> GetActiveRestaurantsAsync(
        CancellationToken cancellationToken = default)
    {
        // EF (not Dapper) here — Settings is a JSON-owned column (ToJson()),
        // and letting EF translate the property access avoids having to know
        // its exact JSON column/path in raw SQL.
        return await _context.Restaurants
            .IgnoreQueryFilters()
            .Where(r => r.IsActive)
            .Select(r => new ActiveRestaurantRow(
                r.Id, r.TenantId, r.Timezone,
                r.Settings.OpeningTime, r.Settings.ClosingTime, r.Settings.KitchenCapacity))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForecastProductRow>> GetActiveProductsAsync(
        Guid tenantId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT "Id" AS ProductId, "Name" AS Name
            FROM products
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "IsAvailable" = true
            """;

        var rows = await conn.QueryAsync<ForecastProductRow>(
            new CommandDefinition(sql, new { tenantId, restaurantId }, cancellationToken: cancellationToken));

        return rows.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<HistoricalSaleRow>> GetHistoricalSalesAsync(
        Guid tenantId, Guid restaurantId, DateTimeOffset since, string timezone, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                (item->>'ProductId')::uuid                                          AS ProductId,
                (o."CreatedAt" AT TIME ZONE @timezone)::date                         AS LocalDate,
                EXTRACT(DOW  FROM (o."CreatedAt" AT TIME ZONE @timezone))::int       AS DayOfWeek,
                EXTRACT(HOUR FROM (o."CreatedAt" AT TIME ZONE @timezone))::int       AS Hour,
                SUM((item->>'Quantity')::int)                                       AS Quantity
            FROM orders o, jsonb_array_elements(o.items) AS item
            WHERE o."TenantId"     = @tenantId
              AND o."RestaurantId" = @restaurantId
              AND o."Status"      <> 'Cancelled'
              AND o."CreatedAt"    >= @since
              AND o.items IS NOT NULL
            GROUP BY ProductId, LocalDate, DayOfWeek, Hour
            """;

        var rows = await conn.QueryAsync<HistoricalSaleRow>(
            new CommandDefinition(sql, new { tenantId, restaurantId, since, timezone }, cancellationToken: cancellationToken));

        return rows.ToList().AsReadOnly();
    }

    public async Task ReplaceForecastsAsync(
        Guid tenantId, Guid restaurantId, DateOnly fromDate, DateOnly toDate,
        IReadOnlyList<DemandForecast> forecasts, CancellationToken cancellationToken = default)
    {
        var existing = await _context.DemandForecasts
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId && f.RestaurantId == restaurantId
                     && f.ForecastDate >= fromDate && f.ForecastDate <= toDate)
            .ToListAsync(cancellationToken);

        _context.DemandForecasts.RemoveRange(existing);
        await _context.DemandForecasts.AddRangeAsync(forecasts, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForecastReadRow>> GetForecastRowsAsync(
        Guid tenantId, Guid restaurantId, DateOnly date, Guid? productId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                p."Id"                AS ProductId,
                p."Name"               AS Name,
                p.price_amount          AS Price,
                f."ForecastHour"        AS Hour,
                f."PredictedQuantity"   AS PredictedQuantity,
                f."ConfidenceLevel"     AS ConfidenceLevel
            FROM demand_forecasts f
            JOIN products p ON p."Id" = f."ProductId" AND p."TenantId" = @tenantId
            WHERE f."TenantId"     = @tenantId
              AND f."RestaurantId" = @restaurantId
              AND f."ForecastDate" = @date
              AND (@productId IS NULL OR f."ProductId" = @productId)
            ORDER BY f."ForecastHour"
            """;

        var rows = await conn.QueryAsync<ForecastReadRow>(
            new CommandDefinition(sql, new { tenantId, restaurantId, date, productId }, cancellationToken: cancellationToken));

        return rows.ToList().AsReadOnly();
    }
}
