using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;
using Color = System.Drawing.Color;

namespace RushOrder.Desktop.Views.Forecast;

public sealed class DemandForecastControl : UserControl
{
    private readonly ForecastDataService _data;
    private readonly ThemeManager _theme;

    private Button _btnToday = null!;
    private Button _btnTomorrow = null!;
    private Button _btnNextWeek = null!;
    private Label _lblSummary = null!;
    private Label _lblLoading = null!;
    private CartesianChart _hourlyChart = null!;
    private DataGridView _productsGrid = null!;

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);

    public DemandForecastControl(ForecastDataService data, ThemeManager theme)
    {
        _data = data;
        _theme = theme;

        BackColor = theme.Colors.Background;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        Build();
        Load += async (_, _) => await RefreshAsync();
    }

    private void Build()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = _theme.Colors.HeaderBg,
            Padding = new Padding(12, 10, 12, 10),
        };

        _btnToday = MakeDateButton("Hoy", 0);
        _btnTomorrow = MakeDateButton("Mañana", 1);
        _btnNextWeek = MakeDateButton("Próxima semana", 7);
        _btnToday.Location = new Point(0, 4);
        _btnTomorrow.Location = new Point(90, 4);
        _btnNextWeek.Location = new Point(200, 4);

        _lblSummary = new Label
        {
            AutoSize = false,
            Location = new Point(360, 4),
            Size = new Size(600, 34),
            Font = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        toolbar.Controls.AddRange([_btnToday, _btnTomorrow, _btnNextWeek, _lblSummary]);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = _theme.Colors.Background,
        };

        _hourlyChart = new CartesianChart { Dock = DockStyle.Fill };

        _productsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = _theme.Colors.Surface,
            BorderStyle = BorderStyle.None,
        };
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = "Producto", DataPropertyName = "Name", FillWeight = 45 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = "Previsto (uds)", DataPropertyName = "PredictedQuantityDisplay", FillWeight = 20 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = "Preparar (uds)", DataPropertyName = "RecommendedPrepQuantityDisplay", FillWeight = 20 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
        { HeaderText = "Confianza", DataPropertyName = "ConfidenceDisplay", FillWeight = 15 });

        split.Panel1.Controls.Add(_hourlyChart);
        split.Panel2.Controls.Add(_productsGrid);

        _lblLoading = new Label
        {
            Text = "Cargando previsión…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            Visible = false,
        };

        Controls.Add(split);
        Controls.Add(_lblLoading);
        Controls.Add(toolbar);
        _lblLoading.BringToFront();

        // give the chart/table roughly 55/45 once the control has a real size
        HandleCreated += (_, _) => split.SplitterDistance = Math.Max(120, (int)(split.Height * 0.55));
    }

    private Button MakeDateButton(string text, int dayOffset)
    {
        var btn = new Button
        {
            Text = text,
            Width = dayOffset == 7 ? 150 : 80,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = _theme.Colors.Border;
        btn.Click += async (_, _) =>
        {
            _selectedDate = DateOnly.FromDateTime(DateTime.Today).AddDays(dayOffset);
            HighlightSelected(btn);
            await RefreshAsync();
        };
        return btn;
    }

    private void HighlightSelected(Button selected)
    {
        foreach (var btn in new[] { _btnToday, _btnTomorrow, _btnNextWeek })
            btn.BackColor = btn == selected ? _theme.Colors.Primary : _theme.Colors.Surface;
    }

    private async Task RefreshAsync()
    {
        SetLoading(true);
        var result = await _data.GetDemandForecastAsync(_selectedDate);
        SetLoading(false);

        if (result is null)
        {
            _lblSummary.Text = "No se pudo cargar la previsión de demanda.";
            _hourlyChart.Series = [];
            _productsGrid.DataSource = null;
            return;
        }

        ApplyResult(result);
    }

    private void ApplyResult(DemandForecastResult result)
    {
        var peak = result.Summary.PeakHour is { } h ? $"{h:00}:00h" : "—";
        _lblSummary.Text =
            $"Covers previstos: {Math.Round(result.Summary.TotalCovers)}   ·   Hora punta: {peak}   ·   " +
            $"Top: {string.Join(", ", result.Summary.TopProducts.Take(3).Select(p => p.Name))}";

        var hours = result.Hourly.OrderBy(h => h.Hour).ToList();
        _hourlyChart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = hours.Select(h => (double)h.PredictedOrders).ToArray(),
                Name = "Pedidos previstos",
            },
        };
        _hourlyChart.XAxes = new Axis[]
        {
            new() { Labels = hours.Select(h => $"{h.Hour}:00").ToArray(), LabelsRotation = -45, TextSize = 9 },
        };
        _hourlyChart.YAxes = new Axis[]
        {
            new() { Labeler = v => $"{v:N0}", TextSize = 9 },
        };

        var rows = result.Products
            .Select(p => new ProductForecastRowView(
                p.Name,
                $"{Math.Round(p.PredictedQuantity)}",
                $"{Math.Round(p.RecommendedPrepQuantity)}",
                ConfidenceLabel(p.Confidence)))
            .ToList();
        _productsGrid.DataSource = rows;
    }

    private static string ConfidenceLabel(string confidence) => confidence switch
    {
        "High" => "🟢 Alta",
        "Medium" => "🟡 Media",
        _ => "🔴 Baja — sin histórico suficiente",
    };

    private void SetLoading(bool loading)
    {
        _lblLoading.Visible = loading;
        if (loading) _lblLoading.BringToFront();
    }

    // Flat view-model for the grid — DataGridView data-binds by property name.
    private sealed record ProductForecastRowView(
        string Name, string PredictedQuantityDisplay, string RecommendedPrepQuantityDisplay, string ConfidenceDisplay);
}
