using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.DTOs;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Payments.Commands;

public record CreatePaymentIntentCommand(
    Guid OrderId,
    PaymentMethod Method,
    Guid? CustomerId = null) : ICommand<CreatePaymentIntentResult>;

public sealed class CreatePaymentIntentCommandValidator : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentCommandValidator() => RuleFor(x => x.OrderId).NotEmpty();
}

public sealed class CreatePaymentIntentCommandHandler
    : IRequestHandler<CreatePaymentIntentCommand, CreatePaymentIntentResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeGateway _stripe;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePaymentIntentCommandHandler(
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

    public async Task<CreatePaymentIntentResult> Handle(
        CreatePaymentIntentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.Status is Domain.Enums.OrderStatus.Paid)
            throw new BusinessRuleException("Order is already paid.");
        if (order.Status is Domain.Enums.OrderStatus.Cancelled)
            throw new BusinessRuleException("Cannot pay a cancelled order.");

        var restaurant = await _restaurantRepository.GetByIdAsync(order.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), order.RestaurantId);

        var amountCents = (long)(order.Total.Amount * 100);
        var currency = order.Total.Currency.ToLowerInvariant();

        var intentResult = await _stripe.CreatePaymentIntentAsync(
            amountCents, currency, restaurant.StripeAccountId, order.Id, cancellationToken);

        var payment = Payment.Create(
            order.TenantId,
            order.Id,
            order.Total,
            request.Method,
            "Stripe",
            customerId: request.CustomerId ?? order.CustomerId,
            providerPaymentId: intentResult.PaymentIntentId);

        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatePaymentIntentResult(intentResult.ClientSecret, intentResult.PaymentIntentId, payment.Id);
    }
}
