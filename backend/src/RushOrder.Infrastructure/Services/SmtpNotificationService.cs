using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Infrastructure.Settings;

namespace RushOrder.Infrastructure.Services;

public sealed class SmtpNotificationService : INotificationService
{
    private readonly SmtpSettings _settings;

    public SmtpNotificationService(IOptions<SmtpSettings> options)
        => _settings = options.Value;

    public async Task SendReservationConfirmationAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        if (reservation.GuestEmail is null) return;

        var message = BuildMessage(
            toName: reservation.GuestName,
            toAddress: reservation.GuestEmail,
            subject: $"Reserva confirmada – Código: {reservation.ConfirmationCode}",
            htmlBody: $"""
                <h2>Reserva confirmada</h2>
                <p>Hola <strong>{reservation.GuestName}</strong>,</p>
                <p>Tu reserva ha sido confirmada.</p>
                <ul>
                  <li><strong>Código:</strong> {reservation.ConfirmationCode}</li>
                  <li><strong>Fecha:</strong> {reservation.ReservedAt:dd/MM/yyyy HH:mm}</li>
                  <li><strong>Personas:</strong> {reservation.PartySize}</li>
                </ul>
                """);

        await SendAsync(message, cancellationToken);
    }

    public async Task SendOrderStatusUpdateAsync(Order order, Customer? customer, CancellationToken cancellationToken = default)
    {
        if (customer?.Email is null) return;

        var message = BuildMessage(
            toName: customer.DisplayName,
            toAddress: customer.Email.Value,
            subject: $"Pedido {order.OrderNumber} – {order.Status}",
            htmlBody: $"""
                <h2>Actualización de pedido</h2>
                <p>Tu pedido <strong>{order.OrderNumber}</strong> está ahora en estado: <strong>{order.Status}</strong>.</p>
                """);

        await SendAsync(message, cancellationToken);
    }

    public async Task SendLowStockAlertAsync(Product product, int currentStock, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: "Admin",
            toAddress: _settings.AdminEmail,
            subject: $"[Alerta] Stock bajo: {product.Name}",
            htmlBody: $"""
                <h2>Alerta de stock bajo</h2>
                <p>El producto <strong>{product.Name}</strong> tiene stock bajo.</p>
                <p>Stock actual: <strong>{currentStock}</strong> unidades.</p>
                """);

        await SendAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: email,
            toAddress: email,
            subject: "Restablecer contraseña – Rush Order",
            htmlBody: $"""
                <h2>Restablecer contraseña</h2>
                <p>Haz clic en el siguiente enlace para restablecer tu contraseña.</p>
                <p>El enlace es válido durante 1 hora.</p>
                <p><a href="{resetLink}">{resetLink}</a></p>
                """);
        await SendAsync(message, cancellationToken);
    }

    public async Task SendPaymentReceiptAsync(string email, string orderNumber, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(email, email));
        message.Subject = $"Recibo de pago – Pedido {orderNumber}";

        var builder = new BodyBuilder
        {
            HtmlBody = $"""
                <h2>Gracias por tu pedido</h2>
                <p>Adjuntamos el recibo de tu pedido <strong>{orderNumber}</strong>.</p>
                """
        };
        builder.Attachments.Add($"recibo-{orderNumber}.pdf", pdfBytes, new MimeKit.ContentType("application", "pdf"));
        message.Body = builder.ToMessageBody();

        await SendAsync(message, cancellationToken);
    }

    public async Task SendPaymentFailedAsync(string email, string orderNumber, string reason, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: email,
            toAddress: email,
            subject: $"Pago fallido – Pedido {orderNumber}",
            htmlBody: $"""
                <h2>Pago no procesado</h2>
                <p>El pago de tu pedido <strong>{orderNumber}</strong> no ha podido procesarse.</p>
                <p>Motivo: <strong>{reason}</strong></p>
                <p>Por favor, inténtalo de nuevo o contacta con el restaurante.</p>
                """);
        await SendAsync(message, cancellationToken);
    }

    public async Task SendDisputeAlertAsync(string ownerEmail, string orderNumber, string disputeId, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: "Propietario",
            toAddress: ownerEmail,
            subject: $"[Alerta] Disputa de pago – Pedido {orderNumber}",
            htmlBody: $"""
                <h2>Nueva disputa de pago</h2>
                <p>Se ha abierto una disputa para el pedido <strong>{orderNumber}</strong>.</p>
                <p>ID de disputa: <code>{disputeId}</code></p>
                <p>Por favor, revisa tu panel de Stripe para gestionar la disputa.</p>
                """);
        await SendAsync(message, cancellationToken);
    }

    public async Task SendWelcomeEmailAsync(string email, string ownerName, string loginUrl, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: ownerName,
            toAddress: email,
            subject: "¡Bienvenido a Rush Order! Tu prueba gratuita ha comenzado",
            htmlBody: $"""
                <h2>¡Bienvenido a Rush Order, {ownerName}!</h2>
                <p>Tu cuenta está lista. Tienes <strong>30 días de prueba gratuita</strong>.</p>
                <p><a href="{loginUrl}" style="background:#2563EB;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none">Acceder ahora</a></p>
                <p>Si tienes alguna duda, escríbenos a soporte@rushorder.io</p>
                """);
        await SendAsync(message, cancellationToken);
    }

    public async Task SendSubscriptionPaymentDueAsync(string ownerEmail, string planName, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: ownerEmail,
            toAddress: ownerEmail,
            subject: "[Rush Order] Pago pendiente – acción requerida",
            htmlBody: $"""
                <h2>Pago pendiente en tu suscripción</h2>
                <p>No hemos podido procesar el pago de tu plan <strong>{planName}</strong>.</p>
                <p>Por favor, actualiza tu método de pago para evitar la suspensión del servicio.</p>
                <p><a href="https://app.rushorder.io/subscriptions">Actualizar método de pago</a></p>
                """);
        await SendAsync(message, cancellationToken);
    }

    public async Task SendSubscriptionSuspendedAsync(string ownerEmail, string tenantName, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(
            toName: ownerEmail,
            toAddress: ownerEmail,
            subject: "[Rush Order] Tu cuenta ha sido suspendida",
            htmlBody: $"""
                <h2>Cuenta suspendida: {tenantName}</h2>
                <p>Tu cuenta ha sido suspendida por falta de pago.</p>
                <p>Para reactivarla, contacta con soporte en soporte@rushorder.io o actualiza tu plan.</p>
                <p><a href="https://app.rushorder.io/subscriptions">Ver opciones de suscripción</a></p>
                """);
        await SendAsync(message, cancellationToken);
    }

    private MimeMessage BuildMessage(string toName, string toAddress, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };
        return message;
    }

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        var socketOptions = _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);
        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
