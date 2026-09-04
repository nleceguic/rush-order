using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class ForecastDataService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AppState _state;
    private readonly ILogger<ForecastDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public ForecastDataService(AppState state, ILogger<ForecastDataService> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<Result<DemandForecastResult>> GetDemandForecastAsync(DateOnly date, CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/demand-forecast?restaurantId={restaurantId}&date={date:yyyy-MM-dd}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<DemandForecastResult>>(json, JsonOpts);
            return envelope?.Data is { } data
                ? Result<DemandForecastResult>.Ok(data)
                : Result<DemandForecastResult>.Fail(new InvalidOperationException("Empty demand-forecast payload"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demand forecast fetch failed for {Date}", date);
            return Result<DemandForecastResult>.Fail(ex);
        }
    }

    public async Task<Result<KitchenEta>> GetKitchenEtaAsync(CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/kitchen-eta?restaurantId={restaurantId}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<KitchenEta>>(json, JsonOpts);
            return envelope?.Data is { } data
                ? Result<KitchenEta>.Ok(data)
                : Result<KitchenEta>.Fail(new InvalidOperationException("Empty kitchen-eta payload"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kitchen ETA fetch failed");
            return Result<KitchenEta>.Fail(ex);
        }
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } token
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    private sealed class ApiEnvelope<T> { public string Status { get; set; } = ""; public T? Data { get; set; } }
}
