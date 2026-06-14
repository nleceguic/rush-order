using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.API.IntegrationTests.Infrastructure;

/// <summary>No-op notification service — discards all emails in test environment.</summary>
public sealed class NullNotificationService : INotificationService
{
    public Task SendReservationConfirmationAsync(Reservation reservation, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendOrderStatusUpdateAsync(Order order, Customer? customer, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendLowStockAlertAsync(Product product, int currentStock, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPaymentReceiptAsync(string email, string orderNumber, byte[] pdfBytes, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPaymentFailedAsync(string email, string orderNumber, string reason, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendDisputeAlertAsync(string ownerEmail, string orderNumber, string disputeId, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendWelcomeEmailAsync(string email, string ownerName, string loginUrl, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendSubscriptionPaymentDueAsync(string ownerEmail, string planName, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendSubscriptionSuspendedAsync(string ownerEmail, string tenantName, CancellationToken ct = default) => Task.CompletedTask;
}
