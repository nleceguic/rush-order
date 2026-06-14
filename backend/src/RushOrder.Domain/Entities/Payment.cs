using RushOrder.Domain.Common;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Domain.Entities;

public sealed class Payment : TenantEntity
{
    public Guid OrderId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string? ProviderPaymentId { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? ReceiptUrl { get; private set; }
    public string? FailureReason { get; private set; }

    private Payment() { } // EF Core

    private Payment(
        Guid tenantId,
        Guid orderId,
        Guid? customerId,
        Money amount,
        PaymentMethod method,
        string provider) : base(tenantId)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Method = method;
        Provider = provider;
    }

    public static Payment Create(
        Guid tenantId,
        Guid orderId,
        Money amount,
        PaymentMethod method,
        string provider,
        Guid? customerId = null,
        string? providerPaymentId = null)
    {
        var payment = new Payment(tenantId, orderId, customerId, amount, method, provider);
        payment.ProviderPaymentId = providerPaymentId;
        return payment;
    }

    public void Complete(string providerPaymentId, string? receiptUrl = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot complete a payment in status {Status}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);

        Status = PaymentStatus.Completed;
        ProviderPaymentId ??= providerPaymentId;
        ReceiptUrl = receiptUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot fail a payment in status {Status}.");
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Only completed payments can be refunded.");
        Status = PaymentStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
