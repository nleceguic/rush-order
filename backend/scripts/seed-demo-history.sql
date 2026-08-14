-- ============================================================================
-- seed-demo-history.sql
--
-- PURPOSE
--   Backfills realistic *historical* orders for "El Rincón del Chef" (the
--   tenant/restaurant created by DatabaseSeeder.SeedDevelopmentDataAsync,
--   slug 'rincon-chef') so DemandForecastEngine has enough same
--   weekday+hour history to reach ForecastConfidence.Medium/High on several
--   products, instead of the Low/red state you get from the 5 sample orders
--   DatabaseSeeder creates on every startup.
--
--   DemandForecastEngine (backend/src/RushOrder.Application/Forecasting/
--   DemandForecastEngine.cs) buckets history by (ProductId, DayOfWeek, Hour)
--   and needs >=4 distinct weeks in a bucket for High, >=2 for Medium — and
--   GetDemandForecastQuery reports, per product, the WORST confidence across
--   every operating hour of the requested day. That means a product only
--   reads as "High" for a given day if it has >=4 weeks of history in EVERY
--   operating hour (09:00-23:00 for this restaurant) of that specific
--   weekday — partial coverage (e.g. only lunch) still shows Low for the
--   hours with no data. So this script deliberately gives a small set of
--   "anchor" products full-hour, full-week-of-year coverage (see
--   ANCHOR PRODUCTS below), on top of sparser background orders for the
--   rest of the menu, so the demo looks right regardless of which day of
--   the week you actually record on.
--
-- WHEN / HOW TO RUN
--   - One-off, by hand, against your LOCAL dev database, before recording
--     the forecasting demo. It is NOT part of DatabaseSeeder and does NOT
--     run automatically on API startup — DatabaseSeeder.cs is untouched.
--   - Prerequisite: DatabaseSeeder.SeedDevelopmentDataAsync must already
--     have run (tenant 'rincon-chef', its restaurant/products/tables must
--     exist). The script aborts loudly if it doesn't find them.
--   - Run with:
--       psql "postgresql://rushorder:rushorder_dev_pass@localhost:5432/rushorder_dev" \
--         -f backend/scripts/seed-demo-history.sql
--     (adjust host/port/credentials to match your environment/.env).
--   - Safe to re-run: every row it creates is tagged with
--     Notes = 'demo-history-seed', and the script deletes its own
--     previously-generated rows (orders + order_status_history) before
--     inserting again, so running it twice does not double the data.
--
-- IMPORTANT — this only backfills `orders`, not the forecast cache
--   /analytics/demand-forecast reads from the `demand_forecasts` table,
--   which is a daily PRE-COMPUTED cache written by DemandForecastJob
--   (backend/src/RushOrder.Infrastructure/Services/DemandForecastJob.cs).
--   That job only runs once a day (06:00 UTC) and only once per calendar
--   day. Running this script does NOT retroactively refresh that cache —
--   you still need the job to run at least once *after* seeding (either
--   let the API sit running through 06:00 UTC, or temporarily adjust the
--   system clock in a throwaway environment). This script's job is only to
--   make sure that, whenever the job does run, it has good history to
--   compute Medium/High confidence from.
--
--   While verifying this end-to-end (seeding -> job -> GetDemandForecastQuery)
--   two independent, pre-existing Dapper/Postgres type-mapping bugs were
--   found and fixed alongside this script — without them the job/query threw
--   on every single call, regardless of how much history existed:
--     - HistoricalSaleRow.LocalDate was DateOnly but the raw SQL's `::date`
--       materializes as DateTime; Quantity was int but SUM(...) returns
--       bigint. Fixed in IDemandForecastRepository.cs (record) and
--       DemandForecastRepository.cs (added an explicit ::int cast).
--     - GetForecastRowsAsync passed a DateOnly straight into a Dapper
--       parameter, which this Dapper version doesn't know how to bind.
--       Fixed by converting it to DateTime before the query.
--   Verified by running DemandForecastEngine + GetDemandForecastQueryHandler
--   for real (not just SQL) against this script's seeded data: all 7
--   forecast days come back with 6 products at High confidence and 14 at Low.
--
-- SCOPE
--   Only inserts into `orders` and `order_status_history` (so the orders
--   are consistent with the domain: valid item totals, a full Pending ->
--   ... -> Paid status trail). No Payment rows are created — the existing
--   DatabaseSeeder sample orders don't create them for Paid orders either,
--   and the forecast/analytics paths touched here don't read `payments`.
-- ============================================================================

