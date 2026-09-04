using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard;

public partial class TodayForecastWidgetView : UserControl
{
    private readonly LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart _chart;
    private TodayForecastWidgetViewModel? _subscribedVm;

    public TodayForecastWidgetView()
    {
        InitializeComponent();
        _chart = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>("Chart")!;
        DataContextChanged += (_, _) =>
        {
            if (_subscribedVm is not null)
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;

            _subscribedVm = DataContext as TodayForecastWidgetViewModel;

            if (_subscribedVm is not null)
            {
                _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateXAxes(_subscribedVm);
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TodayForecastWidgetViewModel.XLabels) && sender is TodayForecastWidgetViewModel vm)
            UpdateXAxes(vm);
    }

    private void UpdateXAxes(TodayForecastWidgetViewModel vm) =>
        _chart.XAxes = [new Axis { Labels = vm.XLabels, TextSize = 9, LabelsRotation = -45 }];

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
