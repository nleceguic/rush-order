using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class TableRepository : Repository<Table>, ITableRepository
{
    public TableRepository(AppDbContext context) : base(context) { }

    public async Task<Table?> GetByQrCodeAsync(string qrCode, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.QrCode == qrCode, cancellationToken);

    public async Task<IReadOnlyList<Table>> GetByRestaurantAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Table>> GetByStatusAsync(
        Guid restaurantId,
        TableStatus status,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId && t.Status == status)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Table>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await DbSet
            .AsNoTracking()
            .Where(t => idList.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }
}
