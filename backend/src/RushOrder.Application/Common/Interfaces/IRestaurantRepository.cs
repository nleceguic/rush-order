using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IRestaurantRepository : IRepository<Restaurant>
{
    // Bypasses the tenant query filter — for anonymous/public endpoints (e.g. QR menu)
    // where no tenant context is resolved yet.
    Task<Restaurant?> GetByIdPublicAsync(Guid id, CancellationToken cancellationToken = default);
}
