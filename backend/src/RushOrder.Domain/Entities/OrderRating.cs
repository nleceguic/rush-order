using RushOrder.Domain.Common;

namespace RushOrder.Domain.Entities;

// Order-level rating (food/speed/service, 1-5) — matches the PWA's
// RatingSheet exactly (it already POSTs this shape to /orders/{id}/rating,
// which had no backend endpoint until now). There's no per-product rating
// in this schema; product-level "worst rated" signals (anomaly detection,
// weekly insights) are derived by joining an order's FoodRating against the
// products in that order's Items.
public sealed class OrderRating : TenantEntity
{
    public Guid OrderId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public int FoodRating { get; private set; }
    public int SpeedRating { get; private set; }
    public int ServiceRating { get; private set; }
    public string? Comment { get; private set; }

    private OrderRating() { } // EF Core

    private OrderRating(
        Guid tenantId, Guid orderId, Guid restaurantId,
        int foodRating, int speedRating, int serviceRating, string? comment) : base(tenantId)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));
        EnsureStars(foodRating, nameof(foodRating));
        EnsureStars(speedRating, nameof(speedRating));
        EnsureStars(serviceRating, nameof(serviceRating));

        OrderId = orderId;
        RestaurantId = restaurantId;
        FoodRating = foodRating;
        SpeedRating = speedRating;
        ServiceRating = serviceRating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public static OrderRating Create(
        Guid tenantId, Guid orderId, Guid restaurantId,
        int foodRating, int speedRating, int serviceRating, string? comment = null)
        => new(tenantId, orderId, restaurantId, foodRating, speedRating, serviceRating, comment);

    private static void EnsureStars(int value, string paramName)
    {
        if (value is < 1 or > 5) throw new ArgumentException("Rating must be between 1 and 5.", paramName);
    }
}
