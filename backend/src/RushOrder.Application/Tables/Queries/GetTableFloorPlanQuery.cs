using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.DTOs;

namespace RushOrder.Application.Tables.Queries;

public record GetTableFloorPlanQuery(Guid RestaurantId) : IQuery<List<TableFloorPlanDto>>;

public sealed class GetTableFloorPlanQueryHandler : IRequestHandler<GetTableFloorPlanQuery, List<TableFloorPlanDto>>
{
    private readonly ITableRepository _tableRepository;
    private readonly IOrderRepository _orderRepository;

    public GetTableFloorPlanQueryHandler(ITableRepository tableRepository, IOrderRepository orderRepository)
    {
        _tableRepository = tableRepository;
        _orderRepository = orderRepository;
    }

    public async Task<List<TableFloorPlanDto>> Handle(GetTableFloorPlanQuery request, CancellationToken cancellationToken)
    {
        var tables = await _tableRepository.GetByRestaurantAsync(request.RestaurantId, cancellationToken);
        var activeOrders = await _orderRepository.GetActiveByRestaurantAsync(request.RestaurantId, cancellationToken);

        var ordersByTable = activeOrders
            .GroupBy(o => o.TableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return tables.Select(t =>
        {
            ordersByTable.TryGetValue(t.Id, out var orders);
            var activeCount = orders?.Count ?? 0;
            var currentOrderNumber = orders?.OrderByDescending(o => o.CreatedAt).FirstOrDefault()?.OrderNumber;
            return new TableFloorPlanDto(
                t.Id, t.Name, t.Capacity, t.Zone,
                t.Status.ToString(), t.PositionX, t.PositionY,
                activeCount, currentOrderNumber);
        }).ToList();
    }
}
