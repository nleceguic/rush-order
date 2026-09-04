using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class AlertsWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    public ObservableCollection<AlertRowViewModel> Alerts { get; } = [];

    public AlertsWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetAlertsAsync();
        var alerts = result.IsSuccess ? result.Value! : MockDashboardData.Alerts();

        Alerts.Clear();
        foreach (var alert in alerts) Alerts.Add(new AlertRowViewModel(alert));

        if (result.IsSuccess)
        {
            State = alerts.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    /// <summary>Real-time entry point — <c>KitchenAlert</c>/<c>MiseEnPlaceAlert</c> call this
    /// directly instead of triggering <see cref="WidgetViewModelBase.InitializeAsync"/>, so a
    /// live alert never re-fetches or touches <see cref="WidgetViewModelBase.State"/>.</summary>
    public void Prepend(AlertDto alert) => Alerts.Insert(0, new AlertRowViewModel(alert));

    [RelayCommand]
    private void RowClicked(AlertRowViewModel row)
    {
        var routeKey = row.Source.ResourceType switch
        {
            "Product" => "menu/products",
            "Order" => "orders/kanban",
            _ => null, // "Reservation"/"mise_en_place" have no target yet — same as WinForms
        };
        if (routeKey is not null) _nav.NavigateTo(routeKey, row.Source.ResourceId);
    }
}
