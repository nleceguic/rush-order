using RushOrder.Desktop.Models;
using RushOrder.Desktop.Navigation;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Dashboard.Widgets;
using RushOrder.Desktop.Views.Menu;
using RushOrder.Desktop.Views.Orders;

namespace RushOrder.Desktop.Views.Dashboard;

public sealed class DashboardView : UserControl
{
    private readonly DashboardDataService _data;
    private readonly RealTimeService      _realTime;
    private readonly ThemeManager         _theme;
    private readonly NavigationService    _nav;

    private RevenueWidget      _wRevenue     = null!;
    private ActiveOrdersWidget _wOrders      = null!;
    private TablesWidget       _wTables      = null!;
    private AvgTicketWidget    _wTicket      = null!;
    private AlertsWidget       _wAlerts      = null!;
    private ReservationsWidget _wReservations = null!;

    private readonly System.Windows.Forms.Timer _refreshTimer;

    public DashboardView(DashboardDataService data, RealTimeService realTime, ThemeManager theme, NavigationService nav)
    {
        _data     = data;
        _realTime = realTime;
        _theme    = theme;
        _nav      = nav;

        Dock           = DockStyle.Fill;
        BackColor      = theme.Colors.Background;
        DoubleBuffered = true;

        BuildGrid();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _refreshTimer.Tick += async (_, _) => await LoadDataAsync();

        Load           += async (_, _) => await OnFirstLoadAsync();
        VisibleChanged += (_, _) => { if (Visible) _refreshTimer.Start(); else _refreshTimer.Stop(); };

        WireRealTime();
    }

    private void BuildGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 2,
            Padding     = new Padding(10),
            BackColor   = Color.Transparent,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        _wRevenue      = new RevenueWidget(_theme);
        _wOrders       = new ActiveOrdersWidget(_theme);
        _wTables       = new TablesWidget(_theme);
        _wTicket       = new AvgTicketWidget(_theme);
        _wAlerts       = new AlertsWidget(_theme);
        _wReservations = new ReservationsWidget(_theme);

        grid.Controls.Add(_wRevenue,      0, 0);
        grid.Controls.Add(_wOrders,       1, 0);
        grid.Controls.Add(_wTables,       2, 0);
        grid.Controls.Add(_wTicket,       0, 1);
        grid.Controls.Add(_wAlerts,       1, 1);
        grid.Controls.Add(_wReservations, 2, 1);

        Controls.Add(grid);

        _wAlerts.AlertClicked += OnAlertClicked;
    }

    // Each alert already carries a ResourceType from the backend (or MockAlerts) telling us
    // what it's about — route to the screen that's actually relevant instead of the click
    // just doing nothing, which is all it did before (AlertsWidget already set Cursor.Hand and
    // raised this event, but nothing was listening).
    private void OnAlertClicked(AlertDto alert)
    {
        switch (alert.ResourceType)
        {
            case "Product":
                _nav.ClearAndNavigateTo<MenuManagementControl>(alert.ResourceId);
                break;
            case "Order":
                _nav.ClearAndNavigateTo<OrdersView>(alert.ResourceId);
                break;
            // "Reservation" and "mise_en_place" have no dedicated screen yet — "Reservas" in
            // the sidebar is still a no-op placeholder, so there's nowhere to send these.
        }
    }

    private void WireRealTime()
    {
        _realTime.OrderReceived += payload =>
        {
            // Increment active orders
            return Task.CompletedTask;
        };

        _realTime.OrderStatusUpdated += async (_, _, _) =>
        {
            await Task.Delay(200); // slight debounce
            await LoadKpiAsync();
        };

        _realTime.TableStatusChanged += async (_, _) =>
        {
            await Task.Delay(200);
            await LoadKpiAsync();
        };

        _realTime.KitchenAlert += async (message, severity) =>
        {
            await Task.CompletedTask;
            var alerts = await _data.GetAlertsAsync();
            _wAlerts.Update(alerts);
        };

        // Daily mise en place summary (08:00) — not a persisted alert on the
        // backend, so it's prepended to whatever's currently showing rather
        // than fetched.
        _realTime.MiseEnPlaceAlert += async message =>
        {
            var miseEnPlace = new Models.AlertDto(
                Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now);
            var current = await _data.GetAlertsAsync();
            _wAlerts.Update([miseEnPlace, .. current]);
        };
    }

    private async Task OnFirstLoadAsync()
    {
        await LoadDataAsync();
        _refreshTimer.Start();

        // DEBUG: force a full repaint shortly after everything has settled, to test
        // whether the broken widgets are a stale/incomplete paint from during layout.
        var debugTimer = new System.Windows.Forms.Timer { Interval = 800 };
        debugTimer.Tick += (_, _) =>
        {
            debugTimer.Stop();
            debugTimer.Dispose();
            Invalidate(true);
        };
        debugTimer.Start();
    }

    private async Task LoadDataAsync()
    {
        var kpiTask   = _data.GetKpiAsync();
        var alertTask = _data.GetAlertsAsync();
        var resTask   = _data.GetUpcomingReservationsAsync();
        await Task.WhenAll(kpiTask, alertTask, resTask);

        ApplyKpi(kpiTask.Result);
        _wAlerts.Update(alertTask.Result);
        _wReservations.Update(resTask.Result);
    }

    private async Task LoadKpiAsync()
    {
        var kpi = await _data.GetKpiAsync();
        ApplyKpi(kpi);
    }

    private void ApplyKpi(RushOrder.Desktop.Models.DashboardKpi kpi)
    {
        _wRevenue.Update(kpi.RevenueToday, kpi.RevenueYesterday,
            kpi.RevenueByHour.Select(v => (decimal)v).ToArray());
        _wOrders.Update(kpi.OrdersWaiting, kpi.OrdersPreparing, kpi.OrdersReady);
        _wTables.Update(kpi.TablesOccupied, kpi.TablesTotal, kpi.AvgOccupancyMinutes);
        _wTicket.Update(kpi.AvgTicketToday, kpi.AvgTicketYesterday);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshTimer.Dispose();
        base.Dispose(disposing);
    }
}
