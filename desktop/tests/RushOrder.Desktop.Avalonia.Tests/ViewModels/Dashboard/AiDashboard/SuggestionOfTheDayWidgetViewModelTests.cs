using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class SuggestionOfTheDayWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_shows_the_top_simulated_product()
    {
        var vm = new SuggestionOfTheDayWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.Equal("Paella Valenciana", vm.ProductName);
    }
}
