namespace RushOrder.Application.Orders.DTOs;

public record CreateOrderResult(
    Guid OrderId,
    string OrderNumber,
    DateTimeOffset? EstimatedReadyAt,
    string TrackingToken);
