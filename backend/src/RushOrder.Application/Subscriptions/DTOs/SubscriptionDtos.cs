namespace RushOrder.Application.Subscriptions.DTOs;

public record PlanDto(
    Guid Id,
    string Name,
    decimal PriceMonthly,
    decimal PriceYearly,
    string Currency,
    int MaxTables,
    int MaxUsers,
    int MaxRestaurants,
    IReadOnlyList<string> Features,
    bool IsActive);

public record CurrentSubscriptionDto(
    Guid SubscriptionId,
    PlanDto Plan,
    string Status,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    int DaysRemaining);

public record CheckoutSessionDto(string CheckoutUrl);

public record BillingInvoiceDto(
    string InvoiceId,
    DateTimeOffset Date,
    decimal Amount,
    string Currency,
    string Status,
    string? PdfUrl);

public record UsageDto(
    int TablesUsed,
    int MaxTables,
    int UsersUsed,
    int MaxUsers,
    int RestaurantsUsed,
    int MaxRestaurants);

public record CreateCheckoutSessionRequest(Guid PlanId, string BillingPeriod = "monthly");
public record CancelSubscriptionRequest(bool Immediately = false);
