using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;

namespace RushOrder.Domain.Entities;

// One row per status transition an order goes through. Order itself only
// keeps its current Status + UpdatedAt (the last transition), so this is the
// only place "how long did Confirmed -> Ready actually take" can be computed
// from — needed for the kitchen ETA prediction's base_prep_time.
public sealed class OrderStatusHistory : TenantEntity
{
    public Guid OrderId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private OrderStatusHistory() { } // EF Core

    private OrderStatusHistory(
        Guid tenantId, Guid orderId, Guid restaurantId, OrderStatus? fromStatus, OrderStatus toStatus) : base(tenantId)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        if (restaurantId == Guid.Empty) throw new ArgumentException("RestaurantId cannot be empty.", nameof(restaurantId));

        OrderId = orderId;
        RestaurantId = restaurantId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    public static OrderStatusHistory Create(
        Guid tenantId, Guid orderId, Guid restaurantId, OrderStatus? fromStatus, OrderStatus toStatus)
        => new(tenantId, orderId, restaurantId, fromStatus, toStatus);
}
