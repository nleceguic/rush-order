using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class ActiveOrdersWidgetViewModelTests
{
    [Fact]
    public void NavigateCommand_navigates_to_orders_kanban()
    {
        var nav = new NavigationService();
        var vm = new ActiveOrdersWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);

        vm.NavigateCommand.Execute(null);

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("orders/kanban", placeholder.RouteKey);
    }
}
