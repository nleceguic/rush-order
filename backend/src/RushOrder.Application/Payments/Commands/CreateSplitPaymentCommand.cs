using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.DTOs;
using RushOrder.Domain.Entities;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Payments.Commands;

public record SplitEntry(Guid? CustomerId, decimal Amount);

public record CreateSplitPaymentCommand(
    Guid OrderId,
    Domain.Enums.PaymentMethod Method,
    IReadOnlyList<SplitEntry> Splits) : ICommand<IReadOnlyList<SplitPaymentIntentResult>>;

public sealed class CreateSplitPaymentCommandValidator : AbstractValidator<CreateSplitPaymentCommand>
{
    public CreateSplitPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Splits).NotEmpty().Must(s => s.Count >= 2).WithMessage("Split payment requires at least 2 entries.");
        RuleForEach(x => x.Splits).ChildRules(s => s.RuleFor(e => e.Amount).GreaterThan(0));
    }
}

public sealed class CreateSplitPaymentCommandHandler
    : IRequestHandler<CreateSplitPaymentCommand, IReadOnlyList<SplitPaymentIntentResult>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeGateway _stripe;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSplitPaymentCommandHandler(
        IOrderRepository orderRepository,
        IRestaurantRepository restaurantRepository,
        IPaymentRepository paymentRepository,
        IStripeGateway stripe,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _restaurantRepository = restaurantRepository;
        _paymentRepository = paymentRepository;
        _stripe = stripe;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SplitPaymentIntentResult>> Handle(
        CreateSplitPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.Status is Domain.Enums.OrderStatus.Paid)
            throw new BusinessRuleException("Order is already paid.");
        if (order.Status is Domain.Enums.OrderStatus.Cancelled)
            throw new BusinessRuleException("Cannot pay a cancelled order.");

        var splitsTotal = request.Splits.Sum(s => s.Amount);
        if (Math.Abs(splitsTotal - order.Total.Amount) > 0.01m)
            throw new BusinessRuleException(
                $"Split amounts ({splitsTotal:F2}) do not match order total ({order.Total.Amount:F2}).");

        var restaurant = await _restaurantRepository.GetByIdAsync(order.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), order.RestaurantId);

        var currency = order.Total.Currency.ToLowerInvariant();
        var results = new List<SplitPaymentIntentResult>(request.Splits.Count);

        foreach (var split in request.Splits)
        {
            var amountCents = (long)(split.Amount * 100);
            var intentResult = await _stripe.CreatePaymentIntentAsync(
                amountCents, currency, restaurant.StripeAccountId, order.Id, cancellationToken);

            var splitMoney = new Money(split.Amount, order.Total.Currency);
            var payment = Payment.Create(
                order.TenantId,
                order.Id,
                splitMoney,
                request.Method,
                "Stripe",
                customerId: split.CustomerId,
                providerPaymentId: intentResult.PaymentIntentId);

            await _paymentRepository.AddAsync(payment, cancellationToken);

            results.Add(new SplitPaymentIntentResult(
                split.CustomerId,
                split.Amount,
                intentResult.ClientSecret,
                intentResult.PaymentIntentId,
                payment.Id));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return results;
    }
}
