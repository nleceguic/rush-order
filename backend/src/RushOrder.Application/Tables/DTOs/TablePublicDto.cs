namespace RushOrder.Application.Tables.DTOs;

public record TablePublicDto(
    Guid TableId,
    string Name,
    int Capacity,
    string? Zone,
    Guid RestaurantId,
    string RestaurantName,
    string Currency,
    string? LogoUrl,
    string? CoverImageUrl,
    bool UpsellingEnabled,
    IReadOnlyList<string> AvailableLocales,
    decimal VatRate,
    bool OnlinePaymentEnabled,
    string? WelcomeMessage);
