using Avalonia.Headless.XUnit;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class RevenueWidgetViewModelTests
{
    [AvaloniaFact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_data()
    {
        var vm = new RevenueWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance));

        await vm.InitializeAsync();

        // RevenueToday is animated via KpiValueTransition over 500ms (fire-and-forget from
        // LoadAsync — see Task 15 brief); give it time to settle on the target value before
        // asserting. Requires [AvaloniaFact] (not [Fact]): the DispatcherTimer driving the
        // animation only ticks with a running Avalonia dispatcher, which a bare xunit host
        // does not provide — see AvaloniaTestSetup.cs.
        await Task.Delay(600);

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal((double)MockDashboardData.Kpi().RevenueToday, vm.RevenueToday, precision: 2);
    }
}
