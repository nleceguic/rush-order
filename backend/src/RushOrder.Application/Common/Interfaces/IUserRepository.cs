using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsAnyWithEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a user across all tenants. Only for anonymous flows that identify the
    /// user by a token (refresh, MFA verify, password reset), where no tenant is resolved.
    /// </summary>
    Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken cancellationToken = default);
}
