using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendReservationConfirmationAsync(Reservation reservation, CancellationToken cancellationToken = default);
    Task SendOrderStatusUpdateAsync(Order order, Customer? customer, CancellationToken cancellationToken = default);
    Task SendLowStockAlertAsync(Product product, int currentStock, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken = default);
    Task SendPaymentReceiptAsync(string email, string orderNumber, byte[] pdfBytes, CancellationToken cancellationToken = default);
    Task SendPaymentFailedAsync(string email, string orderNumber, string reason, CancellationToken cancellationToken = default);
    Task SendDisputeAlertAsync(string ownerEmail, string orderNumber, string disputeId, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string email, string ownerName, string loginUrl, CancellationToken cancellationToken = default);
    Task SendSubscriptionPaymentDueAsync(string ownerEmail, string planName, CancellationToken cancellationToken = default);
    Task SendSubscriptionSuspendedAsync(string ownerEmail, string tenantName, CancellationToken cancellationToken = default);
}
