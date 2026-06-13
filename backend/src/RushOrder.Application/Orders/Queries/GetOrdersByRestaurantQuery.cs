using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Common.Models;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Orders.Queries;

// --- Query ---

public record GetOrdersByRestaurantQuery(
    Guid RestaurantId,
    OrderStatus? Status,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber,
    int PageSize) : IQuery<PagedResult<OrderSummaryDto>>;

// --- Handler ---

public sealed class GetOrdersByRestaurantQueryHandler
    : IRequestHandler<GetOrdersByRestaurantQuery, PagedResult<OrderSummaryDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITableRepository _tableRepository;

    public GetOrdersByRestaurantQueryHandler(
        IOrderRepository orderRepository,
        ITableRepository tableRepository)
    {
        _orderRepository = orderRepository;
        _tableRepository = tableRepository;
    }

    public async Task<PagedResult<OrderSummaryDto>> Handle(
        GetOrdersByRestaurantQuery request,
        CancellationToken cancellationToken)
    {
        var totalCount = await _orderRepository.CountByRestaurantAsync(
            request.RestaurantId, request.Status, request.DateFrom, request.DateTo, cancellationToken);

        var orders = await _orderRepository.GetPagedByRestaurantAsync(
            request.RestaurantId, request.Status, request.DateFrom, request.DateTo,
            request.PageNumber, request.PageSize, cancellationToken);

        var tableIds = orders.Select(o => o.TableId).Distinct();
        var tables = await _tableRepository.GetByIdsAsync(tableIds, cancellationToken);
        var tableNames = tables.ToDictionary(t => t.Id, t => t.Name);

        var items = orders
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.TableId,
                tableNames.GetValueOrDefault(o.TableId),
                o.Status.ToString(),
                o.Source.ToString(),
                o.Total.Amount,
                o.Total.Currency,
                o.Items.Count,
                o.CreatedAt))
            .ToList()
            .AsReadOnly();

        return new PagedResult<OrderSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
