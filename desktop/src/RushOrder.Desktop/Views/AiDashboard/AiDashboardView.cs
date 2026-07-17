using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.AiDashboard.Widgets;
using RushOrder.Desktop.Views.Dashboard.Widgets;

namespace RushOrder.Desktop.Views.AiDashboard;

// "Dashboard de IA" — Previsión de hoy, Sugerencia del día, Alertas de IA
// (anomaly detection — shares AlertsWidget/AlertMonitoringService with the
// main Dashboard, since the 3 new anomaly rules flow through the same
// AlertTriggered pipeline), and ETA medio de cocina.
public sealed class AiDashboardView : UserControl
{
    private readonly ForecastDataService _forecast;
    private readonly DashboardDataService _dashboardData;
    private readonly RealTimeService _realTime;
    private readonly ThemeManager _theme;

    private TodayForecastWidget _wForecast = null!;
    private SuggestionOfTheDayWidget _wSuggestion = null!;
    private AlertsWidget _wAlerts = null!;
    private KitchenEtaWidget _wEta = null!;

    private readonly System.Windows.Forms.Timer _refreshTimer;

    public AiDashboardView(
        ForecastDataService forecast, DashboardDataService dashboardData, RealTimeService realTime, ThemeManager theme)
    {
        _forecast = forecast;
        _dashboardData = dashboardData;
        _realTime = realTime;
        _theme = theme;

        Dock = DockStyle.Fill;
        BackColor = theme.Colors.Background;
        DoubleBuffered = true;

        BuildGrid();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += async (_, _) => await LoadDataAsync();

        Load += async (_, _) => await OnFirstLoadAsync();
        VisibleChanged += (_, _) => { if (Visible) _refreshTimer.Start(); else _refreshTimer.Stop(); };

        _realTime.KitchenAlert += async (_, _) => _wAlerts.Update(await _dashboardData.GetAlertsAsync());
        _realTime.MiseEnPlaceAlert += async _ => _wAlerts.Update(await _dashboardData.GetAlertsAsync());
    }

    private void BuildGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10),
            BackColor = Color.Transparent,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));

        _wForecast = new TodayForecastWidget(_theme);
        _wSuggestion = new SuggestionOfTheDayWidget(_theme);
        _wAlerts = new AlertsWidget(_theme);
        _wEta = new KitchenEtaWidget(_theme);

        grid.Controls.Add(_wForecast, 0, 0);
        grid.Controls.Add(_wAlerts, 1, 0);
        grid.Controls.Add(_wSuggestion, 0, 1);
        grid.Controls.Add(_wEta, 1, 1);

        Controls.Add(grid);
    }

    private async Task OnFirstLoadAsync()
    {
        await LoadDataAsync();
        _refreshTimer.Start();
    }

    private async Task LoadDataAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var forecastTask = _forecast.GetDemandForecastAsync(today);
        var etaTask = _forecast.GetKitchenEtaAsync();
        var alertsTask = _dashboardData.GetAlertsAsync();
        await Task.WhenAll(forecastTask, etaTask, alertsTask);

        if (forecastTask.Result is { } forecast)
        {
            _wForecast.Update(forecast.Hourly);
            var top = forecast.Summary.TopProducts.FirstOrDefault();
            _wSuggestion.Update(top?.Name, top?.PredictedQuantity ?? 0);
        }

        if (etaTask.Result is { } eta)
            _wEta.Update(eta.AverageMinutes, eta.SampleSize);

        _wAlerts.Update(alertsTask.Result);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshTimer.Dispose();
        base.Dispose(disposing);
    }
}
