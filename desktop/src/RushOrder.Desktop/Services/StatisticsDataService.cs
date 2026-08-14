using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class StatisticsDataService
{
    private readonly AppState _state;
    private readonly ILogger<StatisticsDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public StatisticsDataService(AppState state, ILogger<StatisticsDataService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    // NOTE: backend has no payment-method breakdown endpoint at all — PaymentMethods
    // is always empty until one exists. Real routes are /sales, /products/performance,
    // /waiters/performance (not /products, /waiter as this previously called).
    public async Task<StatisticsDto> GetStatisticsAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();

            var rid = _state.CurrentRestaurant?.Id;
            var f   = Uri.EscapeDataString(from.ToDateTime(TimeOnly.MinValue).ToString("O"));
            var t2  = Uri.EscapeDataString(to.ToDateTime(TimeOnly.MaxValue).ToString("O"));
            var baseUrl = "http://localhost:5143/api/v1/analytics";

            var salesTask   = _http.GetStringAsync($"{baseUrl}/sales?restaurantId={rid}&from={f}&to={t2}&groupBy=hour", ct);
            var productTask = _http.GetStringAsync($"{baseUrl}/products/performance?restaurantId={rid}&from={f}&to={t2}", ct);
            var waiterTask  = _http.GetStringAsync($"{baseUrl}/waiters/performance?restaurantId={rid}&from={f}&to={t2}", ct);
            await Task.WhenAll(salesTask, productTask, waiterTask);

            var sales = JsonConvert.DeserializeObject<ApiEnvelope<BackendSalesDto>>(salesTask.Result)?.Data
                ?? throw new InvalidOperationException("La API de analíticas devolvió una respuesta de ventas vacía o inválida.");
            var products = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendProductPerformanceDto>>>(productTask.Result)?.Data ?? [];
            var waiters  = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendWaiterPerformanceDto>>>(waiterTask.Result)?.Data ?? [];

            return StatisticsMapper.Map(from, to, sales, products, waiters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron obtener las estadísticas de la API para {From}-{To}", from, to);
            throw;
        }
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;
    }
}
