using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Settings;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace RushOrder.Infrastructure.Services;

public sealed class SendGridEmailService : IEmailSender
{
    private readonly SendGridSettings _settings;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(IOptions<SendGridSettings> settings, ILogger<SendGridEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("SendGrid:ApiKey is not configured — skipping email to {ToEmail} ({Subject})", toEmail, subject);
            return;
        }

        try
        {
            var client = new SendGridClient(_settings.ApiKey);
            var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
            var to = new EmailAddress(toEmail);
            var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: htmlBody);

            var response = await client.SendEmailAsync(message, cancellationToken);
            if ((int)response.StatusCode >= 300)
            {
                var body = await response.Body.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "SendGrid returned {StatusCode} sending to {ToEmail}: {Body}", response.StatusCode, toEmail, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SendGrid to {ToEmail} ({Subject})", toEmail, subject);
        }
    }
}
