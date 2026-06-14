using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Subscriptions.Queries;

public sealed record GetBillingHistoryQuery : IRequest<IReadOnlyList<BillingInvoiceDto>>;

public sealed class GetBillingHistoryQueryHandler
    : IRequestHandler<GetBillingHistoryQuery, IReadOnlyList<BillingInvoiceDto>>
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly ISubscriptionService _subscriptionService;

    public GetBillingHistoryQueryHandler(
        ICurrentTenantService currentTenant,
        ISubscriptionService subscriptionService)
    {
        _currentTenant = currentTenant;
        _subscriptionService = subscriptionService;
    }

    public async Task<IReadOnlyList<BillingInvoiceDto>> Handle(
        GetBillingHistoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new BusinessRuleException("Tenant context is required.");

        return await _subscriptionService.GetBillingHistoryAsync(tenantId, cancellationToken);
    }
}
