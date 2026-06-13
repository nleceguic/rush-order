using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Orders.Queries;

// --- Query ---

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDetailDto?>;

// --- Handler ---

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IUserRepository _userRepository;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        ITableRepository tableRepository,
        IUserRepository userRepository)
    {
        _orderRepository = orderRepository;
        _tableRepository = tableRepository;
        _userRepository = userRepository;
    }

    public async Task<OrderDetailDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return null;

        var table = await _tableRepository.GetByIdAsync(order.TableId, cancellationToken);

        User? waiter = null;
        if (order.WaiterId.HasValue)
            waiter = await _userRepository.GetByIdAsync(order.WaiterId.Value, cancellationToken);

        return ToDetailDto(order, table, waiter);
    }

    internal static OrderDetailDto ToDetailDto(Order order, Table? table, User? waiter)
        => new(
            order.Id,
            order.OrderNumber,
            order.RestaurantId,
            order.TableId,
            table?.Name,
            order.WaiterId,
            waiter?.FullName,
            order.CustomerId,
            order.Status.ToString(),
            order.Source.ToString(),
            order.Subtotal.Amount,
            order.TaxAmount.Amount,
            order.DiscountAmount.Amount,
            order.TipAmount.Amount,
            order.Total.Amount,
            order.Total.Currency,
            order.Notes,
            order.EstimatedReadyAt,
            order.CancellationReason,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.Name,
                i.UnitPrice.Amount,
                i.UnitPrice.Currency,
                i.Quantity,
                i.Notes,
                i.Modifiers,
                i.LineTotal.Amount)).ToList().AsReadOnly());
}
