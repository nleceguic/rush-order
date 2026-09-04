using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Animations;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class RevenueWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private IDisposable? _counterAnimation;

    [ObservableProperty] private double _revenueToday;
    [ObservableProperty] private string _deltaText = "—";
    [ObservableProperty] private bool _deltaIsUp;
    [ObservableProperty] private IReadOnlyList<decimal> _hourly = [];

    public RevenueWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();

        if (result.IsSuccess)
        {
            ApplyKpi(result.Value!);
            State = WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            ApplyKpi(MockDashboardData.Kpi());
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    private void ApplyKpi(DashboardKpi kpi)
    {
        var target = (double)kpi.RevenueToday;
        _counterAnimation?.Dispose();
        _counterAnimation = KpiValueTransition.Animate(RevenueToday, target, TimeSpan.FromMilliseconds(500),
            value => RevenueToday = value);

        var up = kpi.RevenueToday >= kpi.RevenueYesterday;
        var pct = kpi.RevenueYesterday != 0
            ? Math.Abs((double)((kpi.RevenueToday - kpi.RevenueYesterday) / kpi.RevenueYesterday * 100))
            : 0;
        DeltaIsUp = up;
        DeltaText = $"{(up ? "▲" : "▼")} {pct:F1}% vs. ayer";
        Hourly = kpi.RevenueByHour;
    }
}
