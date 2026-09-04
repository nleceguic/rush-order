using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class DashboardViewModel : IDisposable
{
    private readonly RealTimeService _realTime;
    private readonly System.Timers.Timer _refreshTimer;

    public RevenueWidgetViewModel Revenue { get; }
    public ActiveOrdersWidgetViewModel Orders { get; }
    public TablesWidgetViewModel Tables { get; }
    public AvgTicketWidgetViewModel Ticket { get; }
    public AlertsWidgetViewModel Alerts { get; }
    public ReservationsWidgetViewModel Reservations { get; }

    public DashboardViewModel(
        RevenueWidgetViewModel revenue, ActiveOrdersWidgetViewModel orders, TablesWidgetViewModel tables,
        AvgTicketWidgetViewModel ticket, AlertsWidgetViewModel alerts, ReservationsWidgetViewModel reservations,
        RealTimeService realTime)
    {
        Revenue = revenue; Orders = orders; Tables = tables;
        Ticket = ticket; Alerts = alerts; Reservations = reservations;
        _realTime = realTime;

        WireRealTime();

        _refreshTimer = new System.Timers.Timer(30_000) { AutoReset = true };
        _refreshTimer.Elapsed += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await Task.WhenAll(
            Revenue.InitializeAsync(), Orders.InitializeAsync(), Tables.InitializeAsync(),
            Ticket.InitializeAsync(), Alerts.InitializeAsync(), Reservations.InitializeAsync());
    }

    // Real-time routing table — spec Section 3. Each handler touches exactly one widget's
    // properties directly; none re-fetches or calls InitializeAsync(). Each handler is a named
    // method (not an inline lambda) so Dispose() can unsubscribe it by reference — an anonymous
    // lambda's delegate instance can't be recovered later for -=.
    private void WireRealTime()
    {
        _realTime.OrderReceived += OnOrderReceived;
        _realTime.TableStatusChanged += OnTableStatusChanged;
        _realTime.KitchenAlert += OnKitchenAlert;
        _realTime.MiseEnPlaceAlert += OnMiseEnPlaceAlert;
    }

    private Task OnOrderReceived(RushOrder.Desktop.Core.Hubs.OrderReceivedPayload payload)
    {
        Orders.Waiting++;
        Orders.Total++;
        return Task.CompletedTask;
    }

    /// <summary>Shared <c>TableStatusChanged</c> guard logic — wired directly as the real
    /// <see cref="RealTimeService.TableStatusChanged"/> handler above (its delegate shape,
    /// <c>Func&lt;string, string, Task&gt;</c>, matches this method's signature exactly), and
    /// called directly by the unit test, since the real event is wired through
    /// <see cref="RealTimeService"/>'s SignalR connection, which isn't started in unit tests.
    /// Kept as a single method so production behavior and the test can never drift apart.</summary>
    internal Task OnTableStatusChanged(string tableId, string status)
    {
        if (status == "Occupied" && Tables.Occupied < Tables.Total) Tables.Occupied++;
        else if (status is "Free" or "Cleaning" && Tables.Occupied > 0) Tables.Occupied--;
        return Task.CompletedTask;
    }

    private Task OnKitchenAlert(string message, string severity)
    {
        var alert = new Models.AlertDto(Guid.NewGuid(), message,
            Enum.TryParse<Models.AlertSeverity>(severity, true, out var sev) ? sev : Models.AlertSeverity.Info,
            null, "Order", DateTimeOffset.Now);
        Alerts.Prepend(alert);
        return Task.CompletedTask;
    }

    private Task OnMiseEnPlaceAlert(string message)
    {
        var alert = new Models.AlertDto(Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now);
        Alerts.Prepend(alert);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _realTime.OrderReceived -= OnOrderReceived;
        _realTime.TableStatusChanged -= OnTableStatusChanged;
        _realTime.KitchenAlert -= OnKitchenAlert;
        _realTime.MiseEnPlaceAlert -= OnMiseEnPlaceAlert;
        _refreshTimer.Dispose();
    }
}
