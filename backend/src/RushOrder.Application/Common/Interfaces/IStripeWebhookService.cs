namespace RushOrder.Application.Common.Interfaces;

public interface IStripeWebhookService
{
    Task ProcessAsync(string rawBody, string stripeSignature, CancellationToken cancellationToken = default);
}
