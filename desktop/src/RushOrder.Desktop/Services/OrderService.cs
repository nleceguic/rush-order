using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Data;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class OrderService
{
    private const string BaseUrl = "http://localhost:5143/api/v1/orders";

    private readonly AppState    _state;
    private readonly LocalDatabase _db;
    private readonly SyncService   _sync;
    private readonly ILogger<OrderService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    // In-memory cache (source of truth for running views)
    private List<OrderDto> _cache = [];

    public OrderService(AppState state, LocalDatabase db, SyncService sync,
        ILogger<OrderService> logger)
    {
        _state  = state;
        _db     = db;
        _sync   = sync;
        _logger = logger;

        // Keep in-memory cache in sync when a queued CREATE_ORDER completes
        _sync.OrderIdReplaced += OnOrderIdReplaced;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        if (_state.IsOnline)
        {
            try
            {
                SetAuth();
                var rid = _state.CurrentRestaurant?.Id;
                // Backend's GetOrders only accepts a single nullable `status` (not a
                // multi-value list) — omit it and filter Cancelled out client-side below.
                var res = await _http.GetAsync(
                    $"{BaseUrl}?restaurantId={rid}&pageSize=200",
                    ct);
                if (res.IsSuccessStatusCode)
                {
                    var json    = await res.Content.ReadAsStringAsync(ct);
                    var summary = JsonConvert.DeserializeObject<ApiEnvelope<List<OrderSummaryDto>>>(json)?.Data ?? [];
                    // Summary rows don't include line items (backend list endpoint is
                    // summary-only); Kanban cards will show 0 items until GetById is
                    // wired up per-order.
                    _cache = summary
                        .Where(s => !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                        .Select(MapSummary)
                        .ToList();

                    // Merge any pending offline orders that haven't synced yet
                    var offlineInQueue = _db.GetOrders().Where(o => o.IsOffline).ToList();
                    foreach (var offline in offlineInQueue)
                    {
                        if (_cache.All(o => o.Id != offline.Id))
                            _cache.Insert(0, offline);
                    }

                    // Snapshot for offline use
                    foreach (var o in _cache) _db.UpsertOrder(o);
                    return _cache;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not fetch orders; falling back to local"); }
        }

        // Offline or API unavailable: load from local DB
        var local = _db.GetOrders();
        _cache = local.Count > 0 ? local : BuildMockOrders();
        return _cache;
    }

    private static OrderDto MapSummary(OrderSummaryDto s) => new(
        s.Id, s.OrderNumber, Guid.Empty, s.TableId, s.TableName ?? "—", 0,
        MapStatus(s.Status), s.Source, null, [], s.Total, 0m, s.Total, null,
        s.CreatedAt, null, null, null, null);

    // Backend's OrderStatus has Pending/Confirmed/Cancelled with no desktop equivalent
    // (desktop's 5-state Kanban predates those). Pending/Confirmed both fold into New;
    // Cancelled orders are filtered out before reaching this mapper.
    private static OrderStatus MapStatus(string status) => status switch
    {
        "Pending" or "Confirmed" => OrderStatus.New,
        "Preparing"              => OrderStatus.Preparing,
        "Ready"                  => OrderStatus.Ready,
        "Served"                 => OrderStatus.Served,
        "Paid"                   => OrderStatus.Paid,
        _                        => OrderStatus.New,
    };

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct = default)
    {
        // Optimistic update in memory and local DB
        var idx = _cache.FindIndex(o => o.Id == orderId);
        if (idx >= 0) _cache[idx] = _cache[idx] with { Status = newStatus };
        _db.UpdateOrderStatus(orderId.ToString(), newStatus.ToString());

        // Backend's OrderStatus has no "New" member (it's Pending/Confirmed there) —
        // PATCHing to New would fail server-side enum binding. In practice this only
        // matters if something re-opens an order to New, which the UI doesn't do today.
        if (!_state.IsOnline)
        {
            var payload = JsonConvert.SerializeObject(new { status = newStatus.ToString() });
            _sync.Enqueue("UPDATE_ORDER_STATUS",
                $"/api/v1/orders/{orderId}/status", "PATCH", payload);
            return true;
        }

        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(new { status = newStatus.ToString() });
            var res  = await _http.PatchAsync(
                $"{BaseUrl}/{orderId}/status",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Status update failed; queuing for sync");
            var payload = JsonConvert.SerializeObject(new { status = newStatus.ToString() });
            _sync.Enqueue("UPDATE_ORDER_STATUS",
                $"/api/v1/orders/{orderId}/status", "PATCH", payload);
            return true;
        }
    }

    public async Task<OrderDto?> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (!_state.IsOnline)
            return CreateOfflineOrder(request);

        // Backend requires a non-null TableId — counter/"Barra" orders with no table
        // can't be created online today; fall back to the offline queue for those.
        if (request.TableId is null)
            return CreateOfflineOrder(request);

        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(ToBackendRequest(request));
            var res  = await _http.PostAsync(
                BaseUrl,
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (res.IsSuccessStatusCode)
            {
                var json   = await res.Content.ReadAsStringAsync(ct);
                var result = JsonConvert.DeserializeObject<ApiEnvelope<CreateOrderResult>>(json)?.Data;
                if (result is not null)
                {
                    // Response only carries {orderId, orderNumber, estimatedReadyAt,
                    // trackingToken} — build the display order from what we already
                    // sent, patched with the server-assigned id/number.
                    var order = BuildOrderFromRequest(request, result.OrderId, result.OrderNumber, isOffline: false);
                    _cache.Insert(0, order);
                    _db.UpsertOrder(order);
                    return order;
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Create order failed; creating offline"); }

        return CreateOfflineOrder(request);
    }

    private OrderDto CreateOfflineOrder(CreateOrderRequest request)
    {
        var num = $"LOCAL-{DateTime.Now:HHmm}-{Random.Shared.Next(10, 99)}";
        var order = BuildOrderFromRequest(request, Guid.NewGuid(), num, isOffline: true);

        _cache.Insert(0, order);
        _db.UpsertOrder(order);
        // Enqueue the backend-shaped payload (not the raw desktop request) so the
        // sync retry actually binds against OrdersController.CreateOrder later.
        _sync.Enqueue("CREATE_ORDER", "/api/v1/orders", "POST",
            JsonConvert.SerializeObject(ToBackendRequest(request)), order.Id.ToString());

        _logger.LogInformation("Created offline order {Num}", order.OrderNumber);
        return order;
    }

    private static object ToBackendRequest(CreateOrderRequest request) => new
    {
        tableId    = request.TableId,
        customerId = (Guid?)null,
        items      = request.Items.Select(i => new
        {
            productId = i.ProductId,
            quantity  = i.Quantity,
            notes     = i.Notes,
        }),
        notes  = request.Notes,
        source = request.Source,
    };

    private static OrderDto BuildOrderFromRequest(
        CreateOrderRequest request, Guid orderId, string orderNumber, bool isOffline)
    {
        var items = request.Items.Select(i =>
            new OrderItemDto(Guid.NewGuid(), i.ProductId, i.ProductName, i.Quantity,
                i.UnitPrice, i.UnitPrice * i.Quantity, i.Notes, [], [])).ToList();
        var sub = items.Sum(x => x.LineTotal);
        var tax = Math.Round(sub * 0.10m, 2);

        return new OrderDto(
            orderId, orderNumber, request.RestaurantId, request.TableId, request.TableName,
            request.GuestCount, OrderStatus.New, request.Source, null, items,
            sub, tax, sub + tax, request.Notes, DateTimeOffset.Now,
            null, null, null, null, IsOffline: isOffline);
    }

    // NOTE: there is no `POST /api/v1/payments` endpoint on the backend — real routes
    // are the Stripe-style `/payments/intent` + `/payments/confirm` (or `/split` for
    // split bills), which need a different request/response flow than this simple
    // "charge and mark paid" call. This always falls through to the local/offline
    // path below; the order is marked Paid locally but no real payment record is ever
    // created server-side. Needs product/design input before it can be wired up.
    public async Task<bool> ProcessPaymentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        if (!_state.IsOnline)
        {
            await UpdateStatusAsync(request.OrderId, OrderStatus.Paid, ct);
            _sync.Enqueue("PROCESS_PAYMENT", "/api/v1/payments", "POST",
                JsonConvert.SerializeObject(request));
            return true;
        }

        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(request);
            var res  = await _http.PostAsync(
                "http://localhost:5143/api/v1/payments",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (res.IsSuccessStatusCode)
            {
                await UpdateStatusAsync(request.OrderId, OrderStatus.Paid, ct);
                return true;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Payment failed; marking paid locally"); }

        await UpdateStatusAsync(request.OrderId, OrderStatus.Paid, ct);
        _sync.Enqueue("PROCESS_PAYMENT", "/api/v1/payments", "POST",
            JsonConvert.SerializeObject(request));
        return true;
    }

    // NOTE: there is no waiter-assignment route on OrdersController at all — this
    // will always fail online and fall back to queuing indefinitely. Needs a new
    // backend endpoint before it can work.
    public async Task<bool> AssignWaiterAsync(Guid orderId, string waiterName, CancellationToken ct = default)
    {
        if (!_state.IsOnline)
        {
            _sync.Enqueue("ASSIGN_WAITER", $"/api/v1/orders/{orderId}/waiter", "PUT",
                JsonConvert.SerializeObject(new { waiterName }));
            return true;
        }
        try
        {
            SetAuth();
            var body = JsonConvert.SerializeObject(new { waiterName });
            var res  = await _http.PutAsync(
                $"{BaseUrl}/{orderId}/waiter",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        _cache.RemoveAll(o => o.Id == orderId);
        _db.UpdateOrderStatus(orderId.ToString(), OrderStatus.Paid.ToString()); // mark closed

        // Backend route is POST {id}/cancel with a required Reason (not DELETE {id}).
        var payload = JsonConvert.SerializeObject(new { reason = "Cancelado desde el POS" });

        if (!_state.IsOnline)
        {
            _sync.Enqueue("CANCEL_ORDER", $"/api/v1/orders/{orderId}/cancel", "POST", payload);
            return true;
        }
        try
        {
            SetAuth();
            var res = await _http.PostAsync(
                $"{BaseUrl}/{orderId}/cancel",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            return res.IsSuccessStatusCode;
        }
        catch { return true; }
    }

    // ── Cache maintenance ─────────────────────────────────────────────────────

    private void OnOrderIdReplaced(string localId, Guid serverId, OrderDto serverOrder)
    {
        if (!Guid.TryParse(localId, out var localGuid)) return;
        var idx = _cache.FindIndex(o => o.Id == localGuid);
        if (idx >= 0) _cache[idx] = serverOrder with { IsOffline = false };
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;

    // ── Mock data ─────────────────────────────────────────────────────────────

    private static List<OrderDto> BuildMockOrders()
    {
        var now = DateTimeOffset.Now;
        return
        [
            MakeOrder("2024-0041", "Mesa 1", 2, OrderStatus.New, "Ana", now.AddMinutes(-12),
                [Item("Paella Valencia", 1, 14.50m), Item("Cola Zero", 2, 2.20m), Item("Agua Mineral", 1, 1.50m, ["Sulphites"])]),

            MakeOrder("2024-0042", "Mesa 5", 4, OrderStatus.New, "Carlos", now.AddMinutes(-2),
                [Item("Menú del día x4", 4, 12.50m), Item("Vino de la casa", 1, 8.00m), Item("Postre variado", 4, 4.00m, ["Gluten", "Dairy", "Eggs"])]),

            MakeOrder("2024-0043", "Mesa 8", 6, OrderStatus.New, "María", now.AddSeconds(-45),
                [Item("Gazpacho", 6, 5.00m), Item("Cochinillo", 3, 22.00m, ["Sulphites"])]),

            MakeOrder("2024-0039", "Mesa 3", 2, OrderStatus.Preparing, "Ana", now.AddMinutes(-18),
                [Item("Tortilla española", 1, 9.50m), Item("Cerveza", 2, 2.80m)]),

            MakeOrder("2024-0040", "Mesa 7", 8, OrderStatus.Preparing, "Carlos", now.AddMinutes(-27),
                [Item("Cocido madrileño x8", 8, 16.00m, ["Gluten", "Nuts"]), Item("Sangría", 2, 12.00m, ["Sulphites"])]),

            MakeOrder("2024-0038", "Mesa 6", 3, OrderStatus.Ready, "María", now.AddMinutes(-35),
                [Item("Chuletón 400g", 2, 28.00m), Item("Ensalada mixta", 1, 7.50m), Item("Patatas fritas", 2, 4.50m)]),

            MakeOrder("2024-0037", "Mesa 2", 2, OrderStatus.Served, "Ana", now.AddMinutes(-55),
                [Item("Salmón a la plancha", 2, 18.50m), Item("Agua con gas", 2, 1.80m)]),

            MakeOrder("2024-0036", "Mesa 4", 4, OrderStatus.Served, "Carlos", now.AddMinutes(-80),
                [Item("Pulpo a la gallega", 1, 18.00m), Item("Merluza al horno", 2, 21.00m), Item("Café", 4, 1.50m)]),

            MakeOrder("2024-0035", "Barra", 1, OrderStatus.Paid, "María", now.AddMinutes(-105),
                [Item("Pincho de tortilla", 2, 2.50m), Item("Caña", 2, 1.80m)]),
        ];
    }

    private static OrderDto MakeOrder(string num, string table, int guests, OrderStatus status,
        string waiter, DateTimeOffset placed, IReadOnlyList<OrderItemDto> items)
    {
        var sub = items.Sum(i => i.LineTotal);
        var tax = Math.Round(sub * 0.10m, 2);
        return new OrderDto(
            Guid.NewGuid(), num, Guid.Empty, Guid.NewGuid(), table, guests, status,
            "POS", waiter, items, sub, tax, sub + tax, null,
            placed, null, null, null, null);
    }

    private static OrderItemDto Item(string name, int qty, decimal price, string[]? allergens = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, qty, price, price * qty, null,
            allergens ?? [], []);
}

// (ApiEnvelope<T> is defined in AuthService.cs — same namespace, reused here.)

// Matches backend's OrderSummaryDto (Orders/DTOs/OrderSummaryDto.cs) — GET /orders
// list endpoint. Note this has no line items and no GuestCount; see MapSummary().
internal sealed record OrderSummaryDto(
    Guid   Id,
    string OrderNumber,
    Guid   TableId,
    string? TableName,
    string Status,
    string Source,
    decimal Total,
    string Currency,
    int    ItemCount,
    DateTimeOffset CreatedAt);

// Matches backend's CreateOrderResult (Orders/DTOs/CreateOrderResult.cs).
internal sealed record CreateOrderResult(
    Guid   OrderId,
    string OrderNumber,
    DateTimeOffset? EstimatedReadyAt,
    string TrackingToken);
