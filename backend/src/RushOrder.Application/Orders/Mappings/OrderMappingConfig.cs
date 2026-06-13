using Mapster;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Orders.Mappings;

public class OrderMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OrderItem, OrderItemDto>()
            .Map(dest => dest.UnitPrice, src => src.UnitPrice.Amount)
            .Map(dest => dest.Currency, src => src.UnitPrice.Currency)
            .Map(dest => dest.LineTotal, src => src.LineTotal.Amount);

        config.NewConfig<OrderItem, KitchenOrderItemDto>();

        config.NewConfig<Order, KitchenOrderDto>()
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.Items, src => src.Items.Adapt<IReadOnlyList<KitchenOrderItemDto>>());
    }
}
