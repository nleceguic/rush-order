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

    public async Task<DashboardKpi> GetKpiAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5000/api/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<DashboardKpi>(json)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch KPI from API; using mock data");
        }
        return MockKpi();
    }

    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5000/api/alerts?restaurantId={restaurantId}&active=true";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<AlertDto>>(json)!;
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
            var url = $"http://localhost:5000/api/reservations/upcoming?restaurantId={restaurantId}&limit=3";
            var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<ReservationDto>>(json)!;
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
