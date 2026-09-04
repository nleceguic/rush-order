using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class DashboardDataServiceTests
{
    [Fact]
    public async Task GetKpiAsync_returns_Fail_when_the_backend_is_unreachable()
    {
        // No backend listening on localhost:5143 in the test environment.
        var service = new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance);

        var result = await service.GetKpiAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
