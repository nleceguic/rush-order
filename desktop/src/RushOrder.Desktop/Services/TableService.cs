using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class TableService
{
    private const string BaseUrl = "http://localhost:5143/api/v1/tables";

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
            // /floorplan (not the plain list) is the endpoint that returns position +
            // active-order info; the backend still has no Width/Height/ShapeType/
            // OccupiedSince/CurrentWaiter storage at all, so those are defaulted below.
            var res = await _http.GetAsync($"{BaseUrl}/floorplan?restaurantId={id}", ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                var rows = JsonConvert.DeserializeObject<ApiEnvelope<List<TableFloorPlanDto>>>(json)?.Data;
                if (rows is not null)
                    return rows.Select((t, i) => MapFloorPlan(t, i)).ToList();
            }
            else
            {
                // A non-success response doesn't throw, so it silently fell through to mock
                // with no trace at all before — e.g. every call is a 401 while there's no
                // logged-in session (no AccessToken/CurrentRestaurant set), which otherwise
                // looks indistinguishable from the backend just being unreachable.
                _logger.LogWarning(
                    "Fetching tables returned {StatusCode}; using mock", res.StatusCode);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch tables; using mock"); }
        return MockTables();
    }

    private static TableDto MapFloorPlan(TableFloorPlanDto t, int index)
    {
        var state = Enum.TryParse<TableState>(t.Status, out var s) ? s : TableState.Free;
        // Fallback grid position for tables the backend never got a saved layout for.
        var x = (float)(t.PositionX ?? (index % 4) * 140);
        var y = (float)(t.PositionY ?? (index / 4) * 150);

        return new TableDto(
            t.Id, ParseTableNumber(t.Name), t.Capacity, state, TableShapeType.Circular,
            x, y, 80, 80, t.ActiveOrderCount > 0, null, null);
    }

    // Backend tables are named "Mesa N" (see DatabaseSeeder); desktop models the
    // number, not the name. Falls back to 0 for non-numeric names ("Barra", etc.).
    private static int ParseTableNumber(string name)
    {
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    // NOTE: OrdersController has no tableId filter — this always falls back to mock.
    // Would need a new backend query capability to work for real.
    public async Task<IReadOnlyList<ActiveOrderSummary>> GetTableOrdersAsync(
        Guid tableId, CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var res = await _http.GetAsync(
                $"http://localhost:5143/api/v1/orders?tableId={tableId}&status=active", ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<ActiveOrderSummary>>(json)!;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch table orders"); }
        return MockOrders();
    }

    // Was try/catch-and-swallow around the whole loop — a failed PUT (401 with no session,
    // 404, 500, ...) never threw, so FloorPlanView's own try/catch always took the "success"
    // path and told the user the layout saved even when nothing actually persisted. Now each
    // response's status is checked, and any failure propagates so the caller's error toast is
    // accurate.
    public async Task SavePositionsAsync(IEnumerable<SavePositionRequest> positions, CancellationToken ct = default)
    {
        SetAuth();
        var failedIds = new List<Guid>();

        foreach (var req in positions)
        {
            // Backend's PUT {id} is a general table-update endpoint (Name/Capacity/
            // Zone/PositionX/PositionY, all optional) — no dedicated /position route.
            var body = JsonConvert.SerializeObject(new { positionX = (double)req.X, positionY = (double)req.Y });
            var response = await _http.PutAsync(
                $"{BaseUrl}/{req.Id}",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Saving position for table {TableId} failed with {StatusCode}",
                    req.Id, response.StatusCode);
                failedIds.Add(req.Id);
            }
        }

        if (failedIds.Count > 0)
            throw new InvalidOperationException(
                $"No se pudo guardar la posición de {failedIds.Count} mesa(s): {string.Join(", ", failedIds)}");
    }

    // NOTE: backend's UpdateTableRequest has no status/state field at all — table
    // occupancy state isn't independently settable via this controller today. This
    // call will always no-op against the real API (204 from a body that changes
    // nothing bound, or a bad-request depending on model binding). Needs a new
    // backend capability (or deriving state from active orders) to work for real.
    public async Task UpdateTableStateAsync(Guid tableId, TableState state, CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(new { state = state.ToString() });
            await _http.PutAsync(
                $"{BaseUrl}/{tableId}",
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
            // X was 450 — table 7 is a 160-wide rectangle starting at 310, so its right edge
            // (470) actually sat 20px past this table's left edge, overlapping it.
            new(_mockIds[10],11, 4, TableState.Occupied,  TableShapeType.Circular,    500, 190, 100,100, false, now.AddMinutes(-30), "María"),
            new(_mockIds[11],12, 8, TableState.Reserved,  TableShapeType.Rectangular, 40,  500, 160, 90, false, null,                null),
        ];
    }

    private static IReadOnlyList<ActiveOrderSummary> MockOrders() =>
    [
        new(Guid.NewGuid(), "A-041", 3, 52.40m,  "Preparing", DateTimeOffset.Now.AddMinutes(-18)),
        new(Guid.NewGuid(), "A-042", 2, 28.00m,  "Ready",     DateTimeOffset.Now.AddMinutes(-5)),
    ];
}

// Matches backend's TableFloorPlanDto (Tables/DTOs/TableFloorPlanDto.cs).
internal sealed record TableFloorPlanDto(
    Guid   Id,
    string Name,
    int    Capacity,
    string? Zone,
    string Status,
    double? PositionX,
    double? PositionY,
    int    ActiveOrderCount,
    string? CurrentOrderNumber);
