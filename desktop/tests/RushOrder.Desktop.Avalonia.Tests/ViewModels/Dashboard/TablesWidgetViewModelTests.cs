using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class TablesWidgetViewModelTests
{
    [Fact]
    public void NavigateCommand_navigates_to_tables_floorplan()
    {
        var nav = new NavigationService();
        var vm = new TablesWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);

        vm.NavigateCommand.Execute(null);

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("tables/floorplan", placeholder.RouteKey);
    }
}
