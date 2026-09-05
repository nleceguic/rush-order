using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RushOrder.Desktop.Avalonia.ViewModels;

public abstract partial class WidgetViewModelBase : ObservableObject
{
    [ObservableProperty]
    private WidgetLoadState _state = WidgetLoadState.Loading;

    [ObservableProperty]
    private bool _isShowingSimulatedData;

    private bool _hasLoadedOnce;

    [RelayCommand]
    private async Task Retry() => await LoadAsync();

    /// <summary>Transitions to <see cref="WidgetLoadState.Loading"/> only on the very first
    /// load, when there's genuinely nothing displayed yet. On any subsequent periodic refresh
    /// this is a no-op, so a widget already showing real or simulated content stays visible
    /// while the background fetch is in flight instead of blanking to "Cargando…".</summary>
    protected void BeginLoad()
    {
        if (!_hasLoadedOnce) State = WidgetLoadState.Loading;
    }

    /// <summary>Fetches from the real data source and sets <see cref="State"/> and
    /// <see cref="IsShowingSimulatedData"/>. Never sets <c>State = Loaded</c> from
    /// simulated data — see spec Section 2's behavior matrix.</summary>
    protected abstract Task LoadAsync();

    public async Task InitializeAsync()
    {
        await LoadAsync();
        _hasLoadedOnce = true;
    }
}
