using Microsoft.EntityFrameworkCore;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public async Task<Customer?> GetByDeviceFingerprintAsync(
        string deviceFingerprint,
        CancellationToken cancellationToken = default)
        => await DbSet
            .FirstOrDefaultAsync(c => c.DeviceFingerprint == deviceFingerprint, cancellationToken);

    public async Task<Customer?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => await DbSet
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.Value == email, cancellationToken);
}
