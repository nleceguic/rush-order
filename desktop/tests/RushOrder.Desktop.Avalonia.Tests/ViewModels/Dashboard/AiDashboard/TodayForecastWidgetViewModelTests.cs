using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class TodayForecastWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_series()
    {
        var vm = new TodayForecastWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(MockForecastData.DemandForecast().Hourly.Count, vm.XLabels.Length);
    }
}
