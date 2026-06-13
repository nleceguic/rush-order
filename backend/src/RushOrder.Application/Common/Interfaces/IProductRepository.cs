using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<bool> AnyByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByRestaurantAsync(Guid restaurantId, bool onlyAvailable = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, bool onlyAvailable = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetPagedByRestaurantAsync(Guid restaurantId, Guid? categoryId, bool? onlyAvailable, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountByRestaurantAsync(Guid restaurantId, Guid? categoryId, bool? onlyAvailable, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> SearchAsync(Guid restaurantId, string searchTerm, CancellationToken cancellationToken = default);
}
