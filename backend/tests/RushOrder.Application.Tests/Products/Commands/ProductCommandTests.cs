using FluentAssertions;
using FluentValidation;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Menu.EventHandlers;
using RushOrder.Application.Products.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Events;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Tests.Products.Commands;

public sealed class ProductCommandTests
{
    // --- CreateProductCommand: negative price fails validation ---

    [Fact]
    public async Task CreateProductCommand_NegativePrice_FailsValidation()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand(
            CategoryId: Guid.NewGuid(),
            Name: "Test Product",
            Description: null,
            Price: -5.00m,
            ImageUrl: null,
            Allergens: [],
            Tags: [],
            PreparationMinutes: 10);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public async Task CreateProductCommand_ZeroPrice_FailsValidation()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand(
            CategoryId: Guid.NewGuid(),
            Name: "Test Product",
            Description: null,
            Price: 0m,
            ImageUrl: null,
            Allergens: [],
            Tags: [],
            PreparationMinutes: 10);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public async Task CreateProductCommand_ValidPrice_PassesValidation()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand(
            CategoryId: Guid.NewGuid(),
            Name: "Test Product",
            Description: null,
            Price: 9.99m,
            ImageUrl: null,
            Allergens: [],
            Tags: [],
            PreparationMinutes: 10);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    // --- ToggleProductAvailabilityCommand: publishes event that invalidates cache ---

    [Fact]
    public async Task ProductAvailabilityChangedHandler_InvalidatesMenuCache()
    {
        var cacheService = new Mock<IMenuCacheService>();
        var handler = new ProductAvailabilityChangedEventHandler(cacheService.Object);

        var restaurantId = Guid.NewGuid();
        var @event = new ProductAvailabilityChangedEvent(Guid.NewGuid(), restaurantId, false);

        await handler.Handle(@event, CancellationToken.None);

        cacheService.Verify(
            c => c.InvalidateMenuAsync(restaurantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleProductAvailabilityCommand_ProductNotFound_ThrowsNotFoundException()
    {
        var productRepo = new Mock<IProductRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        productRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var handlerInstance = new ToggleProductAvailabilityCommandHandler(productRepo.Object, unitOfWork.Object);

        var act = () => handlerInstance.Handle(
            new ToggleProductAvailabilityCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<RushOrder.Application.Common.Exceptions.NotFoundException>();
    }
}
