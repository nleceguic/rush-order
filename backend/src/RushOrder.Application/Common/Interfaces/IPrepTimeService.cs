namespace RushOrder.Application.Common.Interfaces;

public sealed record PrepTimeItem(Guid ProductId, int PreparationMinutes, int Quantity);

public interface IPrepTimeService
{
    // eta_minutes = sum(base_prep_time per item) * kitchen_load_factor
    // kitchen_load_factor = max(1.0, orders_in_preparation / kitchen_capacity)
    //   (clamped to a 1.0 floor — an idle kitchen doesn't cook faster than
    //   the base estimate, it just doesn't slow it down further)
    // base_prep_time per item: avg of the last 30 days' actual Preparing->Ready
    //   duration for that product (OrderStatusHistory), falling back to the
    //   product's static Product.PreparationMinutes when there's no history yet.
    Task<int> GetEtaMinutesAsync(
        Guid restaurantId, IReadOnlyList<PrepTimeItem> items, CancellationToken cancellationToken = default);
}
