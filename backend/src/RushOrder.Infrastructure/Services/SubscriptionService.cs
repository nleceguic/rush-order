using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Subscriptions.DTOs;
using RushOrder.Domain.Enums;
using RushOrder.Infrastructure.Settings;
using Stripe;
using Stripe.Checkout;

namespace RushOrder.Infrastructure.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ITenantRepository _tenantRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;
    private readonly StripeOptions _stripe;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ITenantRepository tenantRepo,
        ISubscriptionRepository subscriptionRepo,
        IPlanRepository planRepo,
        IUnitOfWork unitOfWork,
        INotificationService notifications,
        IOptions<StripeOptions> stripeOptions,
        ILogger<SubscriptionService> logger)
    {
        _tenantRepo = tenantRepo;
        _subscriptionRepo = subscriptionRepo;
        _planRepo = planRepo;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _stripe = stripeOptions.Value;
        _logger = logger;
    }

    public async Task<Domain.Entities.Subscription> CreateTrialSubscriptionAsync(
        Guid tenantId, Guid planId, DateTimeOffset trialEnd, CancellationToken ct = default)
    {
        var subscription = Domain.Entities.Subscription.CreateTrial(tenantId, planId, trialEnd);
        await _subscriptionRepo.AddAsync(subscription, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Guid tenantId, Guid planId, string billingPeriod,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);

        var plan = await _planRepo.GetByIdAsync(planId, ct)
            ?? throw new NotFoundException("Plan", planId);

        var priceId = billingPeriod == "yearly"
            ? plan.StripePriceIdYearly
            : plan.StripePriceIdMonthly;

        if (string.IsNullOrEmpty(priceId))
            throw new BusinessRuleException(
                $"Stripe price not configured for plan '{plan.Name}' ({billingPeriod}).");

        var customerId = await EnsureStripeCustomerAsync(tenant, ct);

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString(),
                ["planId"] = planId.ToString()
            }
        }, cancellationToken: ct);

        return session.Url;
    }

    public async Task CancelSubscriptionAsync(Guid tenantId, bool immediately, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepo.GetByTenantIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Subscription", tenantId);

        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            try
            {
                var svc = new Stripe.SubscriptionService();
                if (immediately)
                    await svc.CancelAsync(subscription.StripeSubscriptionId, cancellationToken: ct);
                else
                    await svc.UpdateAsync(subscription.StripeSubscriptionId,
                        new SubscriptionUpdateOptions { CancelAtPeriodEnd = true }, cancellationToken: ct);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error cancelling subscription {Id}", subscription.StripeSubscriptionId);
                throw new BusinessRuleException("Failed to cancel subscription with Stripe.");
            }
        }

        subscription.Cancel(immediately);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ActivateFromStripeAsync(
        string stripeSubscriptionId,
        string stripeCustomerId,
        Guid planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId, ct);

        if (subscription is null)
        {
            // Find tenant by Stripe customer ID
            var tenants = await _tenantRepo.GetAllAsync(ct);
            var tenant = tenants.FirstOrDefault(t => t.StripeCustomerId == stripeCustomerId);
            if (tenant is null)
            {
                _logger.LogWarning(
                    "No tenant found for Stripe customer {CustomerId}", stripeCustomerId);
                return;
            }

            subscription = await _subscriptionRepo.GetByTenantIdAsync(tenant.Id, ct);
            if (subscription is null) return;
        }

        subscription.Activate(periodStart, periodEnd, stripeSubscriptionId);

        // Also activate the tenant if it was trial/suspended
        var tenantToActivate = await _tenantRepo.GetByIdAsync(subscription.TenantId, ct);
        if (tenantToActivate?.Status is TenantStatus.Trial or TenantStatus.TrialExpired or TenantStatus.Suspended)
            tenantToActivate.Activate();

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetPastDueAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId, ct);
        if (subscription is null) return;

        subscription.SetPastDue();
        await _unitOfWork.SaveChangesAsync(ct);

        _ = TryNotifyPaymentDueAsync(subscription.TenantId, ct);
    }

    public async Task SuspendTenantForSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepo.GetByStripeSubscriptionIdAsync(stripeSubscriptionId, ct);
        if (subscription is null) return;

        subscription.Cancel(immediately: true);

        var tenant = await _tenantRepo.GetByIdAsync(subscription.TenantId, ct);
        tenant?.Suspend();

        await _unitOfWork.SaveChangesAsync(ct);

        _ = TryNotifySuspensionAsync(tenant?.BillingInfo.InvoiceEmail, tenant?.Name, ct);
    }

    public async Task<IReadOnlyList<BillingInvoiceDto>> GetBillingHistoryAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct);
        if (string.IsNullOrEmpty(tenant?.StripeCustomerId))
            return [];

        try
        {
            var invoiceService = new InvoiceService();
            var invoices = await invoiceService.ListAsync(
                new InvoiceListOptions
                {
                    Customer = tenant.StripeCustomerId,
                    Limit = 20
                }, cancellationToken: ct);

            return invoices.Data.Select(inv => new BillingInvoiceDto(
                InvoiceId: inv.Id,
                Date: inv.Created,
                Amount: inv.AmountPaid / 100m,
                Currency: inv.Currency.ToUpperInvariant(),
                Status: inv.Status ?? "unknown",
                PdfUrl: inv.InvoicePdf))
                .ToList();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error fetching invoices for customer {CustomerId}",
                tenant.StripeCustomerId);
            return [];
        }
    }

    private async Task<string> EnsureStripeCustomerAsync(Domain.Entities.Tenant tenant, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(tenant.StripeCustomerId))
            return tenant.StripeCustomerId;

        var customerService = new CustomerService();
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Name = tenant.Name,
            Email = tenant.BillingInfo.InvoiceEmail,
            Metadata = new Dictionary<string, string> { ["tenantId"] = tenant.Id.ToString() }
        }, cancellationToken: ct);

        tenant.SetStripeCustomerId(customer.Id);
        await _unitOfWork.SaveChangesAsync(ct);

        return customer.Id;
    }

    private async Task TryNotifyPaymentDueAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct);
            var plan = tenant is not null
                ? await _planRepo.GetByIdAsync(tenant.PlanId, ct)
                : null;

            var email = tenant?.BillingInfo.InvoiceEmail;
            if (!string.IsNullOrEmpty(email))
                await _notifications.SendSubscriptionPaymentDueAsync(email, plan?.Name ?? "plan", ct);
        }
        catch { /* best-effort */ }
    }

    private async Task TryNotifySuspensionAsync(string? email, string? tenantName, CancellationToken ct)
    {
        try
        {
            if (!string.IsNullOrEmpty(email))
                await _notifications.SendSubscriptionSuspendedAsync(email, tenantName ?? string.Empty, ct);
        }
        catch { /* best-effort */ }
    }
}
