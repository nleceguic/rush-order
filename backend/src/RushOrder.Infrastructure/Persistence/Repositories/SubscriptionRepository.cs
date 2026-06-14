using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _context;

    public SubscriptionRepository(AppDbContext context) => _context = context;

    public async Task<Subscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public async Task<Subscription?> GetByStripeSubscriptionIdAsync(
        string stripeSubscriptionId, CancellationToken ct = default)
        => await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);

    public async Task AddAsync(Subscription subscription, CancellationToken ct = default)
        => await _context.Subscriptions.AddAsync(subscription, ct);
}
