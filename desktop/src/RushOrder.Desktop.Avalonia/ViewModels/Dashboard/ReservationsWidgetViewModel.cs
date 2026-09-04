using System.Collections.ObjectModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class ReservationsWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;

    public ObservableCollection<ReservationRowViewModel> Reservations { get; } = [];

    public ReservationsWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetUpcomingReservationsAsync();
        var reservations = result.IsSuccess ? result.Value! : MockDashboardData.Reservations();

        Reservations.Clear();
        foreach (var r in reservations) Reservations.Add(new ReservationRowViewModel(r));

        if (result.IsSuccess)
        {
            State = reservations.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
