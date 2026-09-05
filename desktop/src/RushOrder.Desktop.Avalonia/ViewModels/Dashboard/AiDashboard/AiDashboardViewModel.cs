using Avalonia.Threading;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed class AiDashboardViewModel : IDisposable
{
    private readonly RealTimeService _realTime;
    private readonly System.Timers.Timer _refreshTimer;

    public TodayForecastWidgetViewModel Forecast { get; }
    public SuggestionOfTheDayWidgetViewModel Suggestion { get; }
    public AlertsWidgetViewModel Alerts { get; }
    public KitchenEtaWidgetViewModel Eta { get; }

    public AiDashboardViewModel(
        TodayForecastWidgetViewModel forecast, SuggestionOfTheDayWidgetViewModel suggestion,
        AlertsWidgetViewModel alerts, KitchenEtaWidgetViewModel eta, RealTimeService realTime)
    {
        Forecast = forecast; Suggestion = suggestion; Alerts = alerts; Eta = eta;
        _realTime = realTime;

        _realTime.KitchenAlert += OnKitchenAlert;
        _realTime.MiseEnPlaceAlert += OnMiseEnPlaceAlert;

        _refreshTimer = new System.Timers.Timer(60_000) { AutoReset = true };
        _refreshTimer.Elapsed += (_, _) => Dispatcher.UIThread.InvokeAsync(RefreshAllAsync);
        _refreshTimer.Start();

        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync() =>
        await Task.WhenAll(Forecast.InitializeAsync(), Suggestion.InitializeAsync(), Alerts.InitializeAsync(), Eta.InitializeAsync());

    /// <summary>The production <c>KitchenAlert</c> handler — also called directly by the unit
    /// test, since <see cref="RealTimeService"/>'s SignalR connection isn't started in unit tests
    /// (same pattern as <c>DashboardViewModel</c>'s Task 21 test hook). Kept as a named method
    /// (not an inline lambda) so <see cref="Dispose"/> can unsubscribe it by reference.</summary>
    internal async Task OnKitchenAlert(string message, string severity) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var sev = Enum.TryParse<Models.AlertSeverity>(severity, true, out var s) ? s : Models.AlertSeverity.Info;
            Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, sev, null, "Order", DateTimeOffset.Now));
        });

    /// <summary>Named for the same reason as <see cref="OnKitchenAlert"/> — <see cref="Dispose"/>
    /// needs a stable delegate reference to unsubscribe.</summary>
    private async Task OnMiseEnPlaceAlert(string message) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now));
        });

    public void Dispose()
    {
        _realTime.KitchenAlert -= OnKitchenAlert;
        _realTime.MiseEnPlaceAlert -= OnMiseEnPlaceAlert;
        _refreshTimer.Dispose();
    }
}
