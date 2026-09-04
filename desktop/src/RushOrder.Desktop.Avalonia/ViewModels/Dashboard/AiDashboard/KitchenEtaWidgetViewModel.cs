using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class KitchenEtaWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private string _averageMinutesText = "—";
    [ObservableProperty] private string _captionText = "sin datos todavía";

    public KitchenEtaWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _forecast.GetKitchenEtaAsync();
        var eta = result.IsSuccess ? result.Value! : MockForecastData.KitchenEta();

        if (eta.AverageMinutes is { } minutes)
        {
            AverageMinutesText = $"{Math.Round(minutes, MidpointRounding.AwayFromZero)} min";
            CaptionText = $"últimos {eta.SampleSize} pedidos";
        }
        else
        {
            AverageMinutesText = "—";
            CaptionText = "sin datos todavía";
        }

        if (result.IsSuccess)
        {
            State = eta.AverageMinutes is null ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
