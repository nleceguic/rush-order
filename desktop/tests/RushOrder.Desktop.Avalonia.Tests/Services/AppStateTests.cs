using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class AppStateTests
{
    [Fact]
    public void SetOnlineStatus_does_not_raise_event_when_value_is_unchanged()
    {
        var state = new AppState();
        var raiseCount = 0;
        state.OnlineStatusChanged += _ => raiseCount++;

        state.SetOnlineStatus(false); // already false by default — no change
        state.SetOnlineStatus(true);  // change — raises
        state.SetOnlineStatus(true);  // no change — does not raise

        Assert.Equal(1, raiseCount);
    }
}
