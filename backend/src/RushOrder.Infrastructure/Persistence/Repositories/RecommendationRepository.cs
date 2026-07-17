using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class RecommendationRepository : IRecommendationRepository
{
    private readonly string _connectionString;

    public RecommendationRepository(AppDbContext context)
    {
        _connectionString = context.Database.GetConnectionString()!;
    }

    public async Task<int> GetCompletedOrderCountAsync(Guid tenantId, Guid restaurantId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT COUNT(*)
            FROM orders
            WHERE "TenantId"     = @tenantId
              AND "RestaurantId" = @restaurantId
              AND "Status"      <> 'Cancelled'
            """;

        return await conn.QuerySingleAsync<int>(
            new CommandDefinition(sql, new { tenantId, restaurantId }, cancellationToken: ct));
    }

    // FASE COLD START — "Más pedidos hoy": unidades vendidas hoy por producto,
    // leídas del array JSONB de items de cada pedido (ver Order.Items).
    public async Task<IReadOnlyList<RecommendationCandidate>> GetTopSellingTodayAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> excludeProductIds, int take, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            WITH sold_today AS (
                SELECT
                    (item->>'ProductId')::uuid AS product_id,
                    SUM((item->>'Quantity')::int) AS units
                FROM orders, jsonb_array_elements(items) AS item
                WHERE "TenantId"      = @tenantId
                  AND "RestaurantId"  = @restaurantId
                  AND "CreatedAt"::date = CURRENT_DATE
                  AND "Status"       <> 'Cancelled'
                  AND items IS NOT NULL
                GROUP BY item->>'ProductId'
            )
            SELECT
                p."Id"       AS ProductId,
                p."Name"     AS Name,
                p."ImageUrl" AS ImageUrl,
                p.price_amount   AS Price,
                p.price_currency AS Currency,
                st.units::numeric AS Weight
            FROM sold_today st
            JOIN products p ON p."Id" = st.product_id AND p."TenantId" = @tenantId
            WHERE p."IsAvailable" = true
              AND NOT (p."Id" = ANY(@excludeIds))
            ORDER BY st.units DESC
            LIMIT @take
            """;

        var rows = await conn.QueryAsync<RecommendationCandidate>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, excludeIds = excludeProductIds.ToArray(), take },
                cancellationToken: ct));

        return rows.ToList().AsReadOnly();
    }

    // FASE COLD START — "El chef recomienda": productos con tag Popular o Recommended.
    public async Task<IReadOnlyList<RecommendationCandidate>> GetChefRecommendedAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> excludeProductIds, int take, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                "Id"       AS ProductId,
                "Name"     AS Name,
                "ImageUrl" AS ImageUrl,
                price_amount   AS Price,
                price_currency AS Currency,
                1::numeric AS Weight
            FROM products
            WHERE "TenantId"     = @tenantId
              AND "RestaurantId" = @restaurantId
              AND "IsAvailable"  = true
              AND tags && ARRAY['Popular','Recommended']
              AND NOT ("Id" = ANY(@excludeIds))
            ORDER BY "SortOrder"
            LIMIT @take
            """;

        var rows = await conn.QueryAsync<RecommendationCandidate>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, excludeIds = excludeProductIds.ToArray(), take },
                cancellationToken: ct));

        return rows.ToList().AsReadOnly();
    }

    // FASE INTERMEDIA — collaborative filtering simplificado: co-occurrence matrix.
    // Adaptado a jsonb_array_elements porque los pedidos guardan sus items como
    // columna JSONB (Order.Items, owned collection) en vez de una tabla
    // order_items normalizada — el join clásico oi1/oi2 no aplica tal cual aquí.
    public async Task<IReadOnlyList<RecommendationCandidate>> GetCoOccurringAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> cartProductIds, int take, CancellationToken ct = default)
    {
        if (cartProductIds.Count == 0) return [];

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            WITH cart_orders AS (
                SELECT DISTINCT o."Id" AS order_id
                FROM orders o, jsonb_array_elements(o.items) AS item
                WHERE o."TenantId"     = @tenantId
                  AND o."RestaurantId" = @restaurantId
                  AND o."Status"      <> 'Cancelled'
                  AND o.items IS NOT NULL
                  AND (item->>'ProductId')::uuid = ANY(@cartProductIds)
            ),
            co_occurrence AS (
                SELECT
                    (item->>'ProductId')::uuid AS product_id,
                    COUNT(DISTINCT o."Id")     AS frequency
                FROM orders o
                JOIN cart_orders co ON co.order_id = o."Id"
                CROSS JOIN LATERAL jsonb_array_elements(o.items) AS item
                WHERE NOT ((item->>'ProductId')::uuid = ANY(@cartProductIds))
                GROUP BY item->>'ProductId'
            )
            SELECT
                p."Id"       AS ProductId,
                p."Name"     AS Name,
                p."ImageUrl" AS ImageUrl,
                p.price_amount   AS Price,
                p.price_currency AS Currency,
                co.frequency::numeric AS Weight
            FROM co_occurrence co
            JOIN products p ON p."Id" = co.product_id AND p."TenantId" = @tenantId
            WHERE p."IsAvailable" = true
            ORDER BY co.frequency DESC
            LIMIT @take
            """;

        var rows = await conn.QueryAsync<RecommendationCandidate>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, cartProductIds = cartProductIds.ToArray(), take },
                cancellationToken: ct));

        return rows.ToList().AsReadOnly();
    }

    // Reglas manuales de maridaje configuradas por el restaurante — máxima
    // prioridad en todas las fases.
    public async Task<IReadOnlyList<RecommendationCandidate>> GetManualPairingsAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> cartProductIds, CancellationToken ct = default)
    {
        if (cartProductIds.Count == 0) return [];

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT DISTINCT
                p."Id"       AS ProductId,
                p."Name"     AS Name,
                p."ImageUrl" AS ImageUrl,
                p.price_amount   AS Price,
                p.price_currency AS Currency,
                1::numeric AS Weight
            FROM product_pairing_rules r
            JOIN products p ON p."Id" = r."TargetProductId" AND p."TenantId" = @tenantId
            WHERE r."TenantId"     = @tenantId
              AND r."RestaurantId" = @restaurantId
              AND r."IsActive"     = true
              AND r."SourceProductId" = ANY(@cartProductIds)
              AND NOT (r."TargetProductId" = ANY(@cartProductIds))
              AND p."IsAvailable" = true
            """;

        var rows = await conn.QueryAsync<RecommendationCandidate>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, cartProductIds = cartProductIds.ToArray() },
                cancellationToken: ct));

        return rows.ToList().AsReadOnly();
    }
}
