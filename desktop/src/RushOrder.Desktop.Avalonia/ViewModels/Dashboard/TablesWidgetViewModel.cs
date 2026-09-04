using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class TablesWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    [ObservableProperty] private int _occupied;
    [ObservableProperty] private int _total;
    [ObservableProperty] private double _avgOccupancyMinutes;

    public TablesWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Occupied = kpi.TablesOccupied;
        Total = kpi.TablesTotal;
        AvgOccupancyMinutes = kpi.AvgOccupancyMinutes;

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
    private void Navigate() => _nav.NavigateTo("tables/floorplan");
}
