using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Data;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class SyncService : IDisposable
{
    private readonly LocalDatabase _db;
    private readonly AppState      _state;
    private readonly ILogger<SyncService> _logger;
    private readonly System.Threading.Timer _autoRefreshTimer;
    private bool _isSyncing;

    // UI-facing events (may fire from background threads; callers must Invoke)
    public event Action<string>? StatusMessage;
    public event Action<int>?    PendingCountChanged;

    // Async conflict handler: set by MainForm to show ConflictResolutionDialog on UI thread
    public Func<ConflictInfo, Task<ConflictResolution>>? ConflictHandler { get; set; }

    // Fired after a CREATE_ORDER sync so OrderService can update its cache
    public event Action<string, Guid, OrderDto>? OrderIdReplaced; // (localId, serverId, serverOrder)

    // Fired when sync fully completes so views can refresh
    public event Action? SyncCompleted;

    public int PendingCount { get; private set; }

    public SyncService(LocalDatabase db, AppState state, ILogger<SyncService> logger)
    {
        _db     = db;
        _state  = state;
        _logger = logger;

        // Auto-refresh every 5 minutes when online
        _autoRefreshTimer = new System.Threading.Timer(
            _ => { if (_state.IsOnline && !_isSyncing) _ = ProcessQueueAsync(); },
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));

        UpdatePendingCount();
    }

    public async Task ProcessQueueAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            var items = _db.GetPendingOperations();
            if (items.Count == 0)
            {
                StatusMessage?.Invoke("");
                return;
            }

            var plural = items.Count == 1 ? "operación" : "operaciones";
            StatusMessage?.Invoke($"⟳ Sincronizando {items.Count} {plural}...");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            if (_state.AccessToken is { } tok)
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", tok);

            var idMapping = new Dictionary<string, string>(); // localId → serverId
            int synced    = 0;

            foreach (var op in items)
            {
                // Replace any previously-resolved local IDs in payload
                var payload = op.PayloadJson;
                foreach (var (localId, serverId) in idMapping)
                    payload = payload.Replace(localId, serverId);

                try
                {
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using var req     = new HttpRequestMessage(
                        new HttpMethod(op.HttpMethod),
                        $"http://localhost:5000{op.Endpoint}") { Content = content };
                    using var res = await http.SendAsync(req).ConfigureAwait(false);

                    if (res.IsSuccessStatusCode)
                    {
                        if (op.OperationType == "CREATE_ORDER" && op.LocalRefId is { } localRef)
                            await HandleOrderCreatedAsync(res, localRef, idMapping);

                        _db.DeleteQueueItem(op.Id);
                        synced++;
                    }
                    else if ((int)res.StatusCode == 409)
                    {
                        await HandleConflictAsync(op, res);
                    }
                    else
                    {
                        _db.IncrementRetryCount(op.Id, $"HTTP {(int)res.StatusCode}");
                        _logger.LogWarning("Sync op {Type} got {Code}", op.OperationType, (int)res.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sync op {Type} failed", op.OperationType);
                    _db.IncrementRetryCount(op.Id, ex.Message);
                }

                UpdatePendingCount();
            }

            if (synced > 0)
            {
                var syncedPlural = synced == 1 ? "operación sincronizada" : "operaciones sincronizadas";
                StatusMessage?.Invoke($"✓ {synced} {syncedPlural}");
                SyncCompleted?.Invoke();

                // Clear the message after 4 seconds
                _ = Task.Delay(4000).ContinueWith(_ => StatusMessage?.Invoke(""));
            }
            else
            {
                StatusMessage?.Invoke("");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync queue processing failed");
            StatusMessage?.Invoke("✗ Error de sincronización");
            _ = Task.Delay(4000).ContinueWith(_ => StatusMessage?.Invoke(""));
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task HandleOrderCreatedAsync(
        HttpResponseMessage res, string localRef, Dictionary<string, string> idMapping)
    {
        try
        {
            var body        = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var serverOrder = JsonConvert.DeserializeObject<OrderDto>(body);
            if (serverOrder is null) return;

            var serverIdStr = serverOrder.Id.ToString();
            idMapping[localRef] = serverIdStr;

            _db.UpdateOrderServerId(localRef, serverOrder.Id, serverOrder);
            OrderIdReplaced?.Invoke(localRef, serverOrder.Id, serverOrder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse server order after CREATE_ORDER sync");
        }
    }

    private async Task HandleConflictAsync(PendingSyncOperation op, HttpResponseMessage res)
    {
        try
        {
            var serverBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var info       = new ConflictInfo(op, serverBody);

            if (ConflictHandler is { } handler)
            {
                var resolution = await handler(info).ConfigureAwait(false);
                if (resolution is ConflictResolution.KeepServer or ConflictResolution.Defer)
                    _db.DeleteQueueItem(op.Id);
                else
                    _db.IncrementRetryCount(op.Id, "409 – usuario forzó local");
            }
            else
            {
                // No UI handler: skip the conflicted op to keep the queue moving
                _db.DeleteQueueItem(op.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conflict handling failed");
            _db.IncrementRetryCount(op.Id, "409 conflict handler error");
        }
    }

    public void Enqueue(string operationType, string endpoint, string httpMethod,
        string payloadJson, string? localRefId = null)
    {
        _db.EnqueueOperation(operationType, endpoint, httpMethod, payloadJson, localRefId);
        UpdatePendingCount();
    }

    private void UpdatePendingCount()
    {
        PendingCount = _db.GetPendingCount();
        PendingCountChanged?.Invoke(PendingCount);
    }

    public void Dispose() => _autoRefreshTimer.Dispose();
}
