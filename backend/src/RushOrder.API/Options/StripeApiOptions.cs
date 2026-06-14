using System.ComponentModel.DataAnnotations;

namespace RushOrder.API.Options;

public sealed class StripeApiOptions
{
    public const string Section = "Stripe";

    [Required]
    public string PublishableKey { get; set; } = string.Empty;
}
