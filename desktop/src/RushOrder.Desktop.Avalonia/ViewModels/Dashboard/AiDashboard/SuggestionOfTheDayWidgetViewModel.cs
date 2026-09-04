using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class SuggestionOfTheDayWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private string? _productName;
    [ObservableProperty] private string _detailText = "";

    public SuggestionOfTheDayWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = await _forecast.GetDemandForecastAsync(today);
        var summary = result.IsSuccess ? result.Value!.Summary : MockForecastData.DemandForecast().Summary;
        var top = summary.TopProducts.FirstOrDefault();

        ProductName = top?.Name;
        DetailText = top is not null ? $"~{Math.Round(top.PredictedQuantity)} unidades previstas hoy — destácalo" : "";

        if (result.IsSuccess)
        {
            State = top is null ? WidgetLoadState.Empty : WidgetLoadState.Loaded; // "sin previsión (histórico insuficiente)"
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
