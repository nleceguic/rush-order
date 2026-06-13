using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface ITableRepository : IRepository<Table>
{
    Task<Table?> GetByQrCodeAsync(string qrCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Table>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
