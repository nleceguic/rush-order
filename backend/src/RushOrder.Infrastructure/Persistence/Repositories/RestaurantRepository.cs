using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class RestaurantRepository : Repository<Restaurant>, IRestaurantRepository
{
    public RestaurantRepository(AppDbContext context) : base(context) { }

    public async Task<Restaurant?> GetByIdPublicAsync(Guid id, CancellationToken cancellationToken = default)
        => await DbSet.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
}
