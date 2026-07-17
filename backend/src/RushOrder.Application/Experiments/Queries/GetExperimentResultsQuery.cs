using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Experiments.DTOs;

namespace RushOrder.Application.Experiments.Queries;

public record GetExperimentResultsQuery(Guid RestaurantId, string ExperimentKey) : IQuery<ExperimentResultsDto>;

public sealed class GetExperimentResultsQueryHandler : IRequestHandler<GetExperimentResultsQuery, ExperimentResultsDto>
{
    private readonly IExperimentRepository _experiments;
    private readonly ICurrentTenantService _tenant;

    public GetExperimentResultsQueryHandler(IExperimentRepository experiments, ICurrentTenantService tenant)
    {
        _experiments = experiments;
        _tenant = tenant;
    }

    public async Task<ExperimentResultsDto> Handle(GetExperimentResultsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        return await _experiments.GetResultsAsync(tenantId, request.RestaurantId, request.ExperimentKey, cancellationToken);
    }
}
