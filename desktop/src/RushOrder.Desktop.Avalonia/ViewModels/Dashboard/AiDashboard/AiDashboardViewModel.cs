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

        _realTime.KitchenAlert += OnKitchenAlertForTest;
        _realTime.MiseEnPlaceAlert += async message =>
        {
            Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now));
            await Task.CompletedTask;
        };

        _refreshTimer = new System.Timers.Timer(60_000) { AutoReset = true };
        _refreshTimer.Elapsed += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync() =>
        await Task.WhenAll(Forecast.InitializeAsync(), Suggestion.InitializeAsync(), Alerts.InitializeAsync(), Eta.InitializeAsync());

    /// <summary>Also the production <c>KitchenAlert</c> handler — named for the test that
    /// exercises it directly since <see cref="RealTimeService"/>'s SignalR connection isn't
    /// started in unit tests (same pattern as <c>DashboardViewModel</c>'s Task 21 test hook).</summary>
    internal Task OnKitchenAlertForTest(string message, string severity)
    {
        var sev = Enum.TryParse<Models.AlertSeverity>(severity, true, out var s) ? s : Models.AlertSeverity.Info;
        Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, sev, null, "Order", DateTimeOffset.Now));
        return Task.CompletedTask;
    }

    public void Dispose() => _refreshTimer.Dispose();
}
