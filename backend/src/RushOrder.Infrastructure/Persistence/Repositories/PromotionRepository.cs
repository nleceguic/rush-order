using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class PromotionRepository : Repository<Promotion>, IPromotionRepository
{
    public PromotionRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Promotion>> GetActiveByRestaurantAsync(
        Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await DbSet.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId
                && p.IsActive
                && p.StartDate <= now
                && p.EndDate >= now)
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);
    }
}
