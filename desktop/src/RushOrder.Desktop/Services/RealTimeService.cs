using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RushOrder.Desktop.Core.Hubs;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class RealTimeService : IAsyncDisposable
{
    private readonly AppState _state;
    private readonly ILogger<RealTimeService> _logger;
    private RestaurantHubClient? _client;

    public bool IsConnected => _client?.State == HubConnectionState.Connected;

    public event Func<OrderReceivedPayload, Task>?      OrderReceived;
    public event Func<string, string, Task>?             TableStatusChanged;
    public event Func<string, string, DateTimeOffset, Task>? OrderStatusUpdated;
    public event Func<string, string, Task>?             KitchenAlert;
    public event Func<string, Task>?                     MiseEnPlaceAlert;
    public event Action<bool>?                           ConnectionChanged;

    public RealTimeService(AppState state, ILogger<RealTimeService> logger)
    {
        _state  = state;
        _logger = logger;

        _state.UserChanged += user =>
        {
            if (user is null && IsConnected)
                _ = DisconnectAsync();
        };
    }

    public async Task ConnectAsync(string baseUrl, CancellationToken ct = default)
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        if (_state.AccessToken is null) return;

        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/restaurant";
        _logger.LogInformation("Connecting to SignalR hub: {Url}", hubUrl);

        using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var hubLogger = loggerFactory.CreateLogger<RestaurantHubClient>();
        _client = new RestaurantHubClient(hubUrl, _state.AccessToken, hubLogger);
        WireEvents();

        try
        {
            await _client.StartAsync(ct);

            if (_state.CurrentRestaurant is not null)
                await _client.JoinRestaurantAsync(_state.CurrentRestaurant.Id.ToString(), ct);

            ConnectionChanged?.Invoke(true);
            _state.SetOnlineStatus(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            ConnectionChanged?.Invoke(false);
            _state.SetOnlineStatus(false);
        }
    }

    private async Task DisconnectAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        ConnectionChanged?.Invoke(false);
    }

    private void WireEvents()
    {
        _client!.OnOrderReceived += p =>
            OrderReceived?.Invoke(p) ?? Task.CompletedTask;

        _client!.OnTableStatusChanged += (id, status) =>
            TableStatusChanged?.Invoke(id, status) ?? Task.CompletedTask;

        _client!.OnOrderStatusUpdated += (id, status, ts) =>
            OrderStatusUpdated?.Invoke(id, status, ts) ?? Task.CompletedTask;

        _client!.OnKitchenAlert += (msg, severity) =>
            KitchenAlert?.Invoke(msg, severity) ?? Task.CompletedTask;

        _client!.OnMiseEnPlaceAlert += msg =>
            MiseEnPlaceAlert?.Invoke(msg) ?? Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}
