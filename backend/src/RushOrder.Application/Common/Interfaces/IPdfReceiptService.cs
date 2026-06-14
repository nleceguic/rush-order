namespace RushOrder.Application.Common.Interfaces;

public record ReceiptLineItem(string Name, int Quantity, decimal UnitPrice, decimal LineTotal, string Currency);

public record ReceiptData(
    Guid PaymentId,
    string OrderNumber,
    DateTimeOffset PaidAt,
    string RestaurantName,
    string RestaurantAddress,
    string RestaurantPhone,
    string RestaurantEmail,
    IReadOnlyList<ReceiptLineItem> Items,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TaxRate,
    decimal DiscountAmount,
    decimal TipAmount,
    decimal Total,
    string Currency);

public interface IPdfReceiptService
{
    byte[] GenerateReceipt(ReceiptData data);
}
