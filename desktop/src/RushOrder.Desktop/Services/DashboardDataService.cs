using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class DashboardDataService
{
    private readonly AppState _state;
    private readonly ILogger<DashboardDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public DashboardDataService(AppState state, ILogger<DashboardDataService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    // Backend's DashboardDto has no hourly revenue breakdown (only Today/Yesterday
    // totals) and TableOccupancy is a percentage, not raw counts — TablesTotal is
    // fetched separately from /tables to derive TablesOccupied.
    public async Task<DashboardKpi> GetKpiAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var dto  = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data;
                if (dto is not null) return await MapKpiAsync(dto, restaurantId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch KPI from API; using mock data");
        }
        return MockKpi();
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
        catch { /* leave at 0 */ }

        var occupied = (int)Math.Round(dto.TableOccupancy.Percentage / 100m * tablesTotal);

        // AvgTicket only carries today's value + %-change — back out yesterday's
        // value from that (guarded against a -100% change producing a division by 0).
        var changeFactor = 1m + dto.AvgTicket.ChangePercent / 100m;
        var avgTicketYesterday = changeFactor != 0 ? dto.AvgTicket.Value / changeFactor : dto.AvgTicket.Value;

        return new DashboardKpi(
            dto.Revenue.Today, dto.Revenue.Yesterday, [], // no hourly breakdown from backend
            dto.Orders.Pending, dto.Orders.InProgress, dto.Orders.Completed,
            occupied, tablesTotal, dto.TableOccupancy.AvgDuration,
            dto.AvgTicket.Value, avgTicketYesterday);
    }

    // Backend has no dedicated alerts endpoint — ActiveAlerts is embedded in the
    // dashboard response, so this re-fetches it (accepted duplicate call; dashboard
    // and alerts are refreshed independently by the UI).
    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var dto  = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data;
                if (dto is not null)
                    // Backend alerts carry no Id/CreatedAt — synthesized here.
                    return dto.ActiveAlerts.Select(a => new AlertDto(
                        Guid.NewGuid(), a.Message,
                        Enum.TryParse<AlertSeverity>(a.Severity, out var sev) ? sev : AlertSeverity.Info,
                        null, a.Type, DateTimeOffset.Now)).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch alerts; using mock data");
        }
        return MockAlerts();
    }

    public async Task<IReadOnlyList<ReservationDto>> GetUpcomingReservationsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var today = DateTimeOffset.Now.Date;
            var url = $"http://localhost:5143/api/v1/reservations?restaurantId={restaurantId}&date={Uri.EscapeDataString(today.ToString("O"))}";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var rows = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendReservationDto>>>(json)?.Data;
                if (rows is not null)
                    return rows
                        .Where(r => r.ReservedAt >= DateTimeOffset.Now && r.Status != "Cancelled")
                        .OrderBy(r => r.ReservedAt)
                        .Take(3)
                        .Select(r => new ReservationDto(r.Id, r.GuestName, r.PartySize, r.ReservedAt, r.Notes))
                        .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch reservations; using mock data");
        }
        return MockReservations();
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;
    }

    // ── Mock data ──────────────────────────────────────────────────────────

    private static DashboardKpi MockKpi() => new(
        RevenueToday:       1247.50m,
        RevenueYesterday:   1111.20m,
        RevenueByHour:      [0m, 0m, 0m, 82m, 156m, 312m, 423m, 274m],
        OrdersWaiting:      3,
        OrdersPreparing:    5,
        OrdersReady:        2,
        TablesOccupied:     8,
        TablesTotal:        12,
        AvgOccupancyMinutes: 42.5,
        AvgTicketToday:     34.20m,
        AvgTicketYesterday: 33.40m);

    private static IReadOnlyList<AlertDto> MockAlerts() =>
    [
        new(Guid.NewGuid(), "Stock bajo: Vino Rioja Reserva (3 botellas)", AlertSeverity.Warning,
            null, "Product", DateTimeOffset.Now.AddMinutes(-14)),
        new(Guid.NewGuid(), "Cocina: Pedido #A-047 lleva +25 min en preparación", AlertSeverity.Critical,
            "A-047", "Order", DateTimeOffset.Now.AddMinutes(-7)),
        new(Guid.NewGuid(), "Reserva en 30 min — Mesa 4, García Martínez, 6 personas", AlertSeverity.Info,
            null, "Reservation", DateTimeOffset.Now.AddMinutes(-2)),
    ];

    private static IReadOnlyList<ReservationDto> MockReservations() =>
    [
        new(Guid.NewGuid(), "García Martínez", 6, DateTimeOffset.Now.AddMinutes(30),  "Cumpleaños"),
        new(Guid.NewGuid(), "Fernández López", 2, DateTimeOffset.Now.AddHours(1),     null),
        new(Guid.NewGuid(), "Rodriguez & Co",  8, DateTimeOffset.Now.AddHours(1).AddMinutes(30), "Menú empresarial"),
    ];
}

// Matches backend's DashboardDto (Analytics/DTOs/DashboardDto.cs).
internal sealed record BackendDashboardDto(
    BackendRevenueStat Revenue,
    BackendOrderStat Orders,
    BackendCoverStat Covers,
    BackendTicketStat AvgTicket,
    IReadOnlyList<object> TopProducts,
    BackendTableOccupancyStat TableOccupancy,
    IReadOnlyList<BackendActiveAlertDto> ActiveAlerts);

internal sealed record BackendRevenueStat(decimal Today, decimal Yesterday, decimal ChangePercent);
internal sealed record BackendOrderStat(int Total, int Pending, int InProgress, int Completed);
internal sealed record BackendCoverStat(int Total, decimal AvgPerTable);
internal sealed record BackendTicketStat(decimal Value, decimal ChangePercent);
internal sealed record BackendTableOccupancyStat(decimal Percentage, double AvgDuration);
internal sealed record BackendActiveAlertDto(string Type, string Message, string Severity);

// Matches backend's ReservationDto (Reservations/DTOs/ReservationDto.cs) — only the
// fields this service needs.
internal sealed record BackendReservationDto(
    Guid Id, string GuestName, int PartySize, DateTimeOffset ReservedAt,
    string Status, string? Notes);
