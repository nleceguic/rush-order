using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByDeviceFingerprintAsync(string deviceFingerprint, CancellationToken cancellationToken = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Looks up a customer across tenants, for flows without a tenant claim (payments, Stripe webhook).</summary>
    Task<Customer?> GetByIdPublicAsync(Guid id, CancellationToken cancellationToken = default);
}
