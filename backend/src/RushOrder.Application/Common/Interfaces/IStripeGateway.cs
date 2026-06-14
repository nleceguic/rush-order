namespace RushOrder.Application.Common.Interfaces;

public record StripeIntentResult(string PaymentIntentId, string ClientSecret);

public interface IStripeGateway
{
    Task<StripeIntentResult> CreatePaymentIntentAsync(
        long amountCents,
        string currency,
        string? connectAccountId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<string> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task<string> CreateRefundAsync(
        string paymentIntentId,
        long? amountCents,
        string reason,
        CancellationToken cancellationToken = default);
}
