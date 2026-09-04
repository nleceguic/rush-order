using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class DashboardViewModelTests
{
    [Fact]
    public async Task TableStatusChanged_patches_only_TablesOccupied_not_other_widgets()
    {
        var appState = new AppState();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance;
        var data = new DashboardDataService(appState, logger);
        var nav = new NavigationService();
        var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);

        var vm = new DashboardViewModel(
            new RevenueWidgetViewModel(data),
            new ActiveOrdersWidgetViewModel(data, nav),
            new TablesWidgetViewModel(data, nav),
            new AvgTicketWidgetViewModel(data),
            new AlertsWidgetViewModel(data, nav),
            new ReservationsWidgetViewModel(data),
            realTime);

        // The constructor kicks off an initial refresh as fire-and-forget (see spec: the 30s
        // timer's first tick must not block construction), so it isn't guaranteed to have
        // populated Tables.Occupied/Total by the time this line runs. Await it directly here
        // (test-only synchronization, not part of the real-time patch path under test) so the
        // TablesWidgetViewModel is in its settled (fallback-to-mock, since no backend is
        // running in this unit test) state before we capture the baseline.
        await vm.Tables.InitializeAsync();

        var occupiedBefore = vm.Tables.Occupied;
        var revenueBefore = vm.Revenue.RevenueToday;

        await vm.OnTableStatusChangedForTest("table-1", "Occupied");

        Assert.NotEqual(occupiedBefore, vm.Tables.Occupied); // patched
        Assert.Equal(revenueBefore, vm.Revenue.RevenueToday); // untouched
    }
}
