using Microsoft.EntityFrameworkCore;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : Repository<Order>
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetActiveByRestaurantAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId
                && o.Status != OrderStatus.Paid
                && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetByTableAsync(
        Guid tableId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(o => o.TableId == tableId && o.CreatedAt >= from && o.CreatedAt <= to)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextSequenceNumberAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var count = await DbSet
            .IgnoreQueryFilters()
            .CountAsync(o => o.RestaurantId == restaurantId && o.CreatedAt >= yearStart, cancellationToken);
        return count + 1;
    }
}
