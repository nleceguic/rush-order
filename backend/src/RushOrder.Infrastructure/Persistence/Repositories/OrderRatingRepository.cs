using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class OrderRatingRepository : Repository<OrderRating>, IOrderRatingRepository
{
    public OrderRatingRepository(AppDbContext context) : base(context) { }

    public async Task<OrderRating?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await DbSet.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
}
