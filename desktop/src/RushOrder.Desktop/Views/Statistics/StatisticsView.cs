using ClosedXML.Excel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;
// Disambiguate System.Drawing types from QuestPDF.Infrastructure
using Color = System.Drawing.Color;
using Size  = System.Drawing.Size;

namespace RushOrder.Desktop.Views.Statistics;

public sealed class StatisticsView : UserControl
{
    private readonly StatisticsDataService _data;
    private readonly ThemeManager          _theme;

    private DateTimePicker _dtFrom = null!;
    private DateTimePicker _dtTo   = null!;
    private Label          _lblSummary = null!;
    private Label          _lblLoading = null!;

    private CartesianChart _revenueChart  = null!;
    private CartesianChart _productsChart = null!;
    private PieChart       _paymentChart  = null!;
    private DataGridView   _waitersGrid   = null!;

    private StatisticsDto? _currentData;

    public StatisticsView(StatisticsDataService data, ThemeManager theme)
    {
        _data  = data;
        _theme = theme;

        BackColor    = theme.Colors.Background;
        Dock         = DockStyle.Fill;
        DoubleBuffered = true;

        Build();
        Load += async (_, _) => await RefreshAsync();
    }

    private void Build()
    {
        // ── Toolbar ───────────────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 52,
            BackColor = _theme.Colors.HeaderBg,
            Padding   = new Padding(12, 8, 12, 8),
        };

        var lblFrom = new Label
        {
            Text      = "Desde:",
            ForeColor = _theme.Colors.TextSecondary,
            Font      = _theme.Fonts.Regular,
            AutoSize  = true,
            Location  = new Point(12, 16),
        };

        _dtFrom = new DateTimePicker
        {
            Format   = DateTimePickerFormat.Short,
            Value    = DateTime.Today.AddDays(-6),
            Size     = new Size(110, 26),
            Location = new Point(60, 13),
        };

        var lblTo = new Label
        {
            Text      = "—",
            ForeColor = _theme.Colors.TextSecondary,
            Font      = _theme.Fonts.Regular,
            AutoSize  = true,
            Location  = new Point(176, 16),
        };

        _dtTo = new DateTimePicker
        {
            Format   = DateTimePickerFormat.Short,
            Value    = DateTime.Today,
            Size     = new Size(110, 26),
            Location = new Point(192, 13),
        };

        var btnRefresh = MakeButton("↺ Actualizar", 316, () => _ = RefreshAsync());
        var btnExcel   = MakeButton("📊 Excel",     426, ExportExcel);
        var btnPdf     = MakeButton("📄 PDF",        514, ExportPdf);

        toolbar.Controls.AddRange([lblFrom, _dtFrom, lblTo, _dtTo, btnRefresh, btnExcel, btnPdf]);

