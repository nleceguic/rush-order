using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : Repository<Product>, IProductRepository
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
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByRestaurantPublicAsync(
        Guid restaurantId,
        bool onlyAvailable,
        CancellationToken cancellationToken = default)
        => await DbSet.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId && (!onlyAvailable || p.IsAvailable))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
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

    public async Task<bool> AnyByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
        => await DbSet.AnyAsync(p => p.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetPagedByRestaurantAsync(
        Guid restaurantId,
        Guid? categoryId,
        bool? onlyAvailable,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(p => p.RestaurantId == restaurantId);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (onlyAvailable.HasValue) query = query.Where(p => p.IsAvailable == onlyAvailable.Value);
        return await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByRestaurantAsync(
        Guid restaurantId,
        Guid? categoryId,
        bool? onlyAvailable,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(p => p.RestaurantId == restaurantId);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (onlyAvailable.HasValue) query = query.Where(p => p.IsAvailable == onlyAvailable.Value);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(
        Guid restaurantId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var lower = searchTerm.ToLowerInvariant();
        return await DbSet
            .AsNoTracking()
            .Where(p => p.RestaurantId == restaurantId
                && (p.Name.ToLower().Contains(lower)
                    || (p.Description != null && p.Description.ToLower().Contains(lower))))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
