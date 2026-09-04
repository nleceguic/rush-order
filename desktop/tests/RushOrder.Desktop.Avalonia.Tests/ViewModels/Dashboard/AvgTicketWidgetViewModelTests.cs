using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class AvgTicketWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_data()
    {
        var vm = new AvgTicketWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(MockDashboardData.Kpi().AvgTicketToday, vm.Today);
    }
}
