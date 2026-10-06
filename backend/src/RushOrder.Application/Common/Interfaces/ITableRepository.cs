using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface ITableRepository : IRepository<Table>
{
    /// <summary>Looks up a table across tenants, for anonymous QR ordering.</summary>
    Task<Table?> GetByIdPublicAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Table?> GetByQrCodeAsync(string qrCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Table>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Table>> GetByRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
