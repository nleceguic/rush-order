using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetAllByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