BEGIN;

-- ── 0. Resolve tenant/restaurant/tables and clean up any previous run ──────

CREATE TEMP TABLE ctx AS
SELECT
    t."Id"                                                            AS tenant_id,
    r."Id"                                                            AS restaurant_id,
    r."Timezone"                                                      AS tz,
    (now() AT TIME ZONE r."Timezone")::date                          AS today_local,
    (now() AT TIME ZONE r."Timezone")::date
        - EXTRACT(DOW FROM (now() AT TIME ZONE r."Timezone"))::int    AS week_start -- this week's Sunday, local calendar
FROM tenants t
JOIN restaurants r ON r."TenantId" = t."Id"
WHERE t."Slug" = 'rincon-chef'
LIMIT 1;

DO $$
BEGIN
    IF (SELECT count(*) FROM ctx) = 0 THEN
        RAISE EXCEPTION 'Tenant ''rincon-chef'' (or its restaurant) not found. Run DatabaseSeeder.SeedDevelopmentDataAsync first.';
    END IF;
END $$;

-- Idempotent re-run: drop whatever this script generated last time.
DELETE FROM order_status_history
WHERE "OrderId" IN (
    SELECT "Id" FROM orders
    WHERE "TenantId" = (SELECT tenant_id FROM ctx) AND "Notes" = 'demo-history-seed'
);
DELETE FROM orders
WHERE "TenantId" = (SELECT tenant_id FROM ctx) AND "Notes" = 'demo-history-seed';

CREATE TEMP TABLE table_list AS
SELECT tb."Id" AS id
FROM tables tb
WHERE tb."RestaurantId" = (SELECT restaurant_id FROM ctx);

-- ── 1. Reference data: anchor products, background products, weights ───────

-- ANCHOR PRODUCTS — one from each course + two drinks, so the demo shows
-- Medium/High confidence spread across categories, not just one dish.
-- These get an order in *every* operating hour, *every* day of the week,
-- across all WEEKS_BACK weeks below (see step 2), which is what guarantees
-- >=4 weeks of same weekday+hour history everywhere.
CREATE TEMP TABLE anchor_products AS
SELECT p."Id" AS product_id, p."Name" AS name, p.price_amount AS price, v.base_qty
FROM products p
JOIN (VALUES
    ('Croquetas de jamón ibérico', 1.0),  -- starter/tapa
    ('Paella valenciana',          1.0),  -- main
    ('Entrecot de ternera (250g)', 1.0),  -- main
    ('Crema catalana',             1.5),  -- dessert
    ('Agua mineral (50cl)',        1.5),  -- drink
    ('Cerveza artesana (33cl)',    1.5)   -- drink
) AS v(name, base_qty) ON v.name = p."Name"
WHERE p."TenantId" = (SELECT tenant_id FROM ctx)
  AND p."RestaurantId" = (SELECT restaurant_id FROM ctx);

-- Everything else — ordered sparsely, at random hours, for background
-- variety. Most of these will stay Low/Medium confidence, which is fine
-- (and realistic — not every dish should forecast cleanly).
CREATE TEMP TABLE background_products AS
SELECT p."Id" AS product_id, p."Name" AS name, p.price_amount AS price
FROM products p
WHERE p."TenantId" = (SELECT tenant_id FROM ctx)
  AND p."RestaurantId" = (SELECT restaurant_id FROM ctx)
  AND p."Id" NOT IN (SELECT product_id FROM anchor_products);

