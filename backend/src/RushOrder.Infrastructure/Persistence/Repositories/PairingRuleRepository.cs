using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class PairingRuleRepository : Repository<ProductPairingRule>, IPairingRuleRepository
{
    public PairingRuleRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ProductPairingRule>> GetByRestaurantAsync(
        Guid restaurantId, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId)
            .ToListAsync(cancellationToken);
}
