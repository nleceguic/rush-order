namespace RushOrder.Application.Common.Interfaces;

public interface IPrepTimeRepository
{
    // Avg minutes between a "Preparing" and the following "Ready" transition
    // (OrderStatusHistory), attributed per product via the order's items,
    // over the last 30 days. Products with no history simply won't have a
    // key in the result — callers fall back to Product.PreparationMinutes.
    Task<IReadOnlyDictionary<Guid, decimal>> GetAveragePrepMinutesAsync(
        Guid tenantId, Guid restaurantId, IReadOnlyCollection<Guid> productIds,
        DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<int> GetOrdersInPreparationCountAsync(
        Guid tenantId, Guid restaurantId, CancellationToken cancellationToken = default);

    // Avg Preparing->Ready duration (minutes) of the most recent N completed
    // orders — powers the desktop AI Dashboard's "ETA medio de cocina" widget.
    Task<decimal?> GetRecentAveragePrepMinutesAsync(
        Guid tenantId, Guid restaurantId, int take = 10, CancellationToken cancellationToken = default);
}
