using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Common.Interfaces;

public interface ISubscriptionService
{
    Task<Domain.Entities.Subscription> CreateTrialSubscriptionAsync(
        Guid tenantId,
        Guid planId,
        DateTimeOffset trialEnd,
        CancellationToken ct = default);

    Task<string> CreateCheckoutSessionAsync(
        Guid tenantId,
        Guid planId,
        string billingPeriod,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    Task CancelSubscriptionAsync(Guid tenantId, bool immediately, CancellationToken ct = default);

    Task ActivateFromStripeAsync(
        string stripeSubscriptionId,
        string stripeCustomerId,
        Guid planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default);

    Task SetPastDueAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task SuspendTenantForSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);

    Task<IReadOnlyList<BillingInvoiceDto>> GetBillingHistoryAsync(Guid tenantId, CancellationToken ct = default);
}
