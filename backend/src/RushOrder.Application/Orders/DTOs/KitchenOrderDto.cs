namespace RushOrder.Application.Orders.DTOs;

public record KitchenOrderItemDto(
    Guid ProductId,
    string Name,
    int Quantity,
    string? Notes,
    IReadOnlyList<string> Modifiers);

public record KitchenOrderDto(
    Guid Id,
    string OrderNumber,
    Guid TableId,
    string Status,
    IReadOnlyList<KitchenOrderItemDto> Items,
    string? Notes,
    DateTimeOffset CreatedAt);
