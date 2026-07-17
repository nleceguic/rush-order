using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IOrderRatingRepository : IRepository<OrderRating>
{
    Task<OrderRating?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
