using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Products.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Events;
using RushOrder.Tests.Common.Builders;

namespace RushOrder.Application.Tests.Products.Commands;

public sealed class ToggleProductAvailabilityHandlerTests
{
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork>        _unitOfWork  = new();
    private readonly ToggleProductAvailabilityCommandHandler _handler;

    public ToggleProductAvailabilityHandlerTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _handler = new ToggleProductAvailabilityCommandHandler(_productRepo.Object, _unitOfWork.Object);
    }

    private void SetupRepo(Product product)
        => _productRepo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(product);

    [Fact]
    public async Task Handle_AvailableProduct_BecomesUnavailable_ReturnsFalse()
    {
        var product = new ProductBuilder().Build(); // IsAvailable = true
        SetupRepo(product);
        _productRepo.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new ToggleProductAvailabilityCommand(product.Id),
            CancellationToken.None);

        result.Should().BeFalse();
        product.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnavailableProduct_BecomesAvailable_ReturnsTrue()
    {
        var product = new ProductBuilder().Unavailable().Build(); // IsAvailable = false
        SetupRepo(product);
        _productRepo.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new ToggleProductAvailabilityCommand(product.Id),
            CancellationToken.None);

        result.Should().BeTrue();
        product.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        _productRepo.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Product?)null);

        var act = () => _handler.Handle(
            new ToggleProductAvailabilityCommand(productId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Toggle_CallsUpdateAsyncAndSaveChanges()
    {
        var product = new ProductBuilder().Build();
        SetupRepo(product);
        _productRepo.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(
            new ToggleProductAvailabilityCommand(product.Id),
            CancellationToken.None);

        _productRepo.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Toggle_RaisesProductAvailabilityChangedEvent()
    {
        var product = new ProductBuilder().Build();
        SetupRepo(product);
        _productRepo.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(
            new ToggleProductAvailabilityCommand(product.Id),
            CancellationToken.None);

        product.DomainEvents.OfType<ProductAvailabilityChangedEvent>()
            .Should().ContainSingle(ev =>
                ev.ProductId == product.Id &&
                ev.IsAvailable == false);
    }

    [Fact]
    public async Task Handle_ToggleTwice_RestoresOriginalAvailability()
    {
        var product = new ProductBuilder().Build(); // starts true
        SetupRepo(product);
        _productRepo.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.Handle(new ToggleProductAvailabilityCommand(product.Id), CancellationToken.None);
        // need to reset cache for second call since entity is the same object
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(product);

        await _handler.Handle(new ToggleProductAvailabilityCommand(product.Id), CancellationToken.None);

        product.IsAvailable.Should().BeTrue(); // back to original
    }
}
