using MediatR;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Forecasting.Queries;

public sealed record KitchenEtaDto(decimal? AverageMinutes, int SampleSize);

public record GetKitchenEtaQuery(Guid RestaurantId) : IQuery<KitchenEtaDto>;

public sealed class GetKitchenEtaQueryHandler : IRequestHandler<GetKitchenEtaQuery, KitchenEtaDto>
{
    private const int SampleSize = 10;

    private readonly IPrepTimeRepository _repository;
    private readonly ICurrentTenantService _tenant;

    public GetKitchenEtaQueryHandler(IPrepTimeRepository repository, ICurrentTenantService tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    public async Task<KitchenEtaDto> Handle(GetKitchenEtaQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var avg = await _repository.GetRecentAveragePrepMinutesAsync(
            tenantId, request.RestaurantId, SampleSize, cancellationToken);
        return new KitchenEtaDto(avg, SampleSize);
    }
}
