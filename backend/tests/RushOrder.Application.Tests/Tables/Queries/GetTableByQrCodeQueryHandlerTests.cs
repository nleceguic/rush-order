using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.Queries;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tests.Tables.Queries;

public sealed class GetTableByQrCodeQueryHandlerTests
{
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IRestaurantRepository> _restaurantRepo = new();

    private readonly GetTableByQrCodeQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetTableByQrCodeQueryHandlerTests()
    {
        _handler = new GetTableByQrCodeQueryHandler(_tableRepo.Object, _restaurantRepo.Object);
    }

    [Fact]
    public async Task Handle_InvalidQrCode_ReturnsNull()
    {
        _tableRepo.Setup(r => r.GetByQrCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Table?)null);

        var result = await _handler.Handle(new GetTableByQrCodeQuery("INVALID_QR"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InactiveRestaurant_ReturnsNull()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");
        restaurant.Deactivate();

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var result = await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNull()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Restaurant?)null);

        var result = await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidQrCode_ReturnsTableAndRestaurantData()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 5", 4, "Terraza");
        var restaurant = Restaurant.Create(
            TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com", taxRate: 0.10m);

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var result = await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        result.Should().NotBeNull();
        result!.TableId.Should().Be(table.Id);
        result.Name.Should().Be("Mesa 5");
        result.Capacity.Should().Be(4);
        result.Zone.Should().Be("Terraza");
        result.RestaurantId.Should().Be(restaurant.Id);
        result.RestaurantName.Should().Be("El Restaurante");
        result.Currency.Should().Be(restaurant.Currency);
        result.UpsellingEnabled.Should().Be(restaurant.Settings.UpsellingEnabled);
        result.AvailableLocales.Should().Contain("es");
        result.VatRate.Should().Be(0.10m);
    }

    [Fact]
    public async Task Handle_RestaurantWithoutStripeAccount_OnlinePaymentDisabled()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var result = await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        result!.OnlinePaymentEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RestaurantWithStripeAccount_OnlinePaymentEnabled()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");
        restaurant.SetStripeAccountId("acct_123");

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var result = await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        result!.OnlinePaymentEnabled.Should().BeTrue();
    }
}
