using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IPromotionRepository : IRepository<Promotion>
{
    // Anonymous/public read (PWA landing page) — must ignore the tenant query filter,
    // same reasoning as ICategoryRepository.GetByRestaurantPublicAsync.
    Task<IReadOnlyList<Promotion>> GetActiveByRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
