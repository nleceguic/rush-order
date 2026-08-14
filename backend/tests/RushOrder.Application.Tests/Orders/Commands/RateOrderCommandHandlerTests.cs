using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class RateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IOrderRatingRepository> _ratingRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly RateOrderCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public RateOrderCommandHandlerTests()
    {
        _handler = new RateOrderCommandHandler(_orderRepo.Object, _ratingRepo.Object, _unitOfWork.Object, _tenantService.Object);
    }

    private Order BuildOrder() => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    private RateOrderCommand BuildCommand() => new(OrderId, Food: 5, Speed: 4, Service: 5, Comment: "Genial");

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Order*");
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_CreatesRating()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _ratingRepo.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((OrderRating?)null);
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<OrderRating>(), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Handle_AlreadyRated_IsIdempotent()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _ratingRepo.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderRating.Create(TenantId, OrderId, RestaurantId, 5, 5, 5, null));
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<OrderRating>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
