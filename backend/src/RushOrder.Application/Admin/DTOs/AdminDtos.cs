using RushOrder.Application.Subscriptions.DTOs;

namespace RushOrder.Application.Admin.DTOs;

public record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string PlanName,
    int RestaurantsCount,
    int UsersCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TrialEndsAt);

public record TenantBillingInfoDto(
    string? CompanyName,
    string? TaxId,
    string? InvoiceEmail,
    string? Country);

public record TenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string PlanName,
    TenantBillingInfoDto BillingInfo,
    CurrentSubscriptionDto? Subscription,
    int RestaurantsCount,
    int UsersCount,
    int TablesCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TrialEndsAt,
    string? StripeCustomerId);

public record GlobalMetricsDto(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int SuspendedTenants,
    decimal Mrr,
    decimal Arr,
    int NewTenantsThisWeek,
    double ChurnRatePercent);

public record SuspendTenantRequest(string Reason);
public record UpdateTenantPlanRequest(Guid PlanId);
public record ImpersonateTokenDto(string Token, DateTimeOffset ExpiresAt);
