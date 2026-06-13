using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.DTOs;

namespace RushOrder.Application.Tables.Queries;

public record GetTablesQuery(Guid RestaurantId) : IQuery<List<TableDto>>;

public sealed class GetTablesQueryHandler : IRequestHandler<GetTablesQuery, List<TableDto>>
{
    private readonly ITableRepository _tableRepository;

    public GetTablesQueryHandler(ITableRepository tableRepository)
        => _tableRepository = tableRepository;

    public async Task<List<TableDto>> Handle(GetTablesQuery request, CancellationToken cancellationToken)
    {
        var tables = await _tableRepository.GetByRestaurantAsync(request.RestaurantId, cancellationToken);
        return tables
            .Select(t => new TableDto(
                t.Id, t.RestaurantId, t.Name, t.Capacity, t.Zone,
                t.QrCode, t.QrUrl, t.Status.ToString(),
                t.PositionX, t.PositionY, t.CreatedAt))
            .ToList();
    }
}
