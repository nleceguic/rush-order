using Bogus;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Enums;

namespace RushOrder.Tests.Common.Fakers;

public static class OrderFaker
{
    private static readonly Faker _faker = new("es");

    public static CreateOrderCommand ACommand(Guid? tableId = null)
        => new(
            TableId:    tableId ?? Guid.NewGuid(),
            CustomerId: null,
            Items:      [AnItem()],
            Notes:      _faker.Lorem.Sentence(),
            Source:     _faker.PickRandom<OrderSource>());

    public static CreateOrderItemInput AnItem(Guid? productId = null, int? quantity = null)
        => new(
            ProductId: productId ?? Guid.NewGuid(),
            Quantity:  quantity  ?? _faker.Random.Int(1, 5),
            Notes:     null);
}
