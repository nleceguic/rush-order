using MediatR;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Admin.Queries;

public sealed record GetTenantDetailQuery(Guid TenantId) : IRequest<TenantDetailDto>;

public sealed class GetTenantDetailQueryHandler
    : IRequestHandler<GetTenantDetailQuery, TenantDetailDto>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IPlanRepository _planRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IAdminRepository _adminRepo;

    public GetTenantDetailQueryHandler(
        ITenantRepository tenantRepo,
        IPlanRepository planRepo,
        ISubscriptionRepository subscriptionRepo,
        IAdminRepository adminRepo)
    {
        _tenantRepo = tenantRepo;
        _planRepo = planRepo;
        _subscriptionRepo = subscriptionRepo;
        _adminRepo = adminRepo;
    }

    public async Task<TenantDetailDto> Handle(
        GetTenantDetailQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        var plan = await _planRepo.GetByIdAsync(tenant.PlanId, cancellationToken);
        var subscription = await _subscriptionRepo.GetByTenantIdAsync(request.TenantId, cancellationToken);
        var detail = await _adminRepo.GetTenantDetailMetricsAsync(request.TenantId, cancellationToken);

        CurrentSubscriptionDto? subDto = null;
        if (subscription is not null && plan is not null)
        {
            var daysRemaining = Math.Max(0,
                (int)(subscription.CurrentPeriodEnd - DateTimeOffset.UtcNow).TotalDays);
            subDto = new CurrentSubscriptionDto(
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

        var billing = tenant.BillingInfo;
        return new TenantDetailDto(
            Id: tenant.Id,
            Name: tenant.Name,
            Slug: tenant.Slug,
            Status: tenant.Status.ToString(),
            PlanName: plan?.Name ?? "Unknown",
            BillingInfo: new TenantBillingInfoDto(
                billing.CompanyName, billing.TaxId,
                billing.InvoiceEmail, billing.Country),
            Subscription: subDto,
            RestaurantsCount: detail.RestaurantsCount,
            UsersCount: detail.UsersCount,
            TablesCount: detail.TablesCount,
            CreatedAt: tenant.CreatedAt,
            TrialEndsAt: tenant.TrialEndsAt,
            StripeCustomerId: tenant.StripeCustomerId);
    }
}
