using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Settings;
using Stripe;
using Stripe.BillingPortal;

namespace RushOrder.Infrastructure.Services;

public sealed class StripeWebhookService : IStripeWebhookService
{
    private readonly StripeOptions _options;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly INotificationService _notificationService;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IOptions<StripeOptions> options,
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository,
        IRestaurantRepository restaurantRepository,
        ICustomerRepository customerRepository,
        INotificationService notificationService,
        IPdfReceiptService pdfReceiptService,
        ISubscriptionService subscriptionService,
        IUnitOfWork unitOfWork,
        ILogger<StripeWebhookService> logger)
    {
        _options = options.Value;
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _restaurantRepository = restaurantRepository;
        _customerRepository = customerRepository;
        _notificationService = notificationService;
        _pdfReceiptService = pdfReceiptService;
        _subscriptionService = subscriptionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessAsync(string rawBody, string stripeSignature, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(rawBody, stripeSignature, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new InvalidOperationException("Invalid Stripe webhook signature.", ex);
        }

        _logger.LogInformation("Processing Stripe webhook event {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                await HandlePaymentSucceededAsync(stripeEvent, cancellationToken);
                break;

            case "payment_intent.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent, cancellationToken);
                break;

            case "charge.dispute.created":
                await HandleDisputeCreatedAsync(stripeEvent, cancellationToken);
                break;

            case "invoice.payment_succeeded":
                await HandleInvoicePaymentSucceededAsync(stripeEvent, cancellationToken);
                break;

            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe webhook event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandlePaymentSucceededAsync(Event stripeEvent, CancellationToken ct)
    {
        var intent = stripeEvent.Data.Object as PaymentIntent;
        if (intent is null) return;

        var payment = await _paymentRepository.GetByProviderPaymentIdAsync(intent.Id, ct);
        if (payment is null)
        {
            _logger.LogWarning("No payment found for PaymentIntent {Id} in payment_intent.succeeded webhook.", intent.Id);
            return;
        }

        if (payment.Status == Domain.Enums.PaymentStatus.Completed) return;

        payment.Complete(intent.Id);

        var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
        if (order is not null) order.Pay();

        await _unitOfWork.SaveChangesAsync(ct);

        _ = TrySendReceiptAsync(payment, order, ct);
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var intent = stripeEvent.Data.Object as PaymentIntent;
        if (intent is null) return;

        var payment = await _paymentRepository.GetByProviderPaymentIdAsync(intent.Id, ct);
        if (payment is null) return;

        var reason = intent.LastPaymentError?.Message ?? "Payment failed";
        payment.Fail(reason);
        await _unitOfWork.SaveChangesAsync(ct);

        _ = TrySendFailureNotificationAsync(payment, reason, ct);
    }

    private async Task HandleDisputeCreatedAsync(Event stripeEvent, CancellationToken ct)
    {
        var dispute = stripeEvent.Data.Object as Dispute;
        if (dispute is null) return;

        var payment = await _paymentRepository.GetByProviderPaymentIdAsync(dispute.PaymentIntentId, ct);
        if (payment is null) return;

        var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
        var restaurant = order is not null
            ? await _restaurantRepository.GetByIdAsync(order.RestaurantId, ct)
            : null;

        if (restaurant is not null && order is not null)
        {
            await _notificationService.SendDisputeAlertAsync(
                restaurant.Email,
                order.OrderNumber,
                dispute.Id,
                ct);
        }
    }

    private async Task TrySendReceiptAsync(Domain.Entities.Payment payment, Domain.Entities.Order? order, CancellationToken ct)
    {
        try
        {
            if (order is null || !payment.CustomerId.HasValue) return;

            var customer = await _customerRepository.GetByIdAsync(payment.CustomerId.Value, ct);
            if (customer?.Email is null) return;

            var restaurant = await _restaurantRepository.GetByIdAsync(order.RestaurantId, ct);
            if (restaurant is null) return;

            var receiptData = new ReceiptData(
                PaymentId: payment.Id,
                OrderNumber: order.OrderNumber,
                PaidAt: payment.UpdatedAt,
                RestaurantName: restaurant.Name,
                RestaurantAddress: restaurant.Address,
                RestaurantPhone: restaurant.Phone,
                RestaurantEmail: restaurant.Email,
                Items: order.Items.Select(i => new ReceiptLineItem(
                    i.Name, i.Quantity, i.UnitPrice.Amount, i.LineTotal.Amount, i.LineTotal.Currency)).ToList(),
                Subtotal: order.Subtotal.Amount,
                TaxAmount: order.TaxAmount.Amount,
                TaxRate: order.TaxRate,
                DiscountAmount: order.DiscountAmount.Amount,
                TipAmount: order.TipAmount.Amount,
                Total: order.Total.Amount,
                Currency: order.Total.Currency);

            var pdfBytes = _pdfReceiptService.GenerateReceipt(receiptData);
            await _notificationService.SendPaymentReceiptAsync(customer.Email.Value, order.OrderNumber, pdfBytes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment receipt for payment {PaymentId}.", payment.Id);
        }
    }

    private async Task HandleInvoicePaymentSucceededAsync(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.SubscriptionId is null) return;

        var planId = Guid.Empty;
        if (invoice.Metadata?.TryGetValue("planId", out var planIdStr) == true)
            Guid.TryParse(planIdStr, out planId);

        var periodStart = new DateTimeOffset(invoice.PeriodStart, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(invoice.PeriodEnd, TimeSpan.Zero);

        await _subscriptionService.ActivateFromStripeAsync(
            invoice.SubscriptionId,
            invoice.CustomerId,
            planId,
            periodStart,
            periodEnd,
            ct);
    }

    private async Task HandleInvoicePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.SubscriptionId is null) return;

        await _subscriptionService.SetPastDueAsync(invoice.SubscriptionId, ct);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken ct)
    {
        var sub = stripeEvent.Data.Object as Stripe.Subscription;
        if (sub is null) return;

        await _subscriptionService.SuspendTenantForSubscriptionAsync(sub.Id, ct);
    }

    private async Task TrySendFailureNotificationAsync(Domain.Entities.Payment payment, string reason, CancellationToken ct)
    {
        try
        {
            if (!payment.CustomerId.HasValue) return;
            var customer = await _customerRepository.GetByIdAsync(payment.CustomerId.Value, ct);
            if (customer?.Email is null) return;

            var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
            if (order is null) return;

            await _notificationService.SendPaymentFailedAsync(customer.Email.Value, order.OrderNumber, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment failure notification for payment {PaymentId}.", payment.Id);
        }
    }
}
