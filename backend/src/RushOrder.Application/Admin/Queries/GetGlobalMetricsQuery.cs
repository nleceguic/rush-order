using MediatR;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Queries;

public sealed record GetGlobalMetricsQuery : IRequest<GlobalMetricsDto>;

public sealed class GetGlobalMetricsQueryHandler
    : IRequestHandler<GetGlobalMetricsQuery, GlobalMetricsDto>
{
    private readonly IAdminRepository _adminRepo;

    public GetGlobalMetricsQueryHandler(IAdminRepository adminRepo)
        => _adminRepo = adminRepo;

    public async Task<GlobalMetricsDto> Handle(
        GetGlobalMetricsQuery request, CancellationToken cancellationToken)
        => await _adminRepo.GetGlobalMetricsAsync(cancellationToken);
}
