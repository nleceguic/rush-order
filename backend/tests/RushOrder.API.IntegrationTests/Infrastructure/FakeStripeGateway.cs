using RushOrder.Application.Common.Interfaces;

namespace RushOrder.API.IntegrationTests.Infrastructure;

/// <summary>In-memory Stripe gateway — returns deterministic test data without hitting Stripe's API.</summary>
public sealed class FakeStripeGateway : IStripeGateway
{
    public Task<StripeIntentResult> CreatePaymentIntentAsync(
        long amountCents,
        string currency,
        string? connectAccountId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var intentId = $"pi_test_{orderId:N}";
        var clientSecret = $"{intentId}_secret_test";
        return Task.FromResult(new StripeIntentResult(intentId, clientSecret));
    }

    public Task<string> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken = default)
        => Task.FromResult("succeeded");

    public Task<string> CreateRefundAsync(
        string paymentIntentId,
        long? amountCents,
        string reason,
        CancellationToken cancellationToken = default)
        => Task.FromResult($"re_test_{Guid.NewGuid():N}");
}
