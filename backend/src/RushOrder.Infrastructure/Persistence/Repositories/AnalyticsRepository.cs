using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Analytics.DTOs;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Persistence;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class AnalyticsRepository : IAnalyticsRepository
{
    private readonly string _connectionString;
    private readonly Guid _tenantId;

    public AnalyticsRepository(AppDbContext context, ICurrentTenantService tenant)
    {
        _connectionString = context.Database.GetConnectionString()!;
        _tenantId = tenant.TenantId ?? Guid.Empty;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<DashboardDto> GetDashboardAsync(Guid restaurantId, DateOnly date, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string statsQuery = """
            WITH order_stats AS (
                SELECT
                    COUNT(*) FILTER (WHERE "Status" <> 'Cancelled')                            AS total_orders,
                    COUNT(*) FILTER (WHERE "Status" = 'Pending')                               AS pending_orders,
                    COUNT(*) FILTER (WHERE "Status" IN ('Confirmed','Preparing','Ready'))       AS in_progress,
                    COUNT(*) FILTER (WHERE "Status" IN ('Served','Paid'))                      AS completed,
                    COALESCE(SUM(total_amount) FILTER (WHERE "Status" <> 'Cancelled'), 0)      AS revenue_today,
                    CASE WHEN COUNT(*) FILTER (WHERE "Status" <> 'Cancelled') > 0
                         THEN SUM(total_amount) FILTER (WHERE "Status" <> 'Cancelled')
                              / COUNT(*) FILTER (WHERE "Status" <> 'Cancelled')
                         ELSE 0 END                                                             AS avg_ticket_today
                FROM orders
                WHERE "TenantId"     = @tenantId
                  AND "RestaurantId" = @restaurantId
                  AND "CreatedAt"::date = @date
            ),
            yesterday_stats AS (
                SELECT
                    COALESCE(SUM(total_amount) FILTER (WHERE "Status" <> 'Cancelled'), 0)      AS revenue_yesterday,
                    CASE WHEN COUNT(*) FILTER (WHERE "Status" <> 'Cancelled') > 0
                         THEN SUM(total_amount) FILTER (WHERE "Status" <> 'Cancelled')
                              / COUNT(*) FILTER (WHERE "Status" <> 'Cancelled')
                         ELSE 0 END                                                             AS avg_ticket_yesterday
                FROM orders
                WHERE "TenantId"     = @tenantId
                  AND "RestaurantId" = @restaurantId
                  AND "CreatedAt"::date = @date - INTERVAL '1 day'
            ),
            table_stats AS (
                SELECT
                    COUNT(*) FILTER (WHERE "Status" = 'Occupied') AS occupied,
                    COUNT(*)                                       AS total_tables
                FROM tables
                WHERE "TenantId"     = @tenantId
                  AND "RestaurantId" = @restaurantId
            ),
            avg_occupation AS (
                SELECT COALESCE(AVG(
                    EXTRACT(EPOCH FROM (NOW() - first_order."CreatedAt")) / 60
                ), 0) AS avg_min
                FROM tables t
                CROSS JOIN LATERAL (
                    SELECT "CreatedAt"
                    FROM orders
                    WHERE "TableId"  = t."Id"
                      AND "TenantId" = @tenantId
                      AND "Status" NOT IN ('Cancelled','Paid')
                    ORDER BY "CreatedAt"
                    LIMIT 1
                ) first_order
                WHERE t."TenantId"     = @tenantId
                  AND t."RestaurantId" = @restaurantId
                  AND t."Status"       = 'Occupied'
            )
            SELECT
                (SELECT total_orders        FROM order_stats)      AS TotalOrders,
                (SELECT pending_orders      FROM order_stats)      AS PendingOrders,
                (SELECT in_progress         FROM order_stats)      AS InProgress,
                (SELECT completed           FROM order_stats)      AS Completed,
                (SELECT revenue_today       FROM order_stats)      AS RevenueToday,
                (SELECT revenue_yesterday   FROM yesterday_stats)  AS RevenueYesterday,
                (SELECT avg_ticket_today    FROM order_stats)      AS AvgTicketToday,
                (SELECT avg_ticket_yesterday FROM yesterday_stats) AS AvgTicketYesterday,
                (SELECT occupied            FROM table_stats)      AS TablesOccupied,
                (SELECT total_tables        FROM table_stats)      AS TotalTables,
                (SELECT avg_min             FROM avg_occupation)   AS AvgOccupationMin
            """;

        var utcDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var row = await conn.QuerySingleAsync<DashboardStatsRow>(
            new CommandDefinition(statsQuery,
                new { tenantId = _tenantId, restaurantId, date = utcDate },
                cancellationToken: ct));

        // Top 5 products from JSONB items.
        // EF Core 8 stores owned properties using their model names:
        //   item properties: "Name", "Quantity"; nested: "UnitPrice"."unit_price_amount"
        const string topProductsQuery = """
            SELECT
                item->>'Name'                                                              AS name,
                SUM((item->>'Quantity')::int)                                              AS quantity,
                SUM((item->'UnitPrice'->>'unit_price_amount')::numeric
                    * (item->>'Quantity')::int)                                            AS revenue
            FROM orders,
                 jsonb_array_elements(items) AS item
            WHERE "TenantId"     = @tenantId
              AND "RestaurantId" = @restaurantId
              AND "CreatedAt"::date = @date
              AND "Status"       <> 'Cancelled'
              AND items IS NOT NULL
            GROUP BY item->>'Name'
            ORDER BY revenue DESC
            LIMIT 5
            """;

        var topProducts = (await conn.QueryAsync<(string name, int quantity, decimal revenue)>(
                new CommandDefinition(topProductsQuery,
                    new { tenantId = _tenantId, restaurantId, date = utcDate },
                    cancellationToken: ct)))
            .Select(r => new TopProductDto(r.name, r.quantity, r.revenue))
            .ToList()
            .AsReadOnly();

        var alerts = await GetActiveAlertsForRestaurantAsync(conn, restaurantId, _tenantId, ct);

        return BuildDashboardDto(row, topProducts, alerts);
    }

    // ── Sales series ──────────────────────────────────────────────────────────

    public async Task<SalesDto> GetSalesAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string groupBy,
        CancellationToken ct = default)
    {
        // Validated whitelist — truncUnit is never derived from raw user input
        var truncUnit = groupBy switch
        {
            "hour"  => "hour",
            "week"  => "week",
            "month" => "month",
            _       => "day"
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var seriesQuery = $"""
            SELECT
                DATE_TRUNC('{truncUnit}', "CreatedAt")                                   AS date,
                COALESCE(SUM(total_amount) FILTER (WHERE "Status" <> 'Cancelled'), 0)   AS revenue,
                COUNT(*)            FILTER (WHERE "Status" <> 'Cancelled')               AS orders,
                COUNT(*)            FILTER (WHERE "Status" IN ('Served','Paid'))         AS covers
            FROM orders
            WHERE "TenantId"     = @tenantId
              AND "RestaurantId" = @restaurantId
              AND "CreatedAt" BETWEEN @from AND @to
            GROUP BY DATE_TRUNC('{truncUnit}', "CreatedAt")
            ORDER BY date
            """;

        var seriesRows = (await conn.QueryAsync<SalesSeriesRow>(
                new CommandDefinition(seriesQuery,
                    new { tenantId = _tenantId, restaurantId, from, to },
                    cancellationToken: ct)))
            .ToList();

        var series       = seriesRows.Select(r => new SalesSeriesPoint(r.date, r.revenue, r.orders, r.covers)).ToList().AsReadOnly();
        var totalRevenue = series.Sum(s => s.Revenue);
        var totalOrders  = series.Sum(s => s.Orders);
        var avgTicket    = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;
        var bestPoint    = seriesRows.Count > 0 ? seriesRows.MaxBy(r => r.revenue) : null;
        var worstPoint   = seriesRows.Count > 1 ? seriesRows.MinBy(r => r.revenue) : null;

        return new SalesDto(series, new SalesTotals(
            Revenue:   totalRevenue,
            Orders:    totalOrders,
            AvgTicket: avgTicket,
            BestDay:   bestPoint?.date,
            WorstDay:  worstPoint?.date != bestPoint?.date ? worstPoint?.date : null));
    }

    // ── Product performance ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductPerformanceDto>> GetProductPerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var prevFrom = from - (to - from);

        const string sql = """
            WITH current_period AS (
                SELECT
                    item->>'ProductId'                                               AS product_id,
                    item->>'Name'                                                    AS name,
                    SUM((item->>'Quantity')::int)                                    AS quantity,
                    SUM((item->'UnitPrice'->>'unit_price_amount')::numeric
                        * (item->>'Quantity')::int)                                  AS revenue
                FROM orders,
                     jsonb_array_elements(items) AS item
                WHERE "TenantId"     = @tenantId
                  AND "RestaurantId" = @restaurantId
                  AND "CreatedAt" BETWEEN @from AND @to
                  AND "Status"    <> 'Cancelled'
                  AND items IS NOT NULL
                GROUP BY item->>'ProductId', item->>'Name'
            ),
            prev_period AS (
                SELECT
                    item->>'ProductId'           AS product_id,
                    SUM((item->>'Quantity')::int) AS quantity
                FROM orders,
                     jsonb_array_elements(items) AS item
                WHERE "TenantId"     = @tenantId
                  AND "RestaurantId" = @restaurantId
                  AND "CreatedAt" BETWEEN @prevFrom AND @from
                  AND "Status"    <> 'Cancelled'
                  AND items IS NOT NULL
                GROUP BY item->>'ProductId'
            )
            SELECT
                cp.product_id::uuid                            AS ProductId,
                cp.name                                        AS Name,
                COALESCE(cat."Name", 'Sin categoría')          AS Category,
                cp.quantity                                    AS QuantitySold,
                cp.revenue                                     AS Revenue,
                CASE
                    WHEN COALESCE(pp.quantity, 0) = 0         THEN 'up'
                    WHEN cp.quantity > pp.quantity * 1.1      THEN 'up'
                    WHEN cp.quantity < pp.quantity * 0.9      THEN 'down'
                    ELSE 'stable'
                END                                            AS Trend
            FROM current_period cp
            LEFT JOIN prev_period pp
                   ON pp.product_id = cp.product_id
            LEFT JOIN products p
                   ON p."Id"       = cp.product_id::uuid
                  AND p."TenantId" = @tenantId
            LEFT JOIN categories cat
                   ON cat."Id"       = p."CategoryId"
                  AND cat."TenantId" = @tenantId
                  AND cat."IsDeleted" = false
            ORDER BY cp.revenue DESC
            """;

        var rows = await conn.QueryAsync<ProductPerformanceRow>(
            new CommandDefinition(sql,
                new { tenantId = _tenantId, restaurantId, from, to, prevFrom },
                cancellationToken: ct));

        return rows
            .Select(r => new ProductPerformanceDto(
                r.ProductId, r.Name, r.Category,
                r.QuantitySold, r.Revenue,
                AvgRating: null,
                r.Trend,
                MarginEstimate: null))
            .ToList()
            .AsReadOnly();
    }

    // ── Table performance ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TablePerformanceDto>> GetTablePerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var totalHours = (to - from).TotalHours;

        const string sql = """
            WITH table_orders AS (
                SELECT
                    o."TableId",
                    COUNT(*)    FILTER (WHERE o."Status" NOT IN ('Cancelled'))               AS orders_served,
                    COALESCE(
                        SUM(o.total_amount) FILTER (WHERE o."Status" NOT IN ('Cancelled')), 0
                    )                                                                         AS revenue,
                    COALESCE(AVG(
                        EXTRACT(EPOCH FROM (o."UpdatedAt" - o."CreatedAt")) / 60
                    ) FILTER (WHERE o."Status" IN ('Served','Paid')), 0)                     AS avg_turnover_min
                FROM orders o
                WHERE o."TenantId"     = @tenantId
                  AND o."RestaurantId" = @restaurantId
                  AND o."CreatedAt" BETWEEN @from AND @to
                GROUP BY o."TableId"
            )
            SELECT
                t."Id"                                             AS TableId,
                t."Name"                                           AS Name,
                t."Zone"                                           AS Zone,
                COALESCE(tord.orders_served, 0)                    AS OrdersServed,
                COALESCE(tord.revenue, 0)                          AS Revenue,
                COALESCE(tord.avg_turnover_min, 0)                 AS AvgTurnoverMinutes,
                CASE WHEN @totalHours > 0 AND COALESCE(tord.avg_turnover_min, 0) > 0
                     THEN LEAST(
                              tord.avg_turnover_min * COALESCE(tord.orders_served, 0)
                              / (@totalHours * 60.0) * 100.0
                          , 100.0)
                     ELSE 0.0 END                                  AS OccupancyPercent
            FROM tables t
            LEFT JOIN table_orders tord ON tord."TableId" = t."Id"
            WHERE t."TenantId"     = @tenantId
              AND t."RestaurantId" = @restaurantId
            ORDER BY COALESCE(tord.revenue, 0) DESC
            """;

        var rows = await conn.QueryAsync<TablePerformanceRow>(
            new CommandDefinition(sql,
                new { tenantId = _tenantId, restaurantId, from, to, totalHours },
                cancellationToken: ct));

        return rows
            .Select(r => new TablePerformanceDto(
                r.TableId, r.Name, r.Zone,
                Math.Round((decimal)r.OccupancyPercent, 1),
                Math.Round(r.AvgTurnoverMinutes, 1),
                r.Revenue,
                r.OrdersServed))
            .ToList()
            .AsReadOnly();
    }

    // ── Waiter performance ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<WaiterPerformanceDto>> GetWaiterPerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                u."Id"                                                                  AS WaiterId,
                (u."FirstName" || ' ' || u."LastName")                                 AS Name,
                COUNT(o."Id")  FILTER (WHERE o."Id" IS NOT NULL
                                         AND o."Status" NOT IN ('Cancelled'))           AS OrdersServed,
                COALESCE(SUM(o.total_amount)
                    FILTER (WHERE o."Status" NOT IN ('Cancelled')), 0)                 AS Revenue,
                CASE WHEN COUNT(o."Id") FILTER (WHERE o."Status" NOT IN ('Cancelled')) > 0
                     THEN SUM(o.total_amount)
                              FILTER (WHERE o."Status" NOT IN ('Cancelled'))
                          / COUNT(o."Id")
                              FILTER (WHERE o."Status" NOT IN ('Cancelled'))
                     ELSE 0 END                                                         AS AvgTicket
            FROM users u
            LEFT JOIN orders o
                   ON o."WaiterId"     = u."Id"
                  AND o."TenantId"     = @tenantId
                  AND o."RestaurantId" = @restaurantId
                  AND o."CreatedAt" BETWEEN @from AND @to
            WHERE u."TenantId" = @tenantId
              AND u."Role"     = 'Waiter'
              AND u."IsActive" = true
              AND @restaurantId = ANY(u.restaurant_ids)
            GROUP BY u."Id", u."FirstName", u."LastName"
            ORDER BY Revenue DESC
            """;

        var rows = await conn.QueryAsync<WaiterPerformanceRow>(
            new CommandDefinition(sql,
                new { tenantId = _tenantId, restaurantId, from, to },
                cancellationToken: ct));

        return rows
            .Select(r => new WaiterPerformanceDto(
                r.WaiterId, r.Name, r.OrdersServed,
                AvgRating: null,
                r.Revenue, r.AvgTicket))
            .ToList()
            .AsReadOnly();
    }

    // ── Active alerts — shared with AlertMonitoringService ────────────────────

    internal static async Task<IReadOnlyList<ActiveAlertDto>> GetActiveAlertsForRestaurantAsync(
        NpgsqlConnection conn,
        Guid restaurantId,
        Guid tenantId,
        CancellationToken ct)
    {
        var alerts = new List<ActiveAlertDto>();

        const string pendingQuery = """
            SELECT "OrderNumber",
                   EXTRACT(EPOCH FROM (NOW() - "CreatedAt")) / 60 AS Minutes
            FROM orders
            WHERE "TenantId"     = @tenantId
              AND "RestaurantId" = @restaurantId
              AND "Status"       = 'Pending'
              AND "CreatedAt"    < NOW() - INTERVAL '10 minutes'
            """;
        var pending = await conn.QueryAsync<(string OrderNumber, double Minutes)>(
            new CommandDefinition(pendingQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        alerts.AddRange(pending.Select(p => new ActiveAlertDto(
            "pending_order",
            $"Pedido {p.OrderNumber} sin confirmar hace {(int)p.Minutes} min",
            "warning")));

        const string stockQuery = """
            SELECT "Name", "StockQuantity"
            FROM products
            WHERE "TenantId"      = @tenantId
              AND "RestaurantId"  = @restaurantId
              AND "StockTracking" = true
              AND "StockQuantity" <= 5
              AND "IsAvailable"   = true
            """;
        var lowStock = await conn.QueryAsync<(string Name, int StockQuantity)>(
            new CommandDefinition(stockQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        alerts.AddRange(lowStock.Select(s => new ActiveAlertDto(
            "low_stock",
            $"Stock bajo: {s.Name} ({s.StockQuantity} unidades)",
            "critical")));

        const string occupationQuery = """
            SELECT t."Name"                                                               AS TableName,
                   EXTRACT(EPOCH FROM (NOW() - MIN(o."CreatedAt"))) / 60                AS Minutes
            FROM tables t
            INNER JOIN orders o
                    ON o."TableId"  = t."Id"
                   AND o."TenantId" = @tenantId
                   AND o."Status" NOT IN ('Cancelled','Paid')
            WHERE t."TenantId"     = @tenantId
              AND t."RestaurantId" = @restaurantId
              AND t."Status"       = 'Occupied'
            GROUP BY t."Id", t."Name"
            HAVING EXTRACT(EPOCH FROM (NOW() - MIN(o."CreatedAt"))) / 60 > 120
            """;
        var longOccupied = await conn.QueryAsync<(string TableName, double Minutes)>(
            new CommandDefinition(occupationQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        alerts.AddRange(longOccupied.Select(l => new ActiveAlertDto(
            "long_occupation",
            $"Mesa {l.TableName} ocupada hace {(int)l.Minutes} min",
            "info")));

        // ── Anomaly detection (IA) ──────────────────────────────────────────

        const string negativeRatingsBurstQuery = """
            WITH low_rated_orders AS (
                SELECT o."Id"
                FROM order_ratings r
                JOIN orders o ON o."Id" = r."OrderId"
                WHERE r."TenantId" = @tenantId AND r."RestaurantId" = @restaurantId
                  AND r."FoodRating" < 2
                  AND r."CreatedAt" >= NOW() - INTERVAL '1 hour'
            )
            SELECT item->>'Name' AS ProductName, COUNT(*) AS Occurrences
            FROM orders o, jsonb_array_elements(o.items) AS item
            WHERE o."Id" IN (SELECT "Id" FROM low_rated_orders)
            GROUP BY item->>'Name'
            HAVING COUNT(*) >= 3
            """;
        var negativeBursts = await conn.QueryAsync<(string ProductName, int Occurrences)>(
            new CommandDefinition(negativeRatingsBurstQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        alerts.AddRange(negativeBursts.Select(b => new ActiveAlertDto(
            "negative_ratings_burst",
            $"{b.ProductName}: {b.Occurrences} valoraciones de comida < 2★ en la última hora",
            "critical")));

        const string salesDropQuery = """
            WITH this_week AS (
                SELECT item->>'Name' AS name, SUM((item->>'Quantity')::int) AS qty
                FROM orders o, jsonb_array_elements(o.items) AS item
                WHERE o."TenantId" = @tenantId AND o."RestaurantId" = @restaurantId AND o."Status" <> 'Cancelled'
                  AND o."CreatedAt" >= NOW() - INTERVAL '7 days' AND o.items IS NOT NULL
                GROUP BY item->>'Name'
            ),
            last_week AS (
                SELECT item->>'Name' AS name, SUM((item->>'Quantity')::int) AS qty
                FROM orders o, jsonb_array_elements(o.items) AS item
                WHERE o."TenantId" = @tenantId AND o."RestaurantId" = @restaurantId AND o."Status" <> 'Cancelled'
                  AND o."CreatedAt" >= NOW() - INTERVAL '14 days' AND o."CreatedAt" < NOW() - INTERVAL '7 days'
                  AND o.items IS NOT NULL
                GROUP BY item->>'Name'
            )
            SELECT tw.name AS ProductName,
                   ROUND((lw.qty - tw.qty)::numeric / lw.qty * 100, 1) AS DropPercent
            FROM this_week tw
            JOIN last_week lw ON lw.name = tw.name
            WHERE lw.qty >= 5 -- ignore near-zero baselines, too noisy to be meaningful
              AND (lw.qty - tw.qty)::numeric / lw.qty > 0.40
            """;
        var salesDrops = await conn.QueryAsync<(string ProductName, decimal DropPercent)>(
            new CommandDefinition(salesDropQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        alerts.AddRange(salesDrops.Select(s => new ActiveAlertDto(
            "sales_drop",
            $"¿Problema con este plato? {s.ProductName} cayó {s.DropPercent}% frente a la semana pasada",
            "warning")));

        const string avgTicketDropQuery = """
            SELECT
                COALESCE(AVG(total_amount) FILTER (WHERE "CreatedAt"::date = CURRENT_DATE), 0)                  AS Today,
                COALESCE(AVG(total_amount) FILTER (WHERE "CreatedAt"::date = CURRENT_DATE - INTERVAL '7 days'), 0) AS SameDayLastWeek
            FROM orders
            WHERE "TenantId" = @tenantId AND "RestaurantId" = @restaurantId AND "Status" <> 'Cancelled'
              AND "CreatedAt"::date IN (CURRENT_DATE, CURRENT_DATE - INTERVAL '7 days')
            """;
        var ticket = await conn.QuerySingleAsync<(decimal Today, decimal SameDayLastWeek)>(
            new CommandDefinition(avgTicketDropQuery, new { tenantId, restaurantId }, cancellationToken: ct));

        if (ticket.SameDayLastWeek > 0)
        {
            var ticketDropPercent = Math.Round((ticket.SameDayLastWeek - ticket.Today) / ticket.SameDayLastWeek * 100, 1);
            if (ticketDropPercent > 20)
            {
                alerts.Add(new ActiveAlertDto(
                    "avg_ticket_drop",
                    $"El ticket medio de hoy cayó {ticketDropPercent}% frente al mismo día de la semana pasada",
                    "warning"));
            }
        }

        return alerts.AsReadOnly();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static DashboardDto BuildDashboardDto(
        DashboardStatsRow row,
        IReadOnlyList<TopProductDto> topProducts,
        IReadOnlyList<ActiveAlertDto> alerts)
    {
        var revenue = new RevenueStat(
            Today: row.RevenueToday,
            Yesterday: row.RevenueYesterday,
            ChangePercent: row.RevenueYesterday == 0m
                ? (row.RevenueToday > 0m ? 100m : 0m)
                : Math.Round((row.RevenueToday - row.RevenueYesterday) / row.RevenueYesterday * 100m, 1));

        var avgTicket = new TicketStat(
            Value: row.AvgTicketToday,
            ChangePercent: row.AvgTicketYesterday == 0m
                ? (row.AvgTicketToday > 0m ? 100m : 0m)
                : Math.Round((row.AvgTicketToday - row.AvgTicketYesterday) / row.AvgTicketYesterday * 100m, 1));

        var totalTables  = Math.Max(row.TotalTables, 1);
        var occupancyPct = Math.Round((decimal)row.TablesOccupied / totalTables * 100m, 1);

        return new DashboardDto(
            Revenue: revenue,
            Orders:  new OrderStat(row.TotalOrders, row.PendingOrders, row.InProgress, row.Completed),
            Covers:  new CoverStat(
                Total:      row.Completed,
                AvgPerTable: Math.Round((decimal)row.Completed / totalTables, 2)),
            AvgTicket:      avgTicket,
            TopProducts:    topProducts,
            TableOccupancy: new TableOccupancyStat(occupancyPct, Math.Round(row.AvgOccupationMin, 1)),
            ActiveAlerts:   alerts);
    }

    // ── Dapper row types ──────────────────────────────────────────────────────

    private sealed class DashboardStatsRow
    {
        public int     TotalOrders        { get; init; }
        public int     PendingOrders      { get; init; }
        public int     InProgress         { get; init; }
        public int     Completed          { get; init; }
        public decimal RevenueToday       { get; init; }
        public decimal RevenueYesterday   { get; init; }
        public decimal AvgTicketToday     { get; init; }
        public decimal AvgTicketYesterday { get; init; }
        public int     TablesOccupied     { get; init; }
        public int     TotalTables        { get; init; }
        public double  AvgOccupationMin   { get; init; }
    }

    private sealed class SalesSeriesRow
    {
        public DateTimeOffset date    { get; init; }
        public decimal        revenue { get; init; }
        public int            orders  { get; init; }
        public int            covers  { get; init; }
    }

    private sealed class ProductPerformanceRow
    {
        public Guid    ProductId    { get; init; }
        public string  Name         { get; init; } = string.Empty;
        public string  Category     { get; init; } = string.Empty;
        public int     QuantitySold { get; init; }
        public decimal Revenue      { get; init; }
        public string  Trend        { get; init; } = "stable";
    }

    private sealed class TablePerformanceRow
    {
        public Guid    TableId            { get; init; }
        public string  Name               { get; init; } = string.Empty;
        public string? Zone               { get; init; }
        public int     OrdersServed       { get; init; }
        public decimal Revenue            { get; init; }
        public double  AvgTurnoverMinutes { get; init; }
        public double  OccupancyPercent   { get; init; }
    }

    private sealed class WaiterPerformanceRow
    {
        public Guid    WaiterId     { get; init; }
        public string  Name         { get; init; } = string.Empty;
        public int     OrdersServed { get; init; }
        public decimal Revenue      { get; init; }
        public decimal AvgTicket    { get; init; }
    }
}
