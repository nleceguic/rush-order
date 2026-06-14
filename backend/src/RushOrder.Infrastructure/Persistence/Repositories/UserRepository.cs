using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await DbSet.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.Value == email && u.IsActive, cancellationToken);

    public async Task<bool> ExistsAnyWithEmailAsync(string email, CancellationToken cancellationToken = default)
        => await DbSet.IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == email, cancellationToken);
}
