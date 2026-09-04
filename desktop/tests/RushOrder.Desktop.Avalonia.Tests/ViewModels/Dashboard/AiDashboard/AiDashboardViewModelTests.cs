using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class AiDashboardViewModelTests
{
    [Fact]
    public async Task KitchenAlert_patches_only_the_AiDashboard_Alerts_widget()
    {
        var appState = new AppState();
        var dashboardData = new DashboardDataService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance);
        var forecastData = new ForecastDataService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance);
        var nav = new NavigationService();
        var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);

        var vm = new AiDashboardViewModel(
            new TodayForecastWidgetViewModel(forecastData),
            new SuggestionOfTheDayWidgetViewModel(forecastData),
            new AlertsWidgetViewModel(dashboardData, nav),
            new KitchenEtaWidgetViewModel(forecastData),
            realTime);

        await vm.OnKitchenAlertForTest("Horno 2 fuera de servicio", "Critical");

        Assert.Equal("Horno 2 fuera de servicio", vm.Alerts.Alerts[0].Message);
    }
}
