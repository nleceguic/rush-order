using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

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

    public async Task<DemandForecastResult?> GetDemandForecastAsync(DateOnly date, CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5000/api/v1/analytics/demand-forecast?restaurantId={restaurantId}&date={date:yyyy-MM-dd}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<DemandForecastResult>>(json, JsonOpts);
            return envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demand forecast API unavailable for {Date}", date);
            return null;
        }
    }

    public async Task<KitchenEta?> GetKitchenEtaAsync(CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5000/api/v1/analytics/kitchen-eta?restaurantId={restaurantId}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<KitchenEta>>(json, JsonOpts);
            return envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kitchen ETA API unavailable");
            return null;
        }
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } token
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    private sealed class ApiEnvelope<T>
    {
        public string Status { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}
