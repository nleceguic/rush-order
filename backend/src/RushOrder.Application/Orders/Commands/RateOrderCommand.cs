using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Orders.Commands;

// Matches the PWA's RatingSheet exactly: food/speed/service, 1-5 each,
// optional comment. AllowAnonymous on the controller — guests rate without
// an account, same as CreateOrder.
public record RateOrderCommand(
    Guid OrderId, int Food, int Speed, int Service, string? Comment) : ICommand<Unit>;

public sealed class RateOrderCommandValidator : AbstractValidator<RateOrderCommand>
{
    public RateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Food).InclusiveBetween(1, 5);
        RuleFor(x => x.Speed).InclusiveBetween(1, 5);
        RuleFor(x => x.Service).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public sealed class RateOrderCommandHandler : IRequestHandler<RateOrderCommand, Unit>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderRatingRepository _ratings;
    private readonly IUnitOfWork _unitOfWork;

    public RateOrderCommandHandler(IOrderRepository orders, IOrderRatingRepository ratings, IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _ratings = ratings;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        var existing = await _ratings.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null) return Unit.Value; // idempotent — one rating per order

        var rating = OrderRating.Create(
            order.TenantId, order.Id, order.RestaurantId,
            request.Food, request.Speed, request.Service, request.Comment);

        await _ratings.AddAsync(rating, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
