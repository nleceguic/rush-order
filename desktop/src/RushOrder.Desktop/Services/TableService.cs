using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class TableService
{
    private readonly AppState _state;
    private readonly ILogger<TableService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public TableService(AppState state, ILogger<TableService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TableDto>> GetTablesAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var id  = _state.CurrentRestaurant?.Id;
            var res = await _http.GetAsync($"http://localhost:5000/api/tables?restaurantId={id}", ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<TableDto>>(json)!;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch tables; using mock"); }
        return MockTables();
    }

    public async Task<IReadOnlyList<ActiveOrderSummary>> GetTableOrdersAsync(
        Guid tableId, CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var res = await _http.GetAsync(
                $"http://localhost:5000/api/orders?tableId={tableId}&status=active", ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<ActiveOrderSummary>>(json)!;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch table orders"); }
        return MockOrders();
    }

    public async Task SavePositionsAsync(IEnumerable<SavePositionRequest> positions, CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            foreach (var req in positions)
            {
                var body = JsonConvert.SerializeObject(new { req.X, req.Y });
                await _http.PutAsync(
                    $"http://localhost:5000/api/tables/{req.Id}/position",
                    new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not save table positions"); }
    }

    public async Task UpdateTableStateAsync(Guid tableId, TableState state, CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(new { state = state.ToString() });
            await _http.PutAsync(
                $"http://localhost:5000/api/tables/{tableId}/state",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not update table state"); }
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;

    // ── Mock layout: typical restaurant grid ─────────────────────────────
    private static readonly Guid[] _mockIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();

    public static IReadOnlyList<TableDto> MockTables()
    {
        var now = DateTimeOffset.Now;
        return
        [
            new(_mockIds[0],  1, 2, TableState.Occupied,  TableShapeType.Circular,    40,  40,  80, 80, true,  now.AddMinutes(-55), "Ana"),
            new(_mockIds[1],  2, 2, TableState.Free,       TableShapeType.Circular,   140,  40,  80, 80, false, null,                null),
            new(_mockIds[2],  3, 4, TableState.Occupied,  TableShapeType.Circular,    280,  40, 100,100, false, now.AddMinutes(-20), "Carlos"),
            new(_mockIds[3],  4, 6, TableState.Reserved,  TableShapeType.Rectangular, 420,  40, 140, 90, false, null,                null),
            new(_mockIds[4],  5, 4, TableState.Occupied,  TableShapeType.Circular,    40,  190, 100,100, true,  now.AddMinutes(-40), "Ana"),
            new(_mockIds[5],  6, 4, TableState.Free,       TableShapeType.Circular,   170,  190, 100,100, false, null,               null),
            new(_mockIds[6],  7, 8, TableState.Occupied,  TableShapeType.Rectangular, 310,  190, 160,100, true,  now.AddMinutes(-75), "Carlos"),
            new(_mockIds[7],  8, 4, TableState.Cleaning,  TableShapeType.Circular,    40,  350, 100,100, false, null,                null),
            new(_mockIds[8],  9, 2, TableState.Occupied,  TableShapeType.Circular,   170,  350,  80, 80, false, now.AddMinutes(-12), "María"),
            new(_mockIds[9], 10, 6, TableState.Free,       TableShapeType.Rectangular, 280, 350, 140, 90, false, null,               null),
            new(_mockIds[10],11, 4, TableState.Occupied,  TableShapeType.Circular,    450, 190, 100,100, false, now.AddMinutes(-30), "María"),
            new(_mockIds[11],12, 8, TableState.Reserved,  TableShapeType.Rectangular, 40,  500, 160, 90, false, null,                null),
        ];
    }

    private static IReadOnlyList<ActiveOrderSummary> MockOrders() =>
    [
        new(Guid.NewGuid(), "A-041", 3, 52.40m,  "Preparing", DateTimeOffset.Now.AddMinutes(-18)),
        new(Guid.NewGuid(), "A-042", 2, 28.00m,  "Ready",     DateTimeOffset.Now.AddMinutes(-5)),
    ];
}
