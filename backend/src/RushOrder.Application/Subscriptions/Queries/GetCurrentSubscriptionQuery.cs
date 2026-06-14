using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Subscriptions.Queries;

public sealed record GetCurrentSubscriptionQuery : IRequest<CurrentSubscriptionDto>;

public sealed class GetCurrentSubscriptionQueryHandler
    : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IPlanRepository _planRepo;

    public GetCurrentSubscriptionQueryHandler(
        ICurrentTenantService currentTenant,
        ISubscriptionRepository subscriptionRepo,
        IPlanRepository planRepo)
    {
        _currentTenant = currentTenant;
        _subscriptionRepo = subscriptionRepo;
        _planRepo = planRepo;
    }

    public async Task<CurrentSubscriptionDto> Handle(
        GetCurrentSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new BusinessRuleException("Tenant context is required.");

        var subscription = await _subscriptionRepo.GetByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Subscription", tenantId);

        var plan = await _planRepo.GetByIdAsync(subscription.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan", subscription.PlanId);

        var daysRemaining = Math.Max(0,
            (int)(subscription.CurrentPeriodEnd - DateTimeOffset.UtcNow).TotalDays);

        return new CurrentSubscriptionDto(
            SubscriptionId: subscription.Id,
            Plan: new PlanDto(
                plan.Id, plan.Name,
                plan.PriceMonthly.Amount, plan.PriceYearly.Amount, plan.PriceMonthly.Currency,
                plan.MaxTables, plan.MaxUsers, plan.MaxRestaurants,
                plan.Features, plan.IsActive),
            Status: subscription.Status.ToString(),
            CurrentPeriodStart: subscription.CurrentPeriodStart,
            CurrentPeriodEnd: subscription.CurrentPeriodEnd,
            CancelAtPeriodEnd: subscription.CancelAtPeriodEnd,
            DaysRemaining: daysRemaining);
    }
}
