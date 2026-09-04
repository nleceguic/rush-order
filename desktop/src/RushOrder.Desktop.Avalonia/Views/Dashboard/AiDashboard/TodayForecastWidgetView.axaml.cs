using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard;

public partial class TodayForecastWidgetView : UserControl
{
    private readonly LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart _chart;

    public TodayForecastWidgetView()
    {
        InitializeComponent();
        _chart = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>("Chart")!;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TodayForecastWidgetViewModel vm)
                _chart.XAxes = [new Axis { Labels = vm.XLabels, TextSize = 9, LabelsRotation = -45 }];
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
