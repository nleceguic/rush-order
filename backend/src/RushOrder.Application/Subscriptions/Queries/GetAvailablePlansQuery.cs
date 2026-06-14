using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Subscriptions.Queries;

public sealed record GetAvailablePlansQuery : IRequest<IReadOnlyList<PlanDto>>;

public sealed class GetAvailablePlansQueryHandler
    : IRequestHandler<GetAvailablePlansQuery, IReadOnlyList<PlanDto>>
{
    private readonly IPlanRepository _planRepo;

    public GetAvailablePlansQueryHandler(IPlanRepository planRepo)
        => _planRepo = planRepo;

    public async Task<IReadOnlyList<PlanDto>> Handle(
        GetAvailablePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _planRepo.GetAllActiveAsync(cancellationToken);

        return plans.Select(p => new PlanDto(
            p.Id, p.Name,
            p.PriceMonthly.Amount, p.PriceYearly.Amount, p.PriceMonthly.Currency,
            p.MaxTables, p.MaxUsers, p.MaxRestaurants,
            p.Features, p.IsActive))
            .ToList();
    }
}
