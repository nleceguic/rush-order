using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RushOrder.Desktop.Avalonia.ViewModels;

public abstract partial class WidgetViewModelBase : ObservableObject
{
    [ObservableProperty]
    private WidgetLoadState _state = WidgetLoadState.Loading;

    [ObservableProperty]
    private bool _isShowingSimulatedData;

    [RelayCommand]
    private async Task Retry() => await LoadAsync();

    /// <summary>Fetches from the real data source and sets <see cref="State"/> and
    /// <see cref="IsShowingSimulatedData"/>. Never sets <c>State = Loaded</c> from
    /// simulated data — see spec Section 2's behavior matrix.</summary>
    protected abstract Task LoadAsync();

    public Task InitializeAsync() => LoadAsync();
}
