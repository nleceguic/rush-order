using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<IReadOnlyList<Category>> GetByRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
