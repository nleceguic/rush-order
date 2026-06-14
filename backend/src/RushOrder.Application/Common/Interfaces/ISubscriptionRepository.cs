using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<Subscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct = default);
    Task AddAsync(Subscription subscription, CancellationToken ct = default);
}
