using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsAnyWithEmailAsync(string email, CancellationToken cancellationToken = default);
}
