using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.Events;
using RushOrder.Domain.ValueObjects;
using RushOrder.Tests.Common.Builders;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class UpdateOrderStatusCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IUnitOfWork>      _unitOfWork = new();
    private readonly UpdateOrderStatusCommandHandler _handler;

    private static readonly Guid RestaurantId = TenantDefaults.RestaurantId;

    public UpdateOrderStatusCommandHandlerTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new UpdateOrderStatusCommandHandler(_orderRepo.Object, _unitOfWork.Object);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private Order BuildPendingOrder()
        => new OrderBuilder().WithItem().Build();

    private Order BuildConfirmedOrder()
    {
        var o = BuildPendingOrder(); o.Confirm(); return o;
    }

    private Order BuildPreparingOrder()
    {
        var o = BuildConfirmedOrder(); o.StartPreparation(); return o;
    }

    private Order BuildReadyOrder()
    {
        var o = BuildPreparingOrder(); o.MarkReady(); return o;
    }

    private Order BuildServedOrder()
    {
        var o = BuildReadyOrder(); o.MarkServed(); return o;
    }

    private Order BuildPaidOrder()
    {
        var o = BuildServedOrder(); o.Pay(); return o;
    }

    private void SetupRepo(Order order)
        => _orderRepo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(order);

    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((Order?)null);

        var act = () => _handler.Handle(
            new UpdateOrderStatusCommand(orderId, OrderStatus.Confirmed, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingToConfirmed_StatusChangedAndSaved()
    {
        var order = BuildPendingOrder();
        SetupRepo(order);
        _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Confirmed, null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Confirmed);
        _orderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConfirmedToPreparing_StatusChanged()
    {
        var order = BuildConfirmedOrder();
        SetupRepo(order);
        _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Preparing, null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public async Task Handle_PreparingToReady_EventRaisedWithNewStatusReady()
    {
        var order = BuildPreparingOrder();
        SetupRepo(order);
        _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        order.ClearDomainEvents();

        await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Ready, null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Ready);
        order.DomainEvents.OfType<OrderStatusChangedEvent>()
            .Should().ContainSingle(ev => ev.NewStatus == OrderStatus.Ready);
    }

    [Fact]
    public async Task Handle_PaidToCancelled_ThrowsInvalidOperationException()
    {
        var order = BuildPaidOrder();
        SetupRepo(order);

        var act = () => _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Cancelled, "razón"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Cancel_FromPending_WithNote_StoresCancellationReason()
    {
        var order = BuildPendingOrder();
        SetupRepo(order);
        _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        const string note = "Cliente cambió de opinión";

        await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Cancelled, note),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be(note);
    }

    [Fact]
    public async Task Handle_InvalidTransition_ConfirmedToConfirmed_ThrowsInvalidOperationException()
    {
        var order = BuildConfirmedOrder();
        SetupRepo(order);

        var act = () => _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Confirmed, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_ServedToPaid_StatusBecomesPaid()
    {
        var order = BuildServedOrder();
        SetupRepo(order);
        _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, OrderStatus.Paid, null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Paid);
    }
}
