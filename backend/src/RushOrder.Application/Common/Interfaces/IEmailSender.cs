namespace RushOrder.Application.Common.Interfaces;

// Raw HTML email sending via SendGrid — separate from INotificationService
// (SMTP, transactional emails like password resets/receipts) because the
// weekly insights email is explicitly specced to go through SendGrid.
public interface IEmailSender
{
    Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
