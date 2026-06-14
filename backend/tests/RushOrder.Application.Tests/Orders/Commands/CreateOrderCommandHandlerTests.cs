using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IRestaurantRepository> _restaurantRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();
    private readonly Mock<IOrderVerificationService> _verificationService = new();

    private readonly CreateOrderCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public CreateOrderCommandHandlerTests()
    {
        _verificationService
            .Setup(s => s.GenerateToken(It.IsAny<Guid>()))
            .Returns("test-tracking-token");

        _handler = new CreateOrderCommandHandler(
            _orderRepo.Object,
            _productRepo.Object,
            _tableRepo.Object,
            _restaurantRepo.Object,
            _unitOfWork.Object,
            _tenantService.Object,
            _verificationService.Object);
    }

    // --- helpers ---

    private Table BuildTable(TableStatus status = TableStatus.Free)
        => Table.Create(TenantId, RestaurantId, "Mesa 1", 4, "Terraza");

    private Restaurant BuildRestaurant()
        => Restaurant.Create(TenantId, "El Restaurante", "Calle Mayor 1", "+34600000000",
            "test@restaurant.com", 0.10m, "Europe/Madrid", "EUR");

    private Product BuildProduct(bool isAvailable = true)
    {
        var product = Product.Create(TenantId, RestaurantId, CategoryId, "Agua Mineral",
            new Money(1.50m, "EUR"));
        if (!isAvailable) product.SetAvailability(false);
        return product;
    }

    private CreateOrderCommand BuildCommand(Guid? productId = null, int quantity = 2)
        => new(
            TableId,
            CustomerId: null,
            Items: [new CreateOrderItemInput(productId ?? ProductId, quantity, "sin hielo")],
            Notes: "mesa exterior",
            Source: OrderSource.Manual);

    private void SetupHappyPath(Table? table = null, Restaurant? restaurant = null, Product? product = null)
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table ?? BuildTable());
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant ?? BuildRestaurant());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product ?? BuildProduct());
        _orderRepo.Setup(r => r.GetNextSequenceNumberAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, CancellationToken _) => o);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    // --- tests ---

    [Fact]
    public async Task Handle_ValidCommand_ReturnsResultWithOrderNumber()
    {
        SetupHappyPath();

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
        result.OrderNumber.Should().MatchRegex(@"^#\d{4}-\d{4}$");
        _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_OrderHasCorrectItemsAndTotals()
    {
        SetupHappyPath();
        Order? captured = null;
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => captured = o)
            .ReturnsAsync((Order o, CancellationToken _) => o);

        await _handler.Handle(BuildCommand(quantity: 3), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Items.Should().HaveCount(1);
        captured.Items[0].Quantity.Should().Be(3);
        captured.Items[0].Name.Should().Be("Agua Mineral");
        captured.Total.Amount.Should().BeApproximately(4.95m, 0.01m); // 3 * 1.50 * 1.10
    }

    [Fact]
    public async Task Handle_NoTenantContext_ThrowsUnauthorizedAccessException()
    {
        _tenantService.Setup(s => s.TenantId).Returns((Guid?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TableNotFound_ThrowsNotFoundException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Table?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Table*");
    }

    [Fact]
    public async Task Handle_TableInCleaningStatus_ThrowsBusinessRuleException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        var table = Table.Create(TenantId, RestaurantId, "Mesa 1", 4);
        table.MarkCleaning();
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ThrowsNotFoundException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTable());
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Restaurant?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Restaurant*");
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTable());
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildRestaurant());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _orderRepo.Setup(r => r.GetNextSequenceNumberAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Product*");
    }

    [Fact]
    public async Task Handle_UnavailableProduct_ThrowsBusinessRuleException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tableRepo.Setup(r => r.GetByIdAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTable());
        _restaurantRepo.Setup(r => r.GetByIdAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildRestaurant());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProduct(isAvailable: false));
        _orderRepo.Setup(r => r.GetNextSequenceNumberAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task Handle_MultipleItems_AllAddedToOrder()
    {
        var productId2 = Guid.NewGuid();
        var product2 = Product.Create(TenantId, RestaurantId, CategoryId, "Café",
            new Money(1.20m, "EUR"));

        SetupHappyPath();
        _productRepo.Setup(r => r.GetByIdAsync(productId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);

        var command = new CreateOrderCommand(
            TableId,
            CustomerId: null,
            Items:
            [
                new CreateOrderItemInput(ProductId, 2),
                new CreateOrderItemInput(productId2, 1)
            ],
            Notes: null,
            Source: OrderSource.QR);

        Order? captured = null;
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => captured = o)
            .ReturnsAsync((Order o, CancellationToken _) => o);

        await _handler.Handle(command, CancellationToken.None);

        captured!.Items.Should().HaveCount(2);
    }
}
