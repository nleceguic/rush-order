using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Dashboard.Widgets;

namespace RushOrder.Desktop.Views.AiDashboard.Widgets;

internal sealed class TodayForecastWidget : KpiWidget
{
    private CartesianChart _chart = null!;

    public TodayForecastWidget(ThemeManager theme) : base(theme, "Previsión de hoy") { }

    protected override void BuildContent(Panel container)
    {
        _chart = new CartesianChart { Dock = DockStyle.Fill, Margin = new Padding(4) };
        container.Controls.Add(_chart);
    }

    public void Update(IReadOnlyList<HourlyForecastPoint> hourly)
    {
        if (InvokeRequired) { Invoke(() => Update(hourly)); return; }

        var ordered = hourly.OrderBy(h => h.Hour).ToList();
        _chart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = ordered.Select(h => (double)h.PredictedOrders).ToArray(),
                Name = "Pedidos",
            },
        };
        _chart.XAxes = new Axis[]
        {
            new() { Labels = ordered.Select(h => $"{h.Hour}h").ToArray(), TextSize = 7, LabelsRotation = -45 },
        };
        _chart.YAxes = new Axis[] { new() { TextSize = 7 } };
    }
}