        Button MakeButton(string text, int x, Action action)
        {
            var b = new Button
            {
                Text      = text,
                Size      = new Size(92, 28),
                Location  = new Point(x, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = _theme.Colors.Primary,
                ForeColor = Color.White,
                Font      = _theme.Fonts.Small,
                Cursor    = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => action();
            return b;
        }

        // ── Summary bar ───────────────────────────────────────────────────
        var summaryBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 36,
            BackColor = Color.FromArgb(30, _theme.Colors.Primary),
        };
        _lblSummary = new Label
        {
            Text      = "Cargando estadísticas…",
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextPrimary,
            AutoSize  = true,
            Location  = new Point(16, 8),
        };
        _lblLoading = new Label
        {
            Text      = "⏳ Cargando…",
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextSecondary,
            AutoSize  = true,
            Location  = new Point(16, 8),
        };
        summaryBar.Controls.AddRange([_lblSummary, _lblLoading]);

        // ── Charts row ────────────────────────────────────────────────────
        var chartsRow = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 2,
            BackColor   = _theme.Colors.Background,
        };
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        chartsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));
        chartsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));

        // Revenue line chart
        var revenueCard = MakeCard("Ingresos por hora (hoy)");
        _revenueChart   = new CartesianChart { Dock = DockStyle.Fill };
        revenueCard.Controls.Add(_revenueChart);
        chartsRow.Controls.Add(revenueCard, 0, 0);

        // Top products bar chart
        var productsCard = MakeCard("Top 10 productos (período)");
        _productsChart   = new CartesianChart { Dock = DockStyle.Fill };
        productsCard.Controls.Add(_productsChart);
        chartsRow.Controls.Add(productsCard, 1, 0);

        // Payment donut chart
        var paymentCard = MakeCard("Métodos de pago");
        _paymentChart   = new PieChart { Dock = DockStyle.Fill };
        paymentCard.Controls.Add(_paymentChart);
        chartsRow.Controls.Add(paymentCard, 2, 0);

        // Waiters grid (spans all 3 columns)
        var waitersCard = MakeCard("Rendimiento por camarero");
        _waitersGrid = BuildWaitersGrid();
        waitersCard.Controls.Add(_waitersGrid);
        chartsRow.Controls.Add(waitersCard, 0, 1);
        chartsRow.SetColumnSpan(waitersCard, 3);

        // Compose
        Controls.Add(chartsRow);
        Controls.Add(summaryBar);
        Controls.Add(toolbar);
    }

    private Panel MakeCard(string title)
    {
        var card = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(1),
            Margin    = new Padding(4),
        };
        var lbl = new Label
        {
            Text      = title,
            Font      = PoppinsFont.New("Poppins", 8.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = _theme.Colors.HeaderBg,
        };
        card.Controls.Add(lbl);
        return card;
    }

    private DataGridView BuildWaitersGrid()
    {
        var grid = new DataGridView
        {
            Dock               = DockStyle.Fill,
            BackgroundColor    = _theme.Colors.Surface,
            BorderStyle        = BorderStyle.None,
            ReadOnly           = true,
            AllowUserToAddRows = false,
            RowHeadersVisible  = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font               = _theme.Fonts.Regular,
            DefaultCellStyle   = new DataGridViewCellStyle
            {
                BackColor = _theme.Colors.Surface,
                ForeColor = _theme.Colors.TextPrimary,
                SelectionBackColor = _theme.Colors.Primary,
                SelectionForeColor = Color.White,
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = _theme.Colors.HeaderBg,
                ForeColor = _theme.Colors.TextSecondary,
                Font      = PoppinsFont.New("Poppins", 8.5f, FontStyle.Bold),
            },
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",       HeaderText = "Camarero",        FillWeight = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Orders",     HeaderText = "Pedidos",         FillWeight = 15 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Revenue",    HeaderText = "Ingresos",        FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgTicket",  HeaderText = "Ticket medio",    FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgMinutes", HeaderText = "Tiempo medio",    FillWeight = 15 });

        return grid;
    }

    // ── Data loading ──────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        SetLoading(true);
        try
        {
            var from = DateOnly.FromDateTime(_dtFrom.Value.Date);
            var to   = DateOnly.FromDateTime(_dtTo.Value.Date);
            _currentData = await _data.GetStatisticsAsync(from, to);
            UpdateCharts(_currentData);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        if (InvokeRequired) { Invoke(() => SetLoading(loading)); return; }
        _lblLoading.Visible  = loading;
        _lblSummary.Visible  = !loading;
    }

    private void UpdateCharts(StatisticsDto dto)
    {
        if (InvokeRequired) { Invoke(() => UpdateCharts(dto)); return; }

        // Summary
        _lblSummary.Text =
            $"Período: {dto.From:dd/MM/yyyy} — {dto.To:dd/MM/yyyy}  ·  " +
            $"Total: €{dto.TotalRevenue:N2}  ·  Pedidos: {dto.TotalOrders:N0}";

        // Revenue by hour (line)
        var hourValues  = dto.HourlyRevenue.Select(h => (double)h.Revenue).ToArray();
        var hourLabels  = dto.HourlyRevenue.Select(h => $"{h.Hour}:00").ToArray();

        _revenueChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = hourValues,
                Name   = "Ingresos (€)",
                Fill   = null,
            }
        };
        _revenueChart.XAxes = new Axis[]
        {
            new() { Labels = hourLabels, LabelsRotation = -45, TextSize = 9 }
        };
        _revenueChart.YAxes = new Axis[]
        {
            new() { Labeler = v => $"€{v:N0}", TextSize = 9 }
        };

        // Top 10 products (bar)
        var prodNames  = dto.TopProducts.Select(p => p.Name).ToArray();
        var prodValues = dto.TopProducts.Select(p => (double)p.Revenue).ToArray();

        _productsChart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = prodValues,
                Name   = "Ingresos (€)",
            }
        };
        _productsChart.XAxes = new Axis[]
        {
            new() { Labels = prodNames, LabelsRotation = -30, TextSize = 8 }
        };
        _productsChart.YAxes = new Axis[]
        {
            new() { Labeler = v => $"€{v:N0}", TextSize = 9 }
        };

        // Payment methods (donut)
        _paymentChart.Series = dto.PaymentMethods
            .Select(pm => (ISeries)new PieSeries<double>
            {
                Values      = new[] { (double)pm.Amount },
                Name        = $"{pm.Method} (€{pm.Amount:N0})",
                InnerRadius = 50,
            })
            .ToArray();

        // Waiters grid
        _waitersGrid.Rows.Clear();
        foreach (var w in dto.WaiterStats)
        {
            _waitersGrid.Rows.Add(
                w.Name,
                w.Orders,
                $"€{w.Revenue:N2}",
                $"€{w.AvgTicket:N2}",
                $"{w.AvgMinutes:N0} min");
        }
    }

    // ── Exports ───────────────────────────────────────────────────────────

    private void ExportExcel()
    {
        if (_currentData is null) return;

        using var sfd = new SaveFileDialog
        {
            Title            = "Exportar a Excel",
            Filter           = "Excel (*.xlsx)|*.xlsx",
            FileName         = $"estadisticas_{DateTime.Today:yyyyMMdd}.xlsx",
            DefaultExt       = "xlsx",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            FillExcel(wb, _currentData);
            wb.SaveAs(sfd.FileName);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void FillExcel(XLWorkbook wb, StatisticsDto dto)
    {
        // Sheet 1 — Revenue by hour
        var ws1 = wb.Worksheets.Add("Ingresos por hora");
        ws1.Cell(1, 1).Value = "Hora";
        ws1.Cell(1, 2).Value = "Ingresos (€)";
        ws1.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < dto.HourlyRevenue.Count; i++)
        {
            ws1.Cell(i + 2, 1).Value = $"{dto.HourlyRevenue[i].Hour}:00";
            ws1.Cell(i + 2, 2).Value = (double)dto.HourlyRevenue[i].Revenue;
        }
        ws1.Columns().AdjustToContents();

        // Sheet 2 — Top products
        var ws2 = wb.Worksheets.Add("Top Productos");
        ws2.Cell(1, 1).Value = "Producto";
        ws2.Cell(1, 2).Value = "Cantidad";
        ws2.Cell(1, 3).Value = "Ingresos (€)";
        ws2.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < dto.TopProducts.Count; i++)
        {
            var p = dto.TopProducts[i];
            ws2.Cell(i + 2, 1).Value = p.Name;
            ws2.Cell(i + 2, 2).Value = p.Quantity;
            ws2.Cell(i + 2, 3).Value = (double)p.Revenue;
        }
        ws2.Columns().AdjustToContents();

        // Sheet 3 — Payment methods
        var ws3 = wb.Worksheets.Add("Métodos de Pago");
        ws3.Cell(1, 1).Value = "Método";
        ws3.Cell(1, 2).Value = "Pedidos";
        ws3.Cell(1, 3).Value = "Total (€)";
        ws3.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < dto.PaymentMethods.Count; i++)
        {
            var pm = dto.PaymentMethods[i];
            ws3.Cell(i + 2, 1).Value = pm.Method;
            ws3.Cell(i + 2, 2).Value = pm.Count;
            ws3.Cell(i + 2, 3).Value = (double)pm.Amount;
        }
        ws3.Columns().AdjustToContents();

        // Sheet 4 — Waiters
        var ws4 = wb.Worksheets.Add("Camareros");
        ws4.Cell(1, 1).Value = "Camarero";
        ws4.Cell(1, 2).Value = "Pedidos";
        ws4.Cell(1, 3).Value = "Ingresos (€)";
        ws4.Cell(1, 4).Value = "Ticket medio (€)";
        ws4.Cell(1, 5).Value = "Tiempo medio (min)";
        ws4.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < dto.WaiterStats.Count; i++)
        {
            var w = dto.WaiterStats[i];
            ws4.Cell(i + 2, 1).Value = w.Name;
            ws4.Cell(i + 2, 2).Value = w.Orders;
            ws4.Cell(i + 2, 3).Value = (double)w.Revenue;
            ws4.Cell(i + 2, 4).Value = (double)w.AvgTicket;
            ws4.Cell(i + 2, 5).Value = w.AvgMinutes;
        }
        ws4.Columns().AdjustToContents();
    }

    private void ExportPdf()
    {
        if (_currentData is null) return;

        using var sfd = new SaveFileDialog
        {
            Title      = "Exportar a PDF",
            Filter     = "PDF (*.pdf)|*.pdf",
            FileName   = $"estadisticas_{DateTime.Today:yyyyMMdd}.pdf",
            DefaultExt = "pdf",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            BuildPdf(_currentData, sfd.FileName);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar PDF: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void BuildPdf(StatisticsDto dto, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Rush Order — Estadísticas")
                        .Bold().FontSize(18).FontColor("#E43946");
                    col.Item().Text(
                        $"Período: {dto.From:dd/MM/yyyy} — {dto.To:dd/MM/yyyy}  ·  " +
                        $"Total: €{dto.TotalRevenue:N2}  ·  {dto.TotalOrders} pedidos")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4)
                        .LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    // Hourly revenue table
                    col.Item().Text("Ingresos por hora").Bold().FontSize(13);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Hora");
                            h.Cell().Element(HeaderCell).Text("Ingresos (€)");
                        });
                        foreach (var pt in dto.HourlyRevenue)
                        {
                            t.Cell().Element(DataCell).Text($"{pt.Hour}:00");
                            t.Cell().Element(DataCell).Text($"€{pt.Revenue:N2}");
                        }
                    });

                    // Top products
                    col.Item().PaddingTop(16).Text("Top 10 Productos").Bold().FontSize(13);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(3);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Producto");
                            h.Cell().Element(HeaderCell).Text("Cantidad");
                            h.Cell().Element(HeaderCell).Text("Ingresos (€)");
                        });
                        foreach (var p in dto.TopProducts)
                        {
                            t.Cell().Element(DataCell).Text(p.Name);
                            t.Cell().Element(DataCell).Text(p.Quantity.ToString());
                            t.Cell().Element(DataCell).Text($"€{p.Revenue:N2}");
                        }
                    });

                    // Payment methods
                    col.Item().PaddingTop(16).Text("Métodos de Pago").Bold().FontSize(13);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(3);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Método");
                            h.Cell().Element(HeaderCell).Text("Pedidos");
                            h.Cell().Element(HeaderCell).Text("Total (€)");
                        });
                        foreach (var pm in dto.PaymentMethods)
                        {
                            t.Cell().Element(DataCell).Text(pm.Method);
                            t.Cell().Element(DataCell).Text(pm.Count.ToString());
                            t.Cell().Element(DataCell).Text($"€{pm.Amount:N2}");
                        }
                    });

                    // Waiters
                    col.Item().PaddingTop(16).Text("Rendimiento Camareros").Bold().FontSize(13);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4); c.RelativeColumn(2);
                            c.RelativeColumn(3); c.RelativeColumn(3); c.RelativeColumn(3);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Camarero");
                            h.Cell().Element(HeaderCell).Text("Pedidos");
                            h.Cell().Element(HeaderCell).Text("Ingresos");
                            h.Cell().Element(HeaderCell).Text("Ticket med.");
                            h.Cell().Element(HeaderCell).Text("T. Medio");
                        });
                        foreach (var w in dto.WaiterStats)
                        {
                            t.Cell().Element(DataCell).Text(w.Name);
                            t.Cell().Element(DataCell).Text(w.Orders.ToString());
                            t.Cell().Element(DataCell).Text($"€{w.Revenue:N2}");
                            t.Cell().Element(DataCell).Text($"€{w.AvgTicket:N2}");
                            t.Cell().Element(DataCell).Text($"{w.AvgMinutes:N0} min");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Rush Order Desktop · Generado el ");
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).Bold();
                    x.Span("  ·  Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(5)
         .BorderBottom(1).BorderColor(Colors.Grey.Lighten1);

    private static IContainer DataCell(IContainer c) =>
        c.Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
}
