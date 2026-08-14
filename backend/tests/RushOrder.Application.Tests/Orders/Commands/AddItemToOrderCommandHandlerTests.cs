using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class AddItemToOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly AddItemToOrderCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public AddItemToOrderCommandHandlerTests()
    {
        _handler = new AddItemToOrderCommandHandler(
            _orderRepo.Object, _productRepo.Object, _unitOfWork.Object, _tenantService.Object);
    }

    private Order BuildOrder()
        => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    private Product BuildProduct(bool isAvailable = true)
    {
        var product = Product.Create(TenantId, RestaurantId, CategoryId, "Agua Mineral", new Money(1.50m, "EUR"));
        if (!isAvailable) product.SetAvailability(false);
        return product;
    }

    private AddItemToOrderCommand BuildCommand() => new(OrderId, ProductId, Quantity: 2);

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Order*");
    }

    [Fact]
    public async Task Handle_StaffCaller_AddsItemRegardlessOfTable()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProduct());
        // IsQrSession defaults to false on a bare mock — this is the staff path.

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_AddsItem()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProduct());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QrSessionWrongTable_ThrowsUnauthorizedAccessException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_UnavailableProduct_ThrowsBusinessRuleException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProduct(isAvailable: false));

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not available*");
    }
}
