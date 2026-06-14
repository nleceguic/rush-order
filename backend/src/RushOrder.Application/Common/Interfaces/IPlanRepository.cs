using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Plan?> GetStarterPlanAsync(CancellationToken ct = default);
    Task AddAsync(Plan plan, CancellationToken ct = default);
}
