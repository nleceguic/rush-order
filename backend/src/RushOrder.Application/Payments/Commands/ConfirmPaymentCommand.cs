using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Payments.Commands;

public record ConfirmPaymentCommand(string PaymentIntentId) : ICommand<ConfirmPaymentResult>;

public sealed class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
        => RuleFor(x => x.PaymentIntentId).NotEmpty();
}

public sealed class ConfirmPaymentCommandHandler
    : IRequestHandler<ConfirmPaymentCommand, ConfirmPaymentResult>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IStripeGateway _stripe;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository,
        IRestaurantRepository restaurantRepository,
        ICustomerRepository customerRepository,
        IStripeGateway stripe,
        IPdfReceiptService pdfReceiptService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _restaurantRepository = restaurantRepository;
        _customerRepository = customerRepository;
        _stripe = stripe;
        _pdfReceiptService = pdfReceiptService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfirmPaymentResult> Handle(
        ConfirmPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByProviderPaymentIdAsync(request.PaymentIntentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found for PaymentIntent", request.PaymentIntentId);

        if (payment.Status == Domain.Enums.PaymentStatus.Completed)
            return MapResult(payment);

        var stripeStatus = await _stripe.GetPaymentIntentStatusAsync(request.PaymentIntentId, cancellationToken);
        if (stripeStatus != "succeeded")
            throw new BusinessRuleException($"Payment intent is not succeeded (status: {stripeStatus}).");

        payment.Complete(request.PaymentIntentId);

        var order = await _orderRepository.GetByIdAsync(payment.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), payment.OrderId);

        order.Pay();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = SendReceiptAsync(payment, order, cancellationToken);

        return MapResult(payment);
    }

    private async Task SendReceiptAsync(Payment payment, Order order, CancellationToken ct)
    {
        try
        {
            Customer? customer = payment.CustomerId.HasValue
                ? await _customerRepository.GetByIdAsync(payment.CustomerId.Value, ct)
                : null;

            if (customer?.Email is null) return;

            var restaurant = await _restaurantRepository.GetByIdAsync(order.RestaurantId, ct);
            if (restaurant is null) return;

            var receiptData = new ReceiptData(
                PaymentId: payment.Id,
                OrderNumber: order.OrderNumber,
                PaidAt: payment.UpdatedAt,
                RestaurantName: restaurant.Name,
                RestaurantAddress: restaurant.Address,
                RestaurantPhone: restaurant.Phone,
                RestaurantEmail: restaurant.Email,
                Items: order.Items.Select(i => new ReceiptLineItem(
                    i.Name, i.Quantity, i.UnitPrice.Amount, i.LineTotal.Amount, i.LineTotal.Currency)).ToList(),
                Subtotal: order.Subtotal.Amount,
                TaxAmount: order.TaxAmount.Amount,
                TaxRate: order.TaxRate,
                DiscountAmount: order.DiscountAmount.Amount,
                TipAmount: order.TipAmount.Amount,
                Total: order.Total.Amount,
                Currency: order.Total.Currency);

            var pdfBytes = _pdfReceiptService.GenerateReceipt(receiptData);
            await _notificationService.SendPaymentReceiptAsync(customer.Email.Value, order.OrderNumber, pdfBytes, ct);
        }
        catch
        {
            // Fire-and-forget: receipt failure must not roll back the payment
        }
    }

    private static ConfirmPaymentResult MapResult(Payment payment) => new(
        Payment: new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount.Amount,
            payment.Amount.Currency,
            payment.Method.ToString(),
            payment.Provider,
            payment.ProviderPaymentId,
            payment.Status.ToString(),
            payment.ReceiptUrl,
            payment.FailureReason,
            payment.CreatedAt),
        ReceiptUrl: payment.ReceiptUrl);
}
