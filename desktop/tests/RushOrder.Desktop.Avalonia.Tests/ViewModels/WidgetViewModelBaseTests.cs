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
            // Mirrors the real widgets' shape (Fix 1): BeginLoad() first, State settled to its
            // final value at the end — the same pattern all 9 production widgets follow.
            BeginLoad();
            LoadCount++;
            if (OnLoad is not null) await OnLoad();
            State = WidgetLoadState.Loaded;
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

    /// <summary>Fix 1 regression test: a periodic refresh (the 30s/60s dashboard timer calling
    /// <see cref="WidgetViewModelBase.InitializeAsync"/> again) must not blank an
    /// already-loaded widget back to <see cref="WidgetLoadState.Loading"/> while its background
    /// fetch is in flight — that's the "Cargando…" flicker the reviewer flagged. This test
    /// would fail without the <c>BeginLoad()</c> guard: the old unconditional
    /// <c>State = WidgetLoadState.Loading;</c> would flip <see cref="WidgetViewModelBase.State"/>
    /// synchronously the instant the second <c>InitializeAsync()</c> call starts, before the
    /// gated fetch below ever resolves.</summary>
    [Fact]
    public async Task Second_InitializeAsync_call_does_not_reset_State_to_Loading_mid_flight()
    {
        var vm = new FakeWidgetViewModel();

        // First load (the very first fetch): completes immediately and settles on Loaded.
        await vm.InitializeAsync();
        Assert.Equal(WidgetLoadState.Loaded, vm.State);

        // Second load (simulating a periodic refresh tick): gate the fetch so we can inspect
        // State while it's still in flight, before it resolves.
        var gate = new TaskCompletionSource();
        vm.OnLoad = () => gate.Task;
        var secondLoad = vm.InitializeAsync();

        // The widget already has content to show — State must stay Loaded, not blank to
        // Loading, while this background refresh is still pending.
        Assert.Equal(WidgetLoadState.Loaded, vm.State);

        gate.SetResult();
        await secondLoad;

        Assert.Equal(WidgetLoadState.Loaded, vm.State);
        Assert.Equal(2, vm.LoadCount);
    }
}
