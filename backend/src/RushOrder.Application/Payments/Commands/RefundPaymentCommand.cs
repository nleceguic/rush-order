using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Payments.Commands;

public record RefundPaymentCommand(
    Guid PaymentId,
    decimal? Amount = null,
    string Reason = "requested_by_customer") : ICommand<PaymentDto>;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
    }
}

public sealed class RefundPaymentCommandHandler
    : IRequestHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeGateway _stripe;
    private readonly IUnitOfWork _unitOfWork;

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IStripeGateway stripe,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _stripe = stripe;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymentDto> Handle(
        RefundPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), request.PaymentId);

        if (payment.ProviderPaymentId is null)
            throw new BusinessRuleException("Payment has no associated provider payment ID.");

        var amountCents = request.Amount.HasValue
            ? (long)(request.Amount.Value * 100)
            : (long?)null;

        await _stripe.CreateRefundAsync(payment.ProviderPaymentId, amountCents, request.Reason, cancellationToken);

        payment.Refund();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PaymentDto(
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
            payment.CreatedAt);
    }
}
