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
