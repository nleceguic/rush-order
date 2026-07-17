using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Experiments.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class ExperimentRepository : IExperimentRepository
{
    private readonly AppDbContext _context;
    private readonly string _connectionString;

    public ExperimentRepository(AppDbContext context)
    {
        _context = context;
        _connectionString = context.Database.GetConnectionString()!;
    }

    // IgnoreQueryFilters: called from the fully anonymous assignment endpoint,
    // where ICurrentTenantService.TenantId is null (no JWT yet) — the tenantId
    // here has already been resolved from the public restaurantId, so it's the
    // WHERE clause below (not the EF tenant filter) that scopes the read.
    public async Task<Experiment?> GetActiveByKeyAsync(
        Guid tenantId, Guid restaurantId, string key, CancellationToken cancellationToken = default)
        => await _context.Experiments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId
                     && e.RestaurantId == restaurantId
                     && e.Key == key.Trim().ToLower()
                     && e.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task RecordEventAsync(ExperimentResult result, CancellationToken cancellationToken = default)
    {
        await _context.ExperimentResults.AddAsync(result, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExperimentResultsDto> GetResultsAsync(
        Guid tenantId, Guid restaurantId, string key, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                "Variant"                                                              AS Variant,
                COUNT(*) FILTER (WHERE "EventType" = 'Exposure')                       AS Exposures,
                COUNT(DISTINCT "DeviceFingerprint")
                    FILTER (WHERE "EventType" = 'SuggestionAdded')                     AS SuggestionAdds,
                COUNT(*) FILTER (WHERE "EventType" = 'OrderCompleted')                 AS OrdersCompleted,
                COALESCE(AVG("CartTotal") FILTER (WHERE "EventType" = 'OrderCompleted'), 0) AS AvgCartTotal
            FROM experiment_results
            WHERE "TenantId"      = @tenantId
              AND "RestaurantId"  = @restaurantId
              AND "ExperimentKey" = @key
            GROUP BY "Variant"
            ORDER BY "Variant"
            """;

        var rows = await conn.QueryAsync<ExperimentVariantStats>(
            new CommandDefinition(sql,
                new { tenantId, restaurantId, key = key.Trim().ToLowerInvariant() },
                cancellationToken: cancellationToken));

        return new ExperimentResultsDto(key, rows.ToList().AsReadOnly());
    }
}
