using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class AlertsWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_shows_simulated_alerts()
    {
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance),
            new NavigationService());

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(3, vm.Alerts.Count); // MockDashboardData.Alerts() has 3 entries
    }

    [Fact]
    public void Prepend_adds_to_the_front_without_a_full_reload()
    {
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance),
            new NavigationService());
        var alert = new AlertDto(Guid.NewGuid(), "Resumen mise en place", AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now);

        vm.Prepend(alert);

        Assert.Equal("Resumen mise en place", vm.Alerts[0].Message);
    }

    [Theory]
    [InlineData("Product", "menu/products")]
    [InlineData("Order", "orders/kanban")]
    public void RowClicked_routes_by_ResourceType(string resourceType, string expectedRoute)
    {
        var nav = new NavigationService();
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);
        var alert = new AlertDto(Guid.NewGuid(), "msg", AlertSeverity.Info, null, resourceType, DateTimeOffset.Now);

        vm.RowClickedCommand.Execute(new AlertRowViewModel(alert));

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal(expectedRoute, placeholder.RouteKey);
    }
}
