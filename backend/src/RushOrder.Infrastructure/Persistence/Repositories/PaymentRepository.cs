using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext context) : base(context) { }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    // Ignores the tenant filter: provider IDs are globally unique, and the callers
    // (Stripe webhook, anonymous payment confirmation) carry no tenant claim.
    public async Task<Payment?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken = default)
        => await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.ProviderPaymentId == providerPaymentId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetAllByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
}
