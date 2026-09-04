using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class ActiveOrdersWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    [ObservableProperty] private int _waiting;
    [ObservableProperty] private int _preparing;
    [ObservableProperty] private int _ready;
    [ObservableProperty] private int _total;

    public ActiveOrdersWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Waiting = kpi.OrdersWaiting;
        Preparing = kpi.OrdersPreparing;
        Ready = kpi.OrdersReady;
        Total = Waiting + Preparing + Ready;

        if (result.IsSuccess)
        {
            State = Total == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    [RelayCommand]
    private void Navigate() => _nav.NavigateTo("orders/kanban");
}
