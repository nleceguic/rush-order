using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Forecasting.DTOs;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Forecasting.Queries;

public record GetDemandForecastQuery(
    Guid RestaurantId, DateOnly Date, Guid? ProductId) : IQuery<DemandForecastResultDto>;

public sealed class GetDemandForecastQueryHandler : IRequestHandler<GetDemandForecastQuery, DemandForecastResultDto>
{
    private readonly IDemandForecastRepository _repository;
    private readonly ICurrentTenantService _tenant;

    public GetDemandForecastQueryHandler(IDemandForecastRepository repository, ICurrentTenantService tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    public async Task<DemandForecastResultDto> Handle(GetDemandForecastQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var rows = await _repository.GetForecastRowsAsync(
            tenantId, request.RestaurantId, request.Date, request.ProductId, cancellationToken);

        // Hourly: sum across products per hour.
        var hourly = rows
            .GroupBy(r => r.Hour)
            .Select(g => new HourlyForecastDto(
                g.Key,
                PredictedOrders: g.Sum(r => r.PredictedQuantity),
                PredictedRevenue: g.Sum(r => r.PredictedQuantity * r.Price)))
            .OrderBy(h => h.Hour)
            .ToList()
            .AsReadOnly();

        var peakHour = hourly.Count > 0
            ? hourly.OrderByDescending(h => h.PredictedOrders).First().Hour
            : (int?)null;

        // Products: sum across hours per product, worst confidence of the day.
        var products = rows
            .GroupBy(r => new { r.ProductId, r.Name })
            .Select(g =>
            {
                var qty = g.Sum(r => r.PredictedQuantity);
                var worstConfidence = g
                    .Select(r => Enum.Parse<ForecastConfidence>(r.ConfidenceLevel))
                    .Max(c => c switch { ForecastConfidence.Low => 2, ForecastConfidence.Medium => 1, _ => 0 });
                var confidenceLabel = worstConfidence switch
                {
                    2 => ForecastConfidence.Low,
                    1 => ForecastConfidence.Medium,
                    _ => ForecastConfidence.High,
                };

                return new ProductForecastDto(
                    g.Key.ProductId,
                    g.Key.Name,
                    PredictedQuantity: qty,
                    RecommendedPrepQuantity: Math.Ceiling(qty * 1.1m), // small safety margin over the raw prediction
                    Confidence: confidenceLabel.ToString());
            })
            .OrderByDescending(p => p.PredictedQuantity)
            .ToList()
            .AsReadOnly();

        // Predicted "covers" approximates to total predicted units sold that
        // day — this app doesn't forecast party size, so it's a proxy, not a
        // literal guest count.
        var totalCovers = products.Sum(p => p.PredictedQuantity);
        var topProducts = products
            .Take(5)
            .Select(p => new TopForecastProductDto(p.ProductId, p.Name, p.PredictedQuantity))
            .ToList()
            .AsReadOnly();

        var summary = new DemandForecastSummaryDto(totalCovers, peakHour, topProducts);

        return new DemandForecastResultDto(summary, hourly, products);
    }
}
