using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class ForecastDataServiceTests
{
    [Fact]
    public async Task GetKitchenEtaAsync_returns_Fail_when_the_backend_is_unreachable()
    {
        var service = new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance);

        var result = await service.GetKitchenEtaAsync();

        Assert.False(result.IsSuccess);
    }
}
