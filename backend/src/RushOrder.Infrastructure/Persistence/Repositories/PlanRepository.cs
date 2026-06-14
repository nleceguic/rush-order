using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _context;

    public PlanRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Plan>> GetAllActiveAsync(CancellationToken ct = default)
        => await _context.Plans.Where(p => p.IsActive).AsNoTracking().ToListAsync(ct);

    public async Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Plans.FindAsync([id], ct);

    public async Task<Plan?> GetStarterPlanAsync(CancellationToken ct = default)
        => await _context.Plans
            .Where(p => p.IsActive && p.Name == "Starter")
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Plan plan, CancellationToken ct = default)
        => await _context.Plans.AddAsync(plan, ct);
}
