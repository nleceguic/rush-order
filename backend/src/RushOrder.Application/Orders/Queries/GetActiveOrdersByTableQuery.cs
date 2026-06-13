using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Application.Orders.Queries;

namespace RushOrder.Application.Orders.Queries;

// --- Query ---

public record GetActiveOrdersByTableQuery(Guid TableId) : IQuery<List<OrderDetailDto>>;

// --- Handler ---

public sealed class GetActiveOrdersByTableQueryHandler
    : IRequestHandler<GetActiveOrdersByTableQuery, List<OrderDetailDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITableRepository _tableRepository;

    public GetActiveOrdersByTableQueryHandler(
        IOrderRepository orderRepository,
        ITableRepository tableRepository)
    {
        _orderRepository = orderRepository;
        _tableRepository = tableRepository;
    }

    public async Task<List<OrderDetailDto>> Handle(
        GetActiveOrdersByTableQuery request,
        CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetActiveByTableAsync(request.TableId, cancellationToken);
        if (orders.Count == 0) return [];

        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken);

        return orders
            .Select(o => GetOrderByIdQueryHandler.ToDetailDto(o, table, null))
            .ToList();
    }
}
