using RushOrder.Application.Analytics.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public interface IAnalyticsRepository
{
    Task<DashboardDto> GetDashboardAsync(Guid restaurantId, DateOnly date, CancellationToken ct = default);

    Task<SalesDto> GetSalesAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string groupBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProductPerformanceDto>> GetProductPerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    Task<IReadOnlyList<TablePerformanceDto>> GetTablePerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    Task<IReadOnlyList<WaiterPerformanceDto>> GetWaiterPerformanceAsync(
        Guid restaurantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
