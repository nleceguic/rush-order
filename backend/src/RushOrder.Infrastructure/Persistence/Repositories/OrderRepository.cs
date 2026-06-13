using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : Repository<Order>, IOrderRepository
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
        var yearStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var count = await DbSet
            .IgnoreQueryFilters()
            .CountAsync(o => o.RestaurantId == restaurantId && o.CreatedAt >= yearStart, cancellationToken);
        return count + 1;
    }

    public async Task<IReadOnlyList<Order>> GetPagedByRestaurantAsync(
        Guid restaurantId,
        OrderStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId);

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (dateFrom.HasValue) query = query.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(o => o.CreatedAt <= dateTo.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByRestaurantAsync(
        Guid restaurantId,
        OrderStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId);

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (dateFrom.HasValue) query = query.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(o => o.CreatedAt <= dateTo.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetActiveByTableAsync(
        Guid tableId,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(o => o.TableId == tableId
                && o.Status != OrderStatus.Paid
                && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetKitchenOrdersAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(o => o.RestaurantId == restaurantId
                && (o.Status == OrderStatus.Pending
                    || o.Status == OrderStatus.Confirmed
                    || o.Status == OrderStatus.Preparing
                    || o.Status == OrderStatus.Ready))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
}
