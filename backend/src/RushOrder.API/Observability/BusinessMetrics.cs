using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RushOrder.API.Observability;

public interface IBusinessMetrics
{
    void RecordOrderPlaced(string restaurantId, string source);
    void RecordOrderTimeToKitchen(double milliseconds, string restaurantId);
    void SetMenuCacheHitRate(string restaurantId, double rate);
    void SetPaymentSuccessRate(string restaurantId, double rate);
    void SetSignalRConnectionCount(string restaurantId, int count);
}

public sealed class BusinessMetrics : IBusinessMetrics, IDisposable
{
    public const string MeterName = "RushOrder.Business";

    private readonly Meter _meter;
    private readonly Counter<long> _ordersPlaced;
    private readonly Histogram<double> _orderTimeToKitchen;
    private readonly ConcurrentDictionary<string, double> _cacheHitRates    = new();
    private readonly ConcurrentDictionary<string, double> _paymentRates     = new();
    private readonly ConcurrentDictionary<string, int>    _signalRConnections = new();

    public BusinessMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName, "1.0");

        _ordersPlaced = _meter.CreateCounter<long>(
            "orders.placed",
            description: "Total orders placed, tagged by restaurant and source");

        _orderTimeToKitchen = _meter.CreateHistogram<double>(
            "orders.time_to_kitchen_ms",
            unit: "ms",
            description: "Elapsed milliseconds from order placement to KDS receipt");

        _meter.CreateObservableGauge(
            "menu.cache.hit_rate",
            () => _cacheHitRates.Select(kv =>
                new Measurement<double>(kv.Value,
                    new KeyValuePair<string, object?>("restaurant_id", kv.Key))),
            description: "Menu Redis cache hit-rate per restaurant (0-1)");

        _meter.CreateObservableGauge(
            "payment.success_rate",
            () => _paymentRates.Select(kv =>
                new Measurement<double>(kv.Value,
                    new KeyValuePair<string, object?>("restaurant_id", kv.Key))),
            description: "Payment success rate per restaurant (0-1)");

        _meter.CreateObservableGauge(
            "signalr.connections.active",
            () => _signalRConnections.Select(kv =>
                new Measurement<double>(kv.Value,
                    new KeyValuePair<string, object?>("restaurant_id", kv.Key))),
            description: "Active SignalR connections per restaurant");
    }

    public void RecordOrderPlaced(string restaurantId, string source) =>
        _ordersPlaced.Add(1,
            new KeyValuePair<string, object?>("restaurant_id", restaurantId),
            new KeyValuePair<string, object?>("source",        source));

    public void RecordOrderTimeToKitchen(double milliseconds, string restaurantId) =>
        _orderTimeToKitchen.Record(milliseconds,
            new KeyValuePair<string, object?>("restaurant_id", restaurantId));

    public void SetMenuCacheHitRate(string restaurantId, double rate)       => _cacheHitRates[restaurantId]      = rate;
    public void SetPaymentSuccessRate(string restaurantId, double rate)     => _paymentRates[restaurantId]       = rate;
    public void SetSignalRConnectionCount(string restaurantId, int count)   => _signalRConnections[restaurantId] = count;

    public void Dispose() => _meter.Dispose();
}
