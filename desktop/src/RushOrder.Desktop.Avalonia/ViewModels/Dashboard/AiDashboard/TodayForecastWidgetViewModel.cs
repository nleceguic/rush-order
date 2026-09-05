using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class TodayForecastWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private ISeries[] _series = [];
    [ObservableProperty] private string[] _xLabels = [];

    public TodayForecastWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        BeginLoad();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = await _forecast.GetDemandForecastAsync(today);
        var hourly = result.IsSuccess ? result.Value!.Hourly : MockForecastData.DemandForecast().Hourly;

        var ordered = hourly.OrderBy(h => h.Hour).ToList();
        Series = [new ColumnSeries<double> { Values = ordered.Select(h => (double)h.PredictedOrders).ToArray(), Name = "Pedidos" }];
        XLabels = ordered.Select(h => $"{h.Hour}h").ToArray();

        if (result.IsSuccess)
        {
            State = hourly.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded; // "histórico insuficiente"
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
