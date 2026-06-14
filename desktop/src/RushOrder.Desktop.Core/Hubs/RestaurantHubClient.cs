using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace RushOrder.Desktop.Core.Hubs;

/// <summary>
/// Typed SignalR client for the restaurant desktop application.
/// Manages connection lifecycle with automatic exponential-backoff reconnection.
/// </summary>
public sealed class RestaurantHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<RestaurantHubClient> _logger;
    private string? _currentRestaurantId;

    public event Func<OrderReceivedPayload, Task>? OnOrderReceived;
    public event Func<string, string, DateTimeOffset, Task>? OnOrderStatusUpdated;
    public event Func<string, string, Task>? OnTableStatusChanged;
    public event Func<string, bool, Task>? OnProductAvailabilityChanged;
    public event Func<string, string, Task>? OnKitchenAlert;
    public event Func<string, string, int, Task>? OnLowStockAlert;

    public HubConnectionState State => _connection.State;

    public RestaurantHubClient(string hubUrl, string accessToken, ILogger<RestaurantHubClient> logger)
    {
        _logger = logger;

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .Build();

        RegisterHandlers();
        RegisterLifecycleEvents();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("RestaurantHub connected. ConnectionId={Id}", _connection.ConnectionId);
    }

    public async Task JoinRestaurantAsync(string restaurantId, CancellationToken ct = default)
    {
        _currentRestaurantId = restaurantId;
        await _connection.InvokeAsync("JoinRestaurantAsync", restaurantId, ct);
    }

    public async Task JoinKitchenAsync(string restaurantId, CancellationToken ct = default)
    {
        await _connection.InvokeAsync("JoinKitchenAsync", restaurantId, ct);
    }

    public async Task AcknowledgeOrderAsync(string orderId, CancellationToken ct = default)
    {
        await _connection.InvokeAsync("AcknowledgeOrderAsync", orderId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private void RegisterHandlers()
    {
        _connection.On<OrderReceivedPayload>("OrderReceived", payload =>
            OnOrderReceived?.Invoke(payload) ?? Task.CompletedTask);

        _connection.On<string, string, DateTimeOffset>("OrderStatusUpdated", (orderId, status, timestamp) =>
            OnOrderStatusUpdated?.Invoke(orderId, status, timestamp) ?? Task.CompletedTask);

        _connection.On<string, string>("TableStatusChanged", (tableId, status) =>
            OnTableStatusChanged?.Invoke(tableId, status) ?? Task.CompletedTask);

        _connection.On<string, bool>("ProductAvailabilityChanged", (productId, isAvailable) =>
            OnProductAvailabilityChanged?.Invoke(productId, isAvailable) ?? Task.CompletedTask);

        _connection.On<string, string>("KitchenAlert", (message, severity) =>
            OnKitchenAlert?.Invoke(message, severity) ?? Task.CompletedTask);

        _connection.On<string, string, int>("LowStockAlert", (productId, productName, remainingStock) =>
            OnLowStockAlert?.Invoke(productId, productName, remainingStock) ?? Task.CompletedTask);
    }

    private void RegisterLifecycleEvents()
    {
        _connection.Reconnecting += ex =>
        {
            _logger.LogWarning(ex, "RestaurantHub connection lost. Reconnecting…");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("RestaurantHub reconnected. ConnectionId={Id}", connectionId);

            // Re-join groups after reconnection — SignalR groups are lost on reconnect
            if (_currentRestaurantId is not null)
            {
                await _connection.InvokeAsync("JoinRestaurantAsync", _currentRestaurantId);
                _logger.LogInformation("Re-joined restaurant group: {Id}", _currentRestaurantId);
            }
        };

        _connection.Closed += ex =>
        {
            if (ex is not null)
                _logger.LogError(ex, "RestaurantHub connection closed with error.");
            else
                _logger.LogInformation("RestaurantHub connection closed.");
            return Task.CompletedTask;
        };
    }
}

/// <summary>
/// Exponential backoff retry policy: 0 s, 2 s, 10 s, 30 s, then 60 s indefinitely.
/// </summary>
internal sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] Delays = [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var index = (int)retryContext.PreviousRetryCount;
        return index < Delays.Length ? Delays[index] : TimeSpan.FromSeconds(60);
    }
}

// ── Payload DTOs (mirrors OrderRealTimeDto without a project reference) ──

public record OrderReceivedPayload(
    Guid OrderId,
    string OrderNumber,
    Guid RestaurantId,
    Guid TableId,
    string TableName,
    string Status,
    string Source,
    IReadOnlyList<OrderItemPayload> Items,
    decimal Total,
    string Currency,
    string? Notes,
    DateTimeOffset PlacedAt);

public record OrderItemPayload(
    Guid ProductId,
    string Name,
    int Quantity,
    string? Notes,
    IReadOnlyList<string> Modifiers);
