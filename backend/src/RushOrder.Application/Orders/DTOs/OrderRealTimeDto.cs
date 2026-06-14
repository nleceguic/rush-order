namespace RushOrder.Application.Orders.DTOs;

public record OrderRealTimeDto(
    Guid OrderId,
    string OrderNumber,
    Guid RestaurantId,
    Guid TableId,
    string TableName,
    string Status,
    string Source,
    IReadOnlyList<OrderItemRealTimeDto> Items,
    decimal Total,
    string Currency,
    string? Notes,
    DateTimeOffset PlacedAt);

public record OrderItemRealTimeDto(
    Guid ProductId,
    string Name,
    int Quantity,
    string? Notes,
    IReadOnlyList<string> Modifiers);
