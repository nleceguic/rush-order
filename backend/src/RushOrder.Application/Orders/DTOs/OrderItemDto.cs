namespace RushOrder.Application.Orders.DTOs;

public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    string? Notes,
    IReadOnlyList<string> Modifiers,
    decimal LineTotal);
