namespace RushOrder.Application.Common.Interfaces;

public sealed record RestaurantOwnerRow(Guid RestaurantId, Guid TenantId, string RestaurantName, string OwnerEmail, string OwnerName);

public sealed record WeeklyInsightsDto(
    decimal RevenueThisWeek,
    decimal RevenueLastWeek,
    decimal RevenueChangePercent,
    string? StarProductName,
    int StarProductQuantity,
    string? ProductToReviewName,
    string? ProductToReviewReason,
    int? PeakHour,
    DateOnly? BestDay,
    decimal BestDayRevenue,
    int WeekendReservations,
    IReadOnlyList<string> WeekendForecastTopProducts);

public interface IWeeklyInsightsRepository
{
    // One row per Owner/Admin user of every active restaurant — the weekly
    // email recipients.
    Task<IReadOnlyList<RestaurantOwnerRow>> GetActiveRestaurantOwnersAsync(CancellationToken cancellationToken = default);

    Task<WeeklyInsightsDto> BuildInsightsAsync(
        Guid tenantId, Guid restaurantId, DateTimeOffset weekStart, DateTimeOffset weekEnd,
        DateOnly nextSaturday, DateOnly nextSunday, CancellationToken cancellationToken = default);
}
