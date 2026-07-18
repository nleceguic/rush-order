using System.Net.Http.Json;
using RushOrder.Desktop.Core.Hubs;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class KitchenService
{
    private readonly AppState   _state;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly List<KitchenOrderDto> _cache           = [];
    private readonly List<TimeSpan>        _completedTimes  = [];
    private          int                   _completedCount  = 0;
    private readonly DateTimeOffset        _shiftStart      = DateTimeOffset.Now;

    public KitchenService(AppState state) => _state = state;

    // Backend's list endpoint (GET /orders) has no multi-status filter and returns
    // summary rows with no items — kitchen tickets need item-level detail, so this
    // fetches the active summary list then GETs each order's detail individually
    // (N+1, but there's no bulk-with-items endpoint to use instead).
    public async Task<IReadOnlyList<KitchenOrderDto>> GetActiveOrdersAsync()
    {
        try
        {
            ApplyAuth();
            var baseUrl = BaseUrl();
            var rid     = _state.CurrentRestaurant?.Id;
            var summary = await _http.GetFromJsonAsync<ApiEnvelope<List<OrderSummaryDto>>>(
                $"{baseUrl}/api/v1/orders?restaurantId={rid}&pageSize=200");
            var active = summary?.Data?
                .Where(o => o.Status is "Pending" or "Confirmed" or "Preparing" or "Ready")
                .ToList();
            if (active is null) throw new InvalidOperationException("null response");

            var detailed = new List<KitchenOrderDto>();
            foreach (var o in active)
            {
                var env = await _http.GetFromJsonAsync<ApiEnvelope<KitchenOrderDetailDto>>(
                    $"{baseUrl}/api/v1/orders/{o.Id}");
                if (env?.Data is not null) detailed.Add(MapToKitchen(env.Data));
            }

            _cache.Clear();
            _cache.AddRange(detailed);
            return _cache.AsReadOnly();
        }
        catch
        {
            if (_cache.Count > 0) return _cache.AsReadOnly();
            var mocks = MockOrders();
            _cache.Clear();
            _cache.AddRange(mocks);
            return _cache.AsReadOnly();
        }
    }

    public async Task MarkAsPreparingAsync(Guid orderId)
    {
        try
        {
            ApplyAuth();
            await _http.PatchAsync($"{BaseUrl()}/api/v1/orders/{orderId}/status",
                JsonContent.Create(new { Status = "Preparing" }));
        }
        catch { }
        UpdateStatus(orderId, OrderStatus.Preparing);
    }

    public async Task MarkAsReadyAsync(Guid orderId)
    {
        try
        {
            ApplyAuth();
            await _http.PatchAsync($"{BaseUrl()}/api/v1/orders/{orderId}/status",
                JsonContent.Create(new { Status = "Ready" }));
        }
        catch { }

        var order = _cache.FirstOrDefault(o => o.OrderId == orderId);
        if (order is not null)
        {
            _completedCount++;
            _completedTimes.Add(DateTimeOffset.Now - order.PlacedAt);
        }
        UpdateStatus(orderId, OrderStatus.Ready);
    }

    public Task SetUrgentAsync(Guid orderId, bool urgent)
    {
        var idx = _cache.FindIndex(o => o.OrderId == orderId);
        if (idx >= 0) _cache[idx] = _cache[idx] with { IsUrgent = urgent };
        return Task.CompletedTask;
    }

    public KitchenShiftStats GetShiftStats()
    {
        var avg     = _completedTimes.Count > 0
            ? TimeSpan.FromSeconds(_completedTimes.Average(t => t.TotalSeconds))
            : TimeSpan.Zero;
        var pending = _cache.Count(o => o.Status is OrderStatus.New or OrderStatus.Preparing);
        return new KitchenShiftStats(_completedCount, avg, pending, _shiftStart);
    }

    public KitchenOrderDto? AddFromSignalR(OrderReceivedPayload p)
    {
        var dto = new KitchenOrderDto(
            p.OrderId, p.OrderNumber, p.TableName, 2, p.PlacedAt,
            OrderStatus.New, false,
            p.Items.Select(i => new KitchenItemDto(
                Guid.NewGuid(), i.ProductId, i.Name, i.Quantity,
                [], i.Modifiers, i.Notes,
                KitchenStationHelper.Detect(i.Name))).ToList());

        var idx = _cache.FindIndex(o => o.OrderId == p.OrderId);
        if (idx >= 0) _cache[idx] = dto;
        else _cache.Add(dto);
        return dto;
    }

    public KitchenOrderDto? UpdateStatusFromSignalR(string orderId, string statusStr)
    {
        if (!Guid.TryParse(orderId, out var id)) return null;
        if (!Enum.TryParse<OrderStatus>(statusStr, out var status)) return null;
        var idx = _cache.FindIndex(o => o.OrderId == id);
        if (idx < 0) return null;
        _cache[idx] = _cache[idx] with { Status = status };
        return _cache[idx];
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyAuth() =>
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _state.AccessToken);

    private string BaseUrl() =>
        "http://localhost:5143"; // real apps: read from config/state

    private void UpdateStatus(Guid orderId, OrderStatus status)
    {
        var idx = _cache.FindIndex(o => o.OrderId == orderId);
        if (idx >= 0) _cache[idx] = _cache[idx] with { Status = status };
    }

    // Backend's OrderStatus has Pending/Confirmed/Cancelled with no desktop
    // equivalent; Pending/Confirmed both fold into New (see OrderService.MapStatus
    // for the same mapping — kept local here to avoid cross-service coupling).
    private static OrderStatus MapStatus(string status) => status switch
    {
        "Pending" or "Confirmed" => OrderStatus.New,
        "Preparing"              => OrderStatus.Preparing,
        "Ready"                  => OrderStatus.Ready,
        "Served"                 => OrderStatus.Served,
        "Paid"                   => OrderStatus.Paid,
        _                        => OrderStatus.New,
    };

    // Backend order items have no per-item Allergens field (allergens live on
    // Product, not OrderItem) — always empty until the backend joins that in.
    private static KitchenOrderDto MapToKitchen(KitchenOrderDetailDto o) => new(
        o.Id, o.OrderNumber, o.TableName ?? "—", 2, o.CreatedAt, MapStatus(o.Status), false,
        o.Items.Select(i => new KitchenItemDto(
            i.Id, i.ProductId, i.Name, i.Quantity,
            [], i.Modifiers, i.Notes,
            KitchenStationHelper.Detect(i.Name))).ToList());

    // ── Mock data ─────────────────────────────────────────────────────────

    private static List<KitchenOrderDto> MockOrders()
    {
        var now = DateTimeOffset.Now;
        return
        [
            new(Guid.NewGuid(), "#1042", "Mesa 3",  4, now.AddMinutes(-3),  OrderStatus.New,       false,
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Chuletón de ternera", 2, ["Gluten"],              ["Punto: vuelta y vuelta"], "Sin sal",  KitchenStation.Grill),
                new(Guid.NewGuid(), Guid.NewGuid(), "Patatas bravas",      1, [],                      [],                        null,       KitchenStation.HotKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Ensalada César",      2, ["Gluten","Lácteos","Huevo"], [],                  null,       KitchenStation.ColdKitchen),
            ]),
            new(Guid.NewGuid(), "#1041", "Mesa 7",  2, now.AddMinutes(-10), OrderStatus.Preparing,   true,
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Risotto de setas",    2, ["Gluten","Lácteos"],   ["Sin queso"],             "Urgente — cliente con prisa", KitchenStation.HotKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Tiramisú",            1, ["Huevo","Lácteos"],    [],                        null,       KitchenStation.Dessert),
            ]),
            new(Guid.NewGuid(), "#1040", "Mesa 1",  6, now.AddMinutes(-14), OrderStatus.Preparing,   false,
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Paella valenciana",   2, ["Mariscos","Gluten"],  [],                        "Para 2 personas", KitchenStation.HotKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Gazpacho andaluz",    2, [],                     [],                        null,       KitchenStation.ColdKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Pan de ajo",          1, ["Gluten","Lácteos"],   [],                        null,       KitchenStation.HotKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Flan de huevo",       2, ["Huevo","Lácteos"],    [],                        null,       KitchenStation.Dessert),
            ]),
            new(Guid.NewGuid(), "#1039", "Barra 2", 1, now.AddMinutes(-2),  OrderStatus.New,         false,
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Hamburguesa BBQ",     1, ["Gluten","Lácteos"],   ["Sin cebolla","Extra bacon"], null,  KitchenStation.Grill),
                new(Guid.NewGuid(), Guid.NewGuid(), "Patatas fritas",      1, [],                     [],                        null,       KitchenStation.HotKitchen),
            ]),
            new(Guid.NewGuid(), "#1038", "Mesa 5",  3, now.AddMinutes(-20), OrderStatus.Ready,       false,
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Pulpo a la gallega",  1, ["Mariscos"],           [],                        null,       KitchenStation.HotKitchen),
                new(Guid.NewGuid(), Guid.NewGuid(), "Agua mineral",        3, [],                     [],                        null,       KitchenStation.Bar),
            ]),
        ];
    }
}

// Matches the subset of backend's OrderDetailDto (Orders/DTOs/OrderDetailDto.cs)
// needed for kitchen tickets.
internal sealed record KitchenOrderDetailDto(
    Guid Id, string OrderNumber, string? TableName, string Status,
    DateTimeOffset CreatedAt, IReadOnlyList<KitchenOrderItemDetailDto> Items);

// Matches backend's OrderItemDto (Orders/DTOs/OrderItemDto.cs) — no Allergens field.
internal sealed record KitchenOrderItemDetailDto(
    Guid Id, Guid ProductId, string Name, int Quantity, string? Notes,
    IReadOnlyList<string> Modifiers);
