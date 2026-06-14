using RushOrder.Application.Admin.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public record TenantMetricsRow(Guid TenantId, int RestaurantsCount, int UsersCount);
public record TenantDetailMetricsRow(int RestaurantsCount, int UsersCount, int TablesCount);

public interface IAdminRepository
{
    Task<IReadOnlyList<TenantMetricsRow>> GetTenantMetricsAsync(CancellationToken ct = default);
    Task<TenantDetailMetricsRow> GetTenantDetailMetricsAsync(Guid tenantId, CancellationToken ct = default);
    Task<GlobalMetricsDto> GetGlobalMetricsAsync(CancellationToken ct = default);
}
