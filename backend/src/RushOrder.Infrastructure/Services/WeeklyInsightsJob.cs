using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

// Every Monday at 09:00 UTC, emails each active restaurant's Owner a
// "Resumen de la semana": revenue vs last week, star product, a product to
// review, peak hour, best day, upcoming weekend reservations, and next
// weekend's demand forecast. Sent via SendGrid (IEmailSender).
public sealed class WeeklyInsightsJob : BackgroundService
{
    private const int RunHourUtc = 9;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyInsightsJob> _logger;
    private DateOnly? _lastRunDate;

    public WeeklyInsightsJob(IServiceScopeFactory scopeFactory, ILogger<WeeklyInsightsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                if (now.DayOfWeek == DayOfWeek.Monday && now.Hour == RunHourUtc && _lastRunDate != today)
                {
                    _lastRunDate = today;
                    await RunAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly insights cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWeeklyInsightsRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var todayUtc = DateTime.UtcNow.Date;
        var weekEnd = new DateTimeOffset(todayUtc, TimeSpan.Zero); // start of today (Monday) = end of last week
        var weekStart = weekEnd.AddDays(-7);
        var nextSaturday = DateOnly.FromDateTime(todayUtc.AddDays(5));
        var nextSunday = DateOnly.FromDateTime(todayUtc.AddDays(6));

        var owners = await repository.GetActiveRestaurantOwnersAsync(ct);
        _logger.LogInformation("Sending weekly insights to {Count} restaurant owner(s)", owners.Count);

        foreach (var owner in owners)
        {
            try
            {
                var insights = await repository.BuildInsightsAsync(
                    owner.TenantId, owner.RestaurantId, weekStart, weekEnd, nextSaturday, nextSunday, ct);

                var html = BuildEmailHtml(owner.RestaurantName, owner.OwnerName, insights);
                await emailSender.SendHtmlEmailAsync(
                    owner.OwnerEmail, $"Resumen de la semana — {owner.RestaurantName}", html, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Weekly insights failed for restaurant {RestaurantId}", owner.RestaurantId);
            }
        }
    }

    private static string BuildEmailHtml(string restaurantName, string ownerName, WeeklyInsightsDto d)
    {
        var revenueColor = d.RevenueChangePercent >= 0 ? "#16a34a" : "#dc2626";
        var revenueSign = d.RevenueChangePercent >= 0 ? "+" : "";
        var sb = new StringBuilder();

        sb.Append($$"""
            <!doctype html>
            <html lang="es">
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:24px 0;">
                <tr><td align="center">
                  <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;">
                    <tr><td style="background:#dc2626;padding:24px 32px;">
                      <h1 style="margin:0;color:#fff;font-size:20px;">Resumen de la semana</h1>
                      <p style="margin:4px 0 0;color:#fecaca;font-size:14px;">{{restaurantName}}</p>
                    </td></tr>
                    <tr><td style="padding:24px 32px;">
                      <p style="margin:0 0 16px;color:#374151;font-size:14px;">Hola {{ownerName}}, esto es lo que pasó la semana pasada:</p>

                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:20px;">
                        <tr>
                          <td style="padding:12px;background:#f9fafb;border-radius:12px;">
                            <p style="margin:0;color:#9ca3af;font-size:12px;text-transform:uppercase;">Ingresos totales</p>
                            <p style="margin:4px 0 0;color:#111827;font-size:22px;font-weight:700;">{{d.RevenueThisWeek.ToString("C", Es)}}</p>
                            <p style="margin:2px 0 0;color:{{revenueColor}};font-size:13px;font-weight:600;">{{revenueSign}}{{d.RevenueChangePercent}}% vs. semana anterior</p>
                          </td>
                        </tr>
                      </table>
            """);

        if (d.StarProductName is not null)
        {
            sb.Append($"""
                <p style="margin:0 0 4px;color:#111827;font-size:14px;"><strong>⭐ Producto estrella:</strong> {d.StarProductName} ({d.StarProductQuantity} unidades)</p>
                """);
        }

        if (d.ProductToReviewName is not null)
        {
            sb.Append($"""
                <p style="margin:0 0 4px;color:#111827;font-size:14px;"><strong>⚠️ Producto a revisar:</strong> {d.ProductToReviewName} — {d.ProductToReviewReason}</p>
                """);
        }

        if (d.PeakHour is not null)
        {
            sb.Append($"""
                <p style="margin:0 0 4px;color:#111827;font-size:14px;"><strong>🕐 Hora punta:</strong> {d.PeakHour:00}:00h</p>
                """);
        }

        if (d.BestDay is not null)
        {
            sb.Append($"""
                <p style="margin:0 0 4px;color:#111827;font-size:14px;"><strong>📈 Mejor día:</strong> {d.BestDay.Value.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM", Es)} ({d.BestDayRevenue.ToString("C", Es)})</p>
                """);
        }

        sb.Append($"""
            <p style="margin:16px 0 4px;color:#111827;font-size:14px;"><strong>📅 Este finde:</strong> {d.WeekendReservations} reserva(s) confirmada(s)</p>
            """);

        if (d.WeekendForecastTopProducts.Count > 0)
        {
            sb.Append($"""
                <p style="margin:0 0 4px;color:#111827;font-size:14px;"><strong>🔮 Previsión del finde:</strong> {string.Join(", ", d.WeekendForecastTopProducts)}</p>
                """);
        }

        sb.Append("""
                    </td></tr>
                    <tr><td style="padding:16px 32px;background:#f9fafb;">
                      <p style="margin:0;color:#9ca3af;font-size:12px;">Rush Order — resumen automático semanal</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }
}
