using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class PrepTimeRepository : IPrepTimeRepository
{
    private readonly string _connectionString;

    public PrepTimeRepository(AppDbContext context)
    {
        _connectionString = context.Database.GetConnectionString()!;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAveragePrepMinutesAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> productIds,
        DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, decimal>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            WITH prep_times AS (
                SELECT
                    h1."OrderId"                       AS order_id,
                    h2."ChangedAt" - h1."ChangedAt"     AS duration
                FROM order_status_history h1
                JOIN order_status_history h2
                     ON h2."OrderId" = h1."OrderId" AND h2."ToStatus" = 'Ready'
                WHERE h1."TenantId"     = @tenantId
                  AND h1."RestaurantId" = @restaurantId
                  AND h1."ToStatus"     = 'Preparing'
                  AND h1."ChangedAt"   >= @since
                  AND h2."ChangedAt"    > h1."ChangedAt"
            ),
            order_products AS (
                SELECT o."Id" AS order_id, (item->>'ProductId')::uuid AS product_id
                FROM orders o, jsonb_array_elements(o.items) AS item
                WHERE o."TenantId" = @tenantId AND o."RestaurantId" = @restaurantId
            )
            SELECT
                op.product_id                                        AS ProductId,
                AVG(EXTRACT(EPOCH FROM pt.duration) / 60.0)::numeric  AS AvgMinutes
            FROM prep_times pt
            JOIN order_products op ON op.order_id = pt.order_id
            WHERE op.product_id = ANY(@productIds)
            GROUP BY op.product_id
            """;

        var rows = await conn.QueryAsync<(Guid ProductId, decimal AvgMinutes)>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, since, productIds = productIds.ToArray() },
                cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.ProductId, r => r.AvgMinutes);
    }

    public async Task<int> GetOrdersInPreparationCountAsync(
        Guid tenantId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT COUNT(*)
            FROM orders
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId
              AND "Status" IN ('Confirmed', 'Preparing')
            """;

        return await conn.QuerySingleAsync<int>(
            new CommandDefinition(sql, new { tenantId, restaurantId }, cancellationToken: cancellationToken));
    }

    public async Task<decimal?> GetRecentAveragePrepMinutesAsync(
        Guid tenantId, Guid restaurantId, int take = 10, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            WITH recent_prep AS (
                SELECT
                    h1."OrderId",
                    h2."ChangedAt" - h1."ChangedAt" AS duration,
                    h2."ChangedAt"                  AS ready_at
                FROM order_status_history h1
                JOIN order_status_history h2
                     ON h2."OrderId" = h1."OrderId" AND h2."ToStatus" = 'Ready'
                WHERE h1."TenantId"     = @tenantId
                  AND h1."RestaurantId" = @restaurantId
                  AND h1."ToStatus"     = 'Preparing'
                  AND h2."ChangedAt"    > h1."ChangedAt"
                ORDER BY h2."ChangedAt" DESC
                LIMIT @take
            )
            SELECT AVG(EXTRACT(EPOCH FROM duration) / 60.0)::numeric FROM recent_prep
            """;

        return await conn.QuerySingleOrDefaultAsync<decimal?>(
            new CommandDefinition(sql, new { tenantId, restaurantId, take }, cancellationToken: cancellationToken));
    }
}
