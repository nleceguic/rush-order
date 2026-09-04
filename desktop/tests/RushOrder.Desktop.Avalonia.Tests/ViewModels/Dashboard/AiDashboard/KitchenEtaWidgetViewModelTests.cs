using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class KitchenEtaWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_shows_simulated_eta()
    {
        var vm = new KitchenEtaWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.Equal("19 min", vm.AverageMinutesText); // Math.Round(18.5m) == 18... see Step 3 rounding note
    }
}
