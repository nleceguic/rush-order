namespace RushOrder.Application.Payments.DTOs;

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid? CustomerId,
    decimal Amount,
    string Currency,
    string Method,
    string Provider,
    string? ProviderPaymentId,
    string Status,
    string? ReceiptUrl,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public record CreatePaymentIntentResult(
    string ClientSecret,
    string PaymentIntentId,
    Guid PaymentId);

public record ConfirmPaymentResult(
    PaymentDto Payment,
    string? ReceiptUrl);

public record SplitPaymentIntentResult(
    Guid? CustomerId,
    decimal Amount,
    string ClientSecret,
    string PaymentIntentId,
    Guid PaymentId);
