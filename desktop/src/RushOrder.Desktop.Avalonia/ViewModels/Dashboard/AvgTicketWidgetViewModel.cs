using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class AvgTicketWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;

    [ObservableProperty] private decimal _today;
    [ObservableProperty] private decimal _yesterday;
    [ObservableProperty] private string _deltaText = "";
    [ObservableProperty] private bool _deltaIsUp;

    public AvgTicketWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        BeginLoad();
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Today = kpi.AvgTicketToday;
        Yesterday = kpi.AvgTicketYesterday;
        DeltaIsUp = Today >= Yesterday;
        var pct = Yesterday != 0 ? Math.Abs((double)((Today - Yesterday) / Yesterday * 100)) : 0;
        DeltaText = $"{(DeltaIsUp ? "▲" : "▼")} {pct:F1}% vs. ayer (€ {Yesterday:N2})";

        if (result.IsSuccess)
        {
            State = WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
