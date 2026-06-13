using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;

namespace RushOrder.Application.Orders.Queries;

// --- Query ---

public record GetKitchenOrdersQuery(Guid RestaurantId) : IQuery<List<KitchenOrderDto>>;

// --- Handler ---

public sealed class GetKitchenOrdersQueryHandler
    : IRequestHandler<GetKitchenOrdersQuery, List<KitchenOrderDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetKitchenOrdersQueryHandler(IOrderRepository orderRepository)
        => _orderRepository = orderRepository;

    public async Task<List<KitchenOrderDto>> Handle(
        GetKitchenOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetKitchenOrdersAsync(request.RestaurantId, cancellationToken);

        return orders
            .Select(o => new KitchenOrderDto(
                o.Id,
                o.OrderNumber,
                o.TableId,
                o.Status.ToString(),
                o.Items
                    .Select(i => new KitchenOrderItemDto(
                        i.ProductId, i.Name, i.Quantity, i.Notes, i.Modifiers))
                    .ToList()
                    .AsReadOnly(),
                o.Notes,
                o.CreatedAt))
            .ToList();
    }
}
