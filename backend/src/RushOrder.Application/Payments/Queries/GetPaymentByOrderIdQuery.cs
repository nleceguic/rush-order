using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Payments.DTOs;

namespace RushOrder.Application.Payments.Queries;

public record GetPaymentByOrderIdQuery(Guid OrderId) : IQuery<PaymentDto?>;

public sealed class GetPaymentByOrderIdQueryHandler
    : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentByOrderIdQueryHandler(IPaymentRepository paymentRepository)
        => _paymentRepository = paymentRepository;

    public async Task<PaymentDto?> Handle(
        GetPaymentByOrderIdQuery request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (payment is null) return null;

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
