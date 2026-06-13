using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendReservationConfirmationAsync(Reservation reservation, CancellationToken cancellationToken = default);
    Task SendOrderStatusUpdateAsync(Order order, Customer? customer, CancellationToken cancellationToken = default);
    Task SendLowStockAlertAsync(Product product, int currentStock, CancellationToken cancellationToken = default);
}
