using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public static class MockForecastData
{
    public static DemandForecastResult DemandForecast() => new(
        Summary: new ForecastSummary(TotalCovers: 86m, PeakHour: 21,
            TopProducts: [new TopForecastProduct(Guid.NewGuid(), "Paella Valenciana", 24m)]),
        Hourly:
        [
            new(12, 4m, 62m), new(13, 9m, 148m), new(14, 6m, 94m), new(19, 5m, 78m),
            new(20, 11m, 176m), new(21, 14m, 224m), new(22, 8m, 128m), new(23, 3m, 47m),
        ],
        Products: [new ProductForecastRow(Guid.NewGuid(), "Paella Valenciana", 24m, 26m, "Alta")]);

    public static KitchenEta KitchenEta() => new(AverageMinutes: 18.5m, SampleSize: 42);
}
