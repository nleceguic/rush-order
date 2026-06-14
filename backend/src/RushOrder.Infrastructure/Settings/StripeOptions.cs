using System.ComponentModel.DataAnnotations;

namespace RushOrder.Infrastructure.Settings;

public sealed class StripeOptions
{
    public const string Section = "Stripe";

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string PublishableKey { get; set; } = string.Empty;

    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    public double ApplicationFeePercent { get; set; } = 0.005;
}
