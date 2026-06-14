namespace RushOrder.Application.Common.Interfaces;

public interface IAuthCacheService
{
    Task BlockJtiAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> IsJtiBlockedAsync(string jti, CancellationToken cancellationToken = default);

    Task<string> StoreMfaPendingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetMfaPendingUserIdAsync(string tempToken, CancellationToken cancellationToken = default);
    Task RemoveMfaPendingAsync(string tempToken, CancellationToken cancellationToken = default);

    Task<string> StorePasswordResetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetPasswordResetUserIdAsync(string rawToken, CancellationToken cancellationToken = default);
    Task RemovePasswordResetAsync(string rawToken, CancellationToken cancellationToken = default);
}
