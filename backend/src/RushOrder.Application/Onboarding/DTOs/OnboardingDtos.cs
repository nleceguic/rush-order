namespace RushOrder.Application.Onboarding.DTOs;

public record RegisterTenantRequest(
    string BusinessName,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerFirstName,
    string OwnerLastName,
    string? Phone = null);

public record RegisterTenantResult(
    Guid TenantId,
    Guid RestaurantId,
    string AccessToken,
    DateTimeOffset TokenExpires);