-- Day-of-week volume multiplier — mirrors DemandForecastEngine's own
-- DayMultipliers (Postgres EXTRACT(DOW): 0=Sunday..6=Saturday) purely so
-- the seeded volume story ("busier Fri/Sat/Sun") matches what the engine
-- will later predict; the engine doesn't read this table, it's just for
-- generating plausible-looking history.
CREATE TEMP TABLE dow_weight(dow int, weight numeric);
INSERT INTO dow_weight VALUES
    (0, 1.30), (1, 0.80), (2, 0.85), (3, 0.90), (4, 1.00), (5, 1.20), (6, 1.40);

-- Hour-of-day shape — quiet mid-morning/mid-afternoon, lunch peak
-- (13:00-15:00), dinner peak (20:00-22:00).
CREATE TEMP TABLE hour_weight(hour int, weight numeric);
INSERT INTO hour_weight VALUES
    (9, 0.4), (10, 0.4), (11, 0.5), (12, 0.8), (13, 1.4), (14, 1.5), (15, 1.2),
    (16, 0.6), (17, 0.5), (18, 0.6), (19, 0.9), (20, 1.4), (21, 1.6), (22, 1.3), (23, 0.6);

-- ── 2. Time slots: every (week, weekday, hour) for the last 8 full weeks ───
-- week_offset 1..8 => 8 full calendar weeks strictly before the current one
-- (so nothing lands in the future, whatever "today" turns out to be).
-- Restaurant operates 09:00-23:00 local (Restaurant.Settings default,
-- confirmed against the seeded restaurant's Settings JSON).
CREATE TEMP TABLE slots AS
SELECT
    w.week_offset,
    d.dow,
    h.hour,
    ((c.week_start - (w.week_offset * 7) + d.dow)::timestamp
        + make_interval(hours => h.hour)) AT TIME ZONE c.tz AS created_at
FROM ctx c
CROSS JOIN generate_series(1, 8) AS w(week_offset)
CROSS JOIN generate_series(0, 6) AS d(dow)
CROSS JOIN generate_series(9, 23) AS h(hour);

-- ── 3. Main coverage orders — one order per slot, all anchor products ──────

WITH per_item AS (
    SELECT
        s.week_offset, s.dow, s.hour, s.created_at,
        ap.product_id, ap.name, ap.price,
        GREATEST(1, ROUND(ap.base_qty * dw.weight * hw.weight * (0.8 + random() * 0.4)))::int AS qty
    FROM slots s
    JOIN dow_weight dw ON dw.dow = s.dow
    JOIN hour_weight hw ON hw.hour = s.hour
    CROSS JOIN anchor_products ap
),
per_order AS (
    SELECT
        created_at,
        jsonb_agg(jsonb_build_object(
            'Id', gen_random_uuid(),
            'Name', name,
            'Notes', NULL,
            'Quantity', qty,
            'ProductId', product_id,
            'UnitPrice', jsonb_build_object('Amount', price, 'Currency', 'EUR')
        )) AS items_json,
        round(sum(price * qty), 2) AS subtotal,
        round(sum(price * qty) * 0.10, 2) AS tax,
        round(sum(price * qty) * 1.10, 2) AS total
    FROM per_item
    GROUP BY week_offset, dow, hour, created_at
)
INSERT INTO orders (
    "Id", "RestaurantId", "TableId", "CustomerId", "WaiterId", "OrderNumber", "Source", "Status",
    subtotal_amount, subtotal_currency, tax_amount, tax_currency,
    discount_amount, discount_currency, tip_amount, tip_currency, total_amount, total_currency,
    "Notes", "EstimatedReadyAt", "TaxRate", "CancellationReason",
    "CreatedAt", "UpdatedAt", "TenantId", items
)
SELECT
    gen_random_uuid(),
    (SELECT restaurant_id FROM ctx),
    (SELECT id FROM table_list ORDER BY random() LIMIT 1),
    NULL, NULL,
    '#DEMO-' || substr(md5(o.created_at::text || random()::text), 1, 6),
    CASE WHEN random() < 0.75 THEN 'QR' WHEN random() < 0.9 THEN 'Manual' ELSE 'Reservation' END,
    'Paid',
    o.subtotal, 'EUR', o.tax, 'EUR', 0, 'EUR', 0, 'EUR', o.total, 'EUR',
    'demo-history-seed', NULL, 0.10, NULL,
    o.created_at,
    o.created_at + make_interval(mins => (20 + floor(random() * 25))::int),
    (SELECT tenant_id FROM ctx),
    o.items_json
FROM per_order o;

-- ── 4. Extra weekend peak-hour orders — more (not just bigger) tickets ─────
-- Fri/Sat/Sun lunch+dinner peaks get a second, smaller order at ~60% of
-- those slots, using a random subset of anchor products, so the busiest
-- slots show more distinct orders, not only higher quantities.

WITH picked AS (
    SELECT
        s.week_offset, s.dow, s.hour, s.created_at,
        x.product_id, x.name, x.price, x.qty
    FROM slots s
    CROSS JOIN LATERAL (
        SELECT ap.product_id, ap.name, ap.price,
               GREATEST(1, ROUND(ap.base_qty * dw.weight * hw.weight * (0.8 + random() * 0.4)))::int AS qty
        FROM anchor_products ap
        JOIN dow_weight dw ON dw.dow = s.dow
        JOIN hour_weight hw ON hw.hour = s.hour
        ORDER BY random()
        LIMIT (2 + floor(random() * 2))::int
    ) AS x
    WHERE s.dow IN (0, 5, 6) AND s.hour IN (13, 14, 15, 20, 21, 22)
      AND random() < 0.6
),
per_order AS (
    SELECT
        created_at,
        jsonb_agg(jsonb_build_object(
            'Id', gen_random_uuid(),
            'Name', name,
            'Notes', NULL,
            'Quantity', qty,
            'ProductId', product_id,
            'UnitPrice', jsonb_build_object('Amount', price, 'Currency', 'EUR')
        )) AS items_json,
        round(sum(price * qty), 2) AS subtotal,
        round(sum(price * qty) * 0.10, 2) AS tax,
        round(sum(price * qty) * 1.10, 2) AS total
    FROM picked
    GROUP BY week_offset, dow, hour, created_at
)
INSERT INTO orders (
    "Id", "RestaurantId", "TableId", "CustomerId", "WaiterId", "OrderNumber", "Source", "Status",
    subtotal_amount, subtotal_currency, tax_amount, tax_currency,
    discount_amount, discount_currency, tip_amount, tip_currency, total_amount, total_currency,
    "Notes", "EstimatedReadyAt", "TaxRate", "CancellationReason",
    "CreatedAt", "UpdatedAt", "TenantId", items
)
SELECT
    gen_random_uuid(),
    (SELECT restaurant_id FROM ctx),
    (SELECT id FROM table_list ORDER BY random() LIMIT 1),
    NULL, NULL,
    '#DEMO-' || substr(md5(o.created_at::text || random()::text), 1, 6),
    CASE WHEN random() < 0.8 THEN 'QR' ELSE 'Manual' END,
    'Paid',
    o.subtotal, 'EUR', o.tax, 'EUR', 0, 'EUR', 0, 'EUR', o.total, 'EUR',
    'demo-history-seed', NULL, 0.10, NULL,
    -- offset a few minutes from the main order at the same slot, staying
    -- inside the same clock hour so it lands in the same forecast bucket
    o.created_at + make_interval(mins => (5 + floor(random() * 10))::int),
    o.created_at + make_interval(mins => (25 + floor(random() * 30))::int),
    (SELECT tenant_id FROM ctx),
    o.items_json
FROM per_order o;

-- ── 5. Background orders — sparse, random single-item tickets ──────────────
-- Gives the rest of the menu some (mostly Low/Medium) history too, so the
-- forecast dashboard isn't suspiciously all-or-nothing.

WITH candidate_slots AS (
    SELECT s.week_offset, s.dow, s.hour, s.created_at
    FROM slots s
    WHERE random() < 0.20
),
picked AS (
    SELECT
        cs.week_offset, cs.dow, cs.hour, cs.created_at,
        bp.product_id, bp.name, bp.price,
        (1 + floor(random() * 2))::int AS qty
    FROM candidate_slots cs
    CROSS JOIN LATERAL (
        SELECT product_id, name, price FROM background_products ORDER BY random() LIMIT 1
    ) AS bp
)
INSERT INTO orders (
    "Id", "RestaurantId", "TableId", "CustomerId", "WaiterId", "OrderNumber", "Source", "Status",
    subtotal_amount, subtotal_currency, tax_amount, tax_currency,
    discount_amount, discount_currency, tip_amount, tip_currency, total_amount, total_currency,
    "Notes", "EstimatedReadyAt", "TaxRate", "CancellationReason",
    "CreatedAt", "UpdatedAt", "TenantId", items
)
SELECT
    gen_random_uuid(),
    (SELECT restaurant_id FROM ctx),
    (SELECT id FROM table_list ORDER BY random() LIMIT 1),
    NULL, NULL,
    '#DEMO-' || substr(md5(p.created_at::text || random()::text), 1, 6),
    CASE WHEN random() < 0.75 THEN 'QR' WHEN random() < 0.9 THEN 'Manual' ELSE 'Reservation' END,
    'Paid',
    round(p.price * p.qty, 2), 'EUR',
    round(p.price * p.qty * 0.10, 2), 'EUR',
    0, 'EUR', 0, 'EUR',
    round(p.price * p.qty * 1.10, 2), 'EUR',
    'demo-history-seed', NULL, 0.10, NULL,
    p.created_at,
    p.created_at + make_interval(mins => (15 + floor(random() * 20))::int),
    (SELECT tenant_id FROM ctx),
    jsonb_build_array(jsonb_build_object(
        'Id', gen_random_uuid(),
        'Name', p.name,
        'Notes', NULL,
        'Quantity', p.qty,
        'ProductId', p.product_id,
        'UnitPrice', jsonb_build_object('Amount', p.price, 'Currency', 'EUR')
    ))
FROM picked p;

-- ── 6. Status history — full Pending -> ... -> Paid trail per order ────────
-- Interpolated between CreatedAt (Pending) and UpdatedAt (Paid) so
-- PrepTimeRepository / kitchen-ETA analytics see plausible transition
-- timestamps too, not just a dangling Paid order with no history.

INSERT INTO order_status_history ("Id", "TenantId", "OrderId", "RestaurantId", "FromStatus", "ToStatus", "ChangedAt", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(), o."TenantId", o."Id", o."RestaurantId",
    tr.from_status, tr.to_status,
    o."CreatedAt" + (o."UpdatedAt" - o."CreatedAt") * tr.frac,
    o."CreatedAt" + (o."UpdatedAt" - o."CreatedAt") * tr.frac,
    o."CreatedAt" + (o."UpdatedAt" - o."CreatedAt") * tr.frac
FROM orders o
CROSS JOIN (VALUES
    (NULL::text, 'Pending',    0.00),
    ('Pending',   'Confirmed', 0.08),
    ('Confirmed', 'Preparing', 0.20),
    ('Preparing', 'Ready',     0.65),
    ('Ready',     'Served',    0.80),
    ('Served',    'Paid',      1.00)
) AS tr(from_status, to_status, frac)
WHERE o."TenantId" = (SELECT tenant_id FROM ctx) AND o."Notes" = 'demo-history-seed';

-- ── 7. Summary ───────────────────────────────────────────────────────────

DO $$
DECLARE
    v_orders int;
    v_history int;
BEGIN
    SELECT count(*) INTO v_orders FROM orders
        WHERE "TenantId" = (SELECT tenant_id FROM ctx) AND "Notes" = 'demo-history-seed';
    SELECT count(*) INTO v_history FROM order_status_history h
        JOIN orders o ON o."Id" = h."OrderId"
        WHERE o."TenantId" = (SELECT tenant_id FROM ctx) AND o."Notes" = 'demo-history-seed';
    RAISE NOTICE 'seed-demo-history: inserted % orders and % status-history rows.', v_orders, v_history;
END $$;

DROP TABLE ctx, table_list, anchor_products, background_products, dow_weight, hour_weight, slots;

COMMIT;
