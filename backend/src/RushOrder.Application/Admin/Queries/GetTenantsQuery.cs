using MediatR;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Queries;

public sealed record GetTenantsQuery : IRequest<IReadOnlyList<TenantSummaryDto>>;

public sealed class GetTenantsQueryHandler
    : IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantSummaryDto>>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IAdminRepository _adminRepo;

    public GetTenantsQueryHandler(
        ITenantRepository tenantRepo,
        IPlanRepository planRepo,
        IAdminRepository adminRepo)
    {
        _tenantRepo = tenantRepo;
        _planRepo = planRepo;
        _adminRepo = adminRepo;
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> Handle(
        GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _tenantRepo.GetAllAsync(cancellationToken);
        var plans = await _planRepo.GetAllActiveAsync(cancellationToken);
        var metrics = await _adminRepo.GetTenantMetricsAsync(cancellationToken);

        var planMap = plans.ToDictionary(p => p.Id, p => p.Name);
        var metricsMap = metrics.ToDictionary(m => m.TenantId);

        return tenants.Select(t =>
        {
            var m = metricsMap.GetValueOrDefault(t.Id);
            return new TenantSummaryDto(
                Id: t.Id,
                Name: t.Name,
                Slug: t.Slug,
                Status: t.Status.ToString(),
                PlanName: planMap.GetValueOrDefault(t.PlanId, "Unknown"),
                RestaurantsCount: m?.RestaurantsCount ?? 0,
                UsersCount: m?.UsersCount ?? 0,
                CreatedAt: t.CreatedAt,
                TrialEndsAt: t.TrialEndsAt);
        }).ToList();
    }
}
