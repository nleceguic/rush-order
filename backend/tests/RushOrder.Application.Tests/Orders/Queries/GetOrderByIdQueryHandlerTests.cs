using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Queries;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Tests.Orders.Queries;

public sealed class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly GetOrderByIdQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public GetOrderByIdQueryHandlerTests()
    {
        _handler = new GetOrderByIdQueryHandler(_orderRepo.Object, _tableRepo.Object, _userRepo.Object, _tenantService.Object);
    }

    private Order BuildOrder() => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNull()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StaffCaller_ReturnsOrderRegardlessOfTable()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        // IsQrSession defaults to false on a bare mock — this is the staff path.

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_ReturnsOrder()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_QrSessionWrongTable_ThrowsUnauthorizedAccessException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
