using Microsoft.Extensions.Options;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Settings;
using Stripe;

namespace RushOrder.Infrastructure.Services;

public sealed class StripeGateway : IStripeGateway
{
    private readonly StripeOptions _options;

    public StripeGateway(IOptions<StripeOptions> options)
        => _options = options.Value;

    public async Task<StripeIntentResult> CreatePaymentIntentAsync(
        long amountCents,
        string currency,
        string? connectAccountId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency,
            Metadata = new Dictionary<string, string> { ["orderId"] = orderId.ToString() },
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
        };

        RequestOptions? requestOptions = null;
        if (!string.IsNullOrEmpty(connectAccountId))
        {
            var fee = (long)(amountCents * _options.ApplicationFeePercent);
            createOptions.ApplicationFeeAmount = fee;
            createOptions.TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = connectAccountId
            };
        }

        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(createOptions, requestOptions, cancellationToken);

        return new StripeIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<string> GetPaymentIntentStatusAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        var intent = await service.GetAsync(paymentIntentId, null, null, cancellationToken);
        return intent.Status;
    }

    public async Task<string> CreateRefundAsync(
        string paymentIntentId,
        long? amountCents,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var options = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = amountCents,
            Reason = reason
        };
        var service = new RefundService();
        var refund = await service.CreateAsync(options, null, cancellationToken);
        return refund.Id;
    }
}
