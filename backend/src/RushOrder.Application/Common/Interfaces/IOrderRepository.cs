using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Common.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<int> GetNextSequenceNumberAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetPagedByRestaurantAsync(
        Guid restaurantId,
        OrderStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountByRestaurantAsync(
        Guid restaurantId,
        OrderStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetActiveByTableAsync(Guid tableId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetKitchenOrdersAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
