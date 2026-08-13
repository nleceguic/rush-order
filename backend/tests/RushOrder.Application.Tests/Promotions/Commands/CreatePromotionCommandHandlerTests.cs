using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Promotions.Commands;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tests.Promotions.Commands;

public sealed class CreatePromotionCommandHandlerTests
{
    private readonly Mock<IPromotionRepository> _promotionRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenant = new();

    private readonly CreatePromotionCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public CreatePromotionCommandHandlerTests()
    {
        _tenant.Setup(t => t.TenantId).Returns(TenantId);
        _handler = new CreatePromotionCommandHandler(_promotionRepo.Object, _unitOfWork.Object, _tenant.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesAndPersistsPromotion()
    {
        var command = new CreatePromotionCommand(
            RestaurantId, "2x1 en cañas", "Toda la tarde",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));

        var id = await _handler.Handle(command, CancellationToken.None);

        id.Should().NotBeEmpty();
        _promotionRepo.Verify(r => r.AddAsync(
            It.Is<Promotion>(p => p.Name == "2x1 en cañas" && p.RestaurantId == RestaurantId),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ThrowsUnauthorizedAccessException()
    {
        _tenant.Setup(t => t.TenantId).Returns((Guid?)null);
        var command = new CreatePromotionCommand(
            RestaurantId, "2x1 en cañas", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // --- Validator ---

    [Fact]
    public async Task Validator_EndDateBeforeStartDate_FailsValidation()
    {
        var validator = new CreatePromotionCommandValidator();
        var command = new CreatePromotionCommand(
            RestaurantId, "2x1 en cañas", null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1));

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EndDate");
    }

    [Fact]
    public async Task Validator_EmptyName_FailsValidation()
    {
        var validator = new CreatePromotionCommandValidator();
        var command = new CreatePromotionCommand(
            RestaurantId, string.Empty, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validator_ValidCommand_PassesValidation()
    {
        var validator = new CreatePromotionCommandValidator();
        var command = new CreatePromotionCommand(
            RestaurantId, "2x1 en cañas", "Toda la tarde",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
