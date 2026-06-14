using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class AdminRepository : IAdminRepository
{
    private readonly string _connectionString;

    public AdminRepository(AppDbContext context)
        => _connectionString = context.Database.GetConnectionString()!;

    public async Task<IReadOnlyList<TenantMetricsRow>> GetTenantMetricsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                t."Id"          AS "TenantId",
                COUNT(DISTINCT r."Id") AS "RestaurantsCount",
                COUNT(DISTINCT u."Id") AS "UsersCount"
            FROM tenants t
            LEFT JOIN restaurants r ON r."TenantId" = t."Id"
            LEFT JOIN users u       ON u."TenantId" = t."Id" AND u."IsActive" = TRUE
            GROUP BY t."Id"
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<TenantMetricsRow>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TenantDetailMetricsRow> GetTenantDetailMetricsAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COUNT(DISTINCT r."Id")    AS "RestaurantsCount",
                COUNT(DISTINCT u."Id")    AS "UsersCount",
                COUNT(DISTINCT tb."Id")   AS "TablesCount"
            FROM tenants t
            LEFT JOIN restaurants r ON r."TenantId" = t."Id"
            LEFT JOIN users u       ON u."TenantId" = t."Id" AND u."IsActive" = TRUE
            LEFT JOIN tables tb     ON tb."TenantId" = t."Id"
            WHERE t."Id" = @tenantId
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleAsync<TenantDetailMetricsRow>(
            new CommandDefinition(sql, new { tenantId }, cancellationToken: ct));
    }

    public async Task<GlobalMetricsDto> GetGlobalMetricsAsync(CancellationToken ct = default)
    {
        const string sql = """
            WITH counts AS (
                SELECT
                    COUNT(*)                                               AS total,
                    COUNT(*) FILTER (WHERE t."Status" = 'Active')        AS active,
                    COUNT(*) FILTER (WHERE t."Status" = 'Trial')         AS trial,
                    COUNT(*) FILTER (WHERE t."Status" = 'Suspended')     AS suspended,
                    COUNT(*) FILTER (WHERE t."CreatedAt" >= NOW() - INTERVAL '7 days') AS new_week
                FROM tenants t
            ),
            mrr_calc AS (
                SELECT COALESCE(SUM(p.price_monthly_amount), 0) AS mrr
                FROM subscriptions s
                JOIN plans p ON p."Id" = s."PlanId"
                WHERE s."Status" = 'Active'
            )
            SELECT
                c.total             AS "TotalTenants",
                c.active            AS "ActiveTenants",
                c.trial             AS "TrialTenants",
                c.suspended         AS "SuspendedTenants",
                m.mrr               AS "Mrr",
                m.mrr * 12          AS "Arr",
                c.new_week          AS "NewTenantsThisWeek",
                CASE WHEN c.active > 0
                    THEN ROUND((c.suspended::numeric / NULLIF(c.active + c.suspended, 0)) * 100, 2)
                    ELSE 0
                END                 AS "ChurnRatePercent"
            FROM counts c, mrr_calc m
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleAsync<GlobalMetricsDto>(
            new CommandDefinition(sql, cancellationToken: ct));
    }
}
