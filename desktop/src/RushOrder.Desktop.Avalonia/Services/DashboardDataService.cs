using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class DashboardDataService
{
    private readonly AppState _state;
    private readonly ILogger<DashboardDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public DashboardDataService(AppState state, ILogger<DashboardDataService> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<Result<DashboardKpi>> GetKpiAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty dashboard payload");

            return Result<DashboardKpi>.Ok(await MapKpiAsync(dto, restaurantId, ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard KPI fetch failed");
            return Result<DashboardKpi>.Fail(ex);
        }
    }

    private async Task<DashboardKpi> MapKpiAsync(BackendDashboardDto dto, Guid? restaurantId, CancellationToken ct)
    {
        var tablesTotal = 0;
        try
        {
            var tRes = await _http.GetAsync($"http://localhost:5143/api/v1/tables?restaurantId={restaurantId}", ct);
            if (tRes.IsSuccessStatusCode)
            {
                var tJson = await tRes.Content.ReadAsStringAsync(ct);
                tablesTotal = JsonConvert.DeserializeObject<ApiEnvelope<List<object>>>(tJson)?.Data?.Count ?? 0;
            }
        }
        catch { /* leave at 0 — matches WinForms behavior, this sub-call isn't the primary fetch */ }

        var occupied = (int)Math.Round(dto.TableOccupancy.Percentage / 100m * tablesTotal);
        var changeFactor = 1m + dto.AvgTicket.ChangePercent / 100m;
        var avgTicketYesterday = changeFactor != 0 ? dto.AvgTicket.Value / changeFactor : dto.AvgTicket.Value;

        return new DashboardKpi(
            dto.Revenue.Today, dto.Revenue.Yesterday, [],
            dto.Orders.Pending, dto.Orders.InProgress, dto.Orders.Completed,
            occupied, tablesTotal, dto.TableOccupancy.AvgDuration,
            dto.AvgTicket.Value, avgTicketYesterday);
    }

    public async Task<Result<IReadOnlyList<AlertDto>>> GetAlertsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty dashboard payload");

            IReadOnlyList<AlertDto> alerts = dto.ActiveAlerts.Select(a => new AlertDto(
                Guid.NewGuid(), a.Message,
                Enum.TryParse<AlertSeverity>(a.Severity, out var sev) ? sev : AlertSeverity.Info,
                null, a.Type, DateTimeOffset.Now)).ToList();

            return Result<IReadOnlyList<AlertDto>>.Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alerts fetch failed");
            return Result<IReadOnlyList<AlertDto>>.Fail(ex);
        }
    }

    public async Task<Result<IReadOnlyList<ReservationDto>>> GetUpcomingReservationsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var today = DateTimeOffset.Now.Date;
            var url = $"http://localhost:5143/api/v1/reservations?restaurantId={restaurantId}&date={Uri.EscapeDataString(today.ToString("O"))}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var rows = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendReservationDto>>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty reservations payload");

            IReadOnlyList<ReservationDto> reservations = rows
                .Where(r => r.ReservedAt >= DateTimeOffset.Now && r.Status != "Cancelled")
                .OrderBy(r => r.ReservedAt)
                .Take(3)
                .Select(r => new ReservationDto(r.Id, r.GuestName, r.PartySize, r.ReservedAt, r.Notes))
                .ToList();

            return Result<IReadOnlyList<ReservationDto>>.Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reservations fetch failed");
            return Result<IReadOnlyList<ReservationDto>>.Fail(ex);
        }
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;
    }
}

internal sealed record BackendDashboardDto(
    BackendRevenueStat Revenue, BackendOrderStat Orders, BackendCoverStat Covers, BackendTicketStat AvgTicket,
    IReadOnlyList<object> TopProducts, BackendTableOccupancyStat TableOccupancy,
    IReadOnlyList<BackendActiveAlertDto> ActiveAlerts);

internal sealed record BackendRevenueStat(decimal Today, decimal Yesterday, decimal ChangePercent);
internal sealed record BackendOrderStat(int Total, int Pending, int InProgress, int Completed);
internal sealed record BackendCoverStat(int Total, decimal AvgPerTable);
internal sealed record BackendTicketStat(decimal Value, decimal ChangePercent);
internal sealed record BackendTableOccupancyStat(decimal Percentage, double AvgDuration);
internal sealed record BackendActiveAlertDto(string Type, string Message, string Severity);
internal sealed record BackendReservationDto(Guid Id, string GuestName, int PartySize, DateTimeOffset ReservedAt, string Status, string? Notes);
internal sealed class ApiEnvelope<T> { public string Status { get; set; } = ""; public T? Data { get; set; } }
