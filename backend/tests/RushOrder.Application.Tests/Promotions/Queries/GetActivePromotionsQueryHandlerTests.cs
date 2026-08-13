using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Promotions.Queries;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tests.Promotions.Queries;

public sealed class GetActivePromotionsQueryHandlerTests
{
    private readonly Mock<IPromotionRepository> _promotionRepo = new();
    private readonly GetActivePromotionsQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetActivePromotionsQueryHandlerTests()
    {
        _handler = new GetActivePromotionsQueryHandler(_promotionRepo.Object);
    }

    [Fact]
    public async Task Handle_NoActivePromotions_ReturnsEmptyList()
    {
        _promotionRepo.Setup(r => r.GetActiveByRestaurantAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Promotion>());

        var result = await _handler.Handle(new GetActivePromotionsQuery(RestaurantId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ActivePromotionsExist_MapsToDto()
    {
        var promotion = Promotion.Create(
            TenantId, RestaurantId, "2x1 en cañas", "Toda la tarde",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _promotionRepo.Setup(r => r.GetActiveByRestaurantAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Promotion> { promotion });

        var result = await _handler.Handle(new GetActivePromotionsQuery(RestaurantId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(promotion.Id);
        result[0].Name.Should().Be("2x1 en cañas");
        result[0].Description.Should().Be("Toda la tarde");
    }

    [Fact]
    public async Task Handle_PromotionWithoutDescription_MapsToEmptyString()
    {
        var promotion = Promotion.Create(
            TenantId, RestaurantId, "Postre gratis", null,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _promotionRepo.Setup(r => r.GetActiveByRestaurantAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Promotion> { promotion });

        var result = await _handler.Handle(new GetActivePromotionsQuery(RestaurantId), CancellationToken.None);

        result[0].Description.Should().Be(string.Empty);
    }
}
