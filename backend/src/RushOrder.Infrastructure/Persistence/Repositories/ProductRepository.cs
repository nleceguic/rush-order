using Microsoft.EntityFrameworkCore;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : Repository<Product>
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId,
        bool onlyAvailable = true,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId && (!onlyAvailable || p.IsAvailable))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByRestaurantAsync(
        Guid restaurantId,
        bool onlyAvailable = false,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId && (!onlyAvailable || p.IsAvailable))
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetLowStockAsync(
        Guid restaurantId,
        int threshold = 5,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId
                && p.StockTracking
                && p.StockQuantity <= threshold)
            .ToListAsync(cancellationToken);
}
