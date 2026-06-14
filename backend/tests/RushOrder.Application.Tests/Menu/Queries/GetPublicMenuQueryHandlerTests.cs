using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Menu.DTOs;
using RushOrder.Application.Menu.EventHandlers;
using RushOrder.Application.Menu.Queries;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Events;

namespace RushOrder.Application.Tests.Menu.Queries;

public sealed class GetPublicMenuQueryHandlerTests
{
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IRestaurantRepository> _restaurantRepo = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IMenuCacheService> _cacheService = new();

    private readonly GetPublicMenuQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetPublicMenuQueryHandlerTests()
    {
        _handler = new GetPublicMenuQueryHandler(
            _tableRepo.Object,
            _restaurantRepo.Object,
            _categoryRepo.Object,
            _productRepo.Object,
            _cacheService.Object);
    }

    [Fact]
    public async Task Handle_InvalidQrToken_ReturnsNull()
    {
        _cacheService.Setup(c => c.GetMenuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicMenuDto?)null);
        _tableRepo.Setup(r => r.GetByQrCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Table?)null);

        var result = await _handler.Handle(new GetPublicMenuQuery("INVALID_QR"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InactiveRestaurant_ReturnsNull()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");
        restaurant.Deactivate();

        _cacheService.Setup(c => c.GetMenuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicMenuDto?)null);
        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var result = await _handler.Handle(new GetPublicMenuQuery(table.QrCode), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedMenuWithoutDbAccess()
    {
        var cachedMenu = new PublicMenuDto(
            RestaurantId,
            new RestaurantInfoDto("Test", null, null, null),
            [],
            []);

        _cacheService.Setup(c => c.GetMenuAsync("QR001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedMenu);

        var result = await _handler.Handle(new GetPublicMenuQuery("QR001"), CancellationToken.None);

        result.Should().Be(cachedMenu);
        _tableRepo.Verify(r => r.GetByQrCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidQrToken_BuildsAndCachesMenu()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");

        _cacheService.Setup(c => c.GetMenuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicMenuDto?)null);
        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);
        _categoryRepo.Setup(r => r.GetByRestaurantPublicAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());
        _productRepo.Setup(r => r.GetByRestaurantPublicAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());

        var result = await _handler.Handle(new GetPublicMenuQuery(table.QrCode), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Restaurant.Name.Should().Be("El Restaurante");
        _cacheService.Verify(c => c.SetMenuAsync(
            table.QrCode,
            It.IsAny<PublicMenuDto>(),
            TimeSpan.FromSeconds(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
