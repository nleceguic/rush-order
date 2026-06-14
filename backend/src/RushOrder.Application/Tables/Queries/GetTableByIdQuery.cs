using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.DTOs;

namespace RushOrder.Application.Tables.Queries;

public record GetTableByIdQuery(Guid TableId) : IQuery<TableDto?>;

public sealed class GetTableByIdQueryHandler : IRequestHandler<GetTableByIdQuery, TableDto?>
{
    private readonly ITableRepository _tableRepository;

    public GetTableByIdQueryHandler(ITableRepository tableRepository)
        => _tableRepository = tableRepository;

    public async Task<TableDto?> Handle(GetTableByIdQuery request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken);
        if (table is null) return null;

        return new TableDto(
            table.Id,
            table.RestaurantId,
            table.Name,
            table.Capacity,
            table.Zone,
            table.QrCode,
            table.QrUrl,
            table.Status.ToString(),
            table.PositionX,
            table.PositionY,
            table.CreatedAt);
    }
}
