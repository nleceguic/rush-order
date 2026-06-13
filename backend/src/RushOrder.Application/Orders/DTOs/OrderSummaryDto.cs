namespace RushOrder.Application.Orders.DTOs;

public record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    Guid TableId,
    string? TableName,
    string Status,
    string Source,
    decimal Total,
    string Currency,
    int ItemCount,
    DateTimeOffset CreatedAt);
