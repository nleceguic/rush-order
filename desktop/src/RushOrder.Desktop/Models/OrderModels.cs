namespace RushOrder.Desktop.Models;

public enum OrderStatus { New, Preparing, Ready, Served, Paid }

public sealed record OrderDto(
    Guid   Id,
    string OrderNumber,
    Guid   RestaurantId,
    Guid?  TableId,
    string TableName,
    int    GuestCount,
    OrderStatus Status,
    string Source,
    string? WaiterName,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? Notes,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ServedAt,
    DateTimeOffset? PaidAt,
    bool IsOffline = false)
{
    public TimeSpan Age => DateTimeOffset.Now - PlacedAt;
}

public sealed record OrderItemDto(
    Guid   Id,
    Guid   ProductId,
    string ProductName,
    int    Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Modifiers);

public sealed record ProductDto(
    Guid   Id,
    string Name,
    string Category,
    decimal Price,
    bool   IsAvailable,
    IReadOnlyList<string> Allergens);

public sealed record CreateOrderRequest(
    Guid   RestaurantId,
    Guid?  TableId,
    string TableName,
    int    GuestCount,
    string Source,
    IReadOnlyList<CreateOrderItemRequest> Items,
    string? Notes);

public sealed record CreateOrderItemRequest(
    Guid    ProductId,
    string  ProductName,
    decimal UnitPrice,
    int     Quantity,
    string? Notes);

public sealed record UpdateOrderStatusRequest(Guid OrderId, OrderStatus NewStatus);

public sealed record PaymentRequest(
    Guid    OrderId,
    string  Method,
    decimal AmountTendered,
    int     SplitParts,
    bool    GenerateInvoice,
    string? CustomerCif);

public enum PaymentMethod { Cash, Card, Qr }
