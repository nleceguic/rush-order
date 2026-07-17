using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IPairingRuleRepository : IRepository<ProductPairingRule>
{
    Task<IReadOnlyList<ProductPairingRule>> GetByRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
