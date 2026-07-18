using Microsoft.Extensions.Logging;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class ConnectivityMonitor : IDisposable
{
    private readonly AppState    _state;
    private readonly SyncService _sync;
    private readonly ILogger<ConnectivityMonitor> _logger;
    private readonly CancellationTokenSource _cts = new();

    private const string HealthUrl = "http://localhost:5143/health";

    public ConnectivityMonitor(AppState state, SyncService sync, ILogger<ConnectivityMonitor> logger)
    {
        _state  = state;
        _sync   = sync;
        _logger = logger;

        _ = Task.Run(PingLoopAsync);
    }

    private async Task PingLoopAsync()
    {
        // Give the app 2 s to finish initializing before first ping
        await Delay(2000);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        while (!_cts.IsCancellationRequested)
        {
            var wasOnline = _state.IsOnline;
            bool isOnline;

            try
            {
                var res = await http.GetAsync(HealthUrl, _cts.Token).ConfigureAwait(false);
                isOnline = res.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check failed");
                isOnline = false;
            }

            _state.SetOnlineStatus(isOnline);

            if (isOnline && !wasOnline)
            {
                _logger.LogInformation("Connectivity restored — processing sync queue");
                _ = Task.Run(_sync.ProcessQueueAsync);
            }

            await Delay(10_000);
        }
    }

    private async Task Delay(int ms)
    {
        try { await Task.Delay(ms, _cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => _cts.Cancel();
}
