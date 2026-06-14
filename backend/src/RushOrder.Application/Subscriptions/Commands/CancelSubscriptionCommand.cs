using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Subscriptions.Commands;

public sealed record CancelSubscriptionCommand(bool Immediately) : ICommand<Unit>;

public sealed class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly ISubscriptionService _subscriptionService;

    public CancelSubscriptionCommandHandler(
        ICurrentTenantService currentTenant,
        ISubscriptionService subscriptionService)
    {
        _currentTenant = currentTenant;
        _subscriptionService = subscriptionService;
    }

    public async Task<Unit> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new BusinessRuleException("Tenant context is required.");

        await _subscriptionService.CancelSubscriptionAsync(tenantId, request.Immediately, cancellationToken);
        return Unit.Value;
    }
}
