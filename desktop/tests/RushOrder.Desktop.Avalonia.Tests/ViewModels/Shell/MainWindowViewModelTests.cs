using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.ViewModels.Shell;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Shell;

public class MainWindowViewModelTests
{
    [Fact]
    public void SelectNav_navigates_to_the_item_route_and_updates_ActiveRouteKey()
    {
        var nav = new NavigationService();
        var vm = new MainWindowViewModel(nav);
        var dashboardItem = vm.NavItems.Single(i => i.RouteKey == "dashboard");

        vm.SelectNavCommand.Execute(dashboardItem);

        Assert.Equal("dashboard", vm.ActiveRouteKey);
        Assert.IsType<PlaceholderViewModel>(vm.CurrentContent);
    }

    [Fact]
    public void NavItems_contains_all_10_sidebar_entries_matching_WinForms_MainForm()
    {
        var vm = new MainWindowViewModel(new NavigationService());

        Assert.Equal(10, vm.NavItems.Count);
        Assert.Contains(vm.NavItems, i => i.RouteKey == "dashboard");
        Assert.Contains(vm.NavItems, i => i.RouteKey == "panel-ia");
    }
}
