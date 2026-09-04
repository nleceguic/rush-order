using RushOrder.Desktop.Avalonia.ViewModels;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels;

public class WidgetViewModelBaseTests
{
    private sealed class FakeWidgetViewModel : WidgetViewModelBase
    {
        public int LoadCount { get; private set; }
        public Func<Task>? OnLoad { get; set; }

        protected override async Task LoadAsync()
        {
            LoadCount++;
            if (OnLoad is not null) await OnLoad();
        }
    }

    [Fact]
    public void Starts_in_Loading_state_with_no_simulated_data()
    {
        var vm = new FakeWidgetViewModel();

        Assert.Equal(WidgetLoadState.Loading, vm.State);
        Assert.False(vm.IsShowingSimulatedData);
    }

    [Fact]
    public async Task RetryCommand_invokes_LoadAsync()
    {
        var vm = new FakeWidgetViewModel();

        await vm.RetryCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.LoadCount);
    }
}
