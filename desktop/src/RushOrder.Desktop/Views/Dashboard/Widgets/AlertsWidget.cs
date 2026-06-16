using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class AlertsWidget : KpiWidget
{
    private FlowLayoutPanel _flow = null!;

    public event Action<AlertDto>? AlertClicked;

    public AlertsWidget(ThemeManager theme) : base(theme, "Alertas activas") { }

    protected override void BuildContent(Panel container)
    {
        _flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoScroll    = true,
            BackColor     = Color.Transparent,
            Padding       = new Padding(4, 2, 4, 4),
        };
        container.Controls.Add(_flow);
    }

    public void Update(IReadOnlyList<AlertDto> alerts)
    {
        if (InvokeRequired) { Invoke(() => Update(alerts)); return; }
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        foreach (var alert in alerts)
            _flow.Controls.Add(MakeRow(alert));
        _flow.ResumeLayout();
    }

    private Panel MakeRow(AlertDto alert)
    {
        var (icon, color) = alert.Severity switch
        {
            AlertSeverity.Critical => ("✕", Theme.Colors.Error),
            AlertSeverity.Warning  => ("⚠", Theme.Colors.Warning),
            _                      => ("ℹ", Theme.Colors.Info),
        };

        var row = new Panel
        {
            Width     = _flow.ClientSize.Width - 8,
            Height    = 44,
            BackColor = Color.FromArgb(15, color.R, color.G, color.B),
            Margin    = new Padding(0, 2, 0, 0),
            Cursor    = Cursors.Hand,
        };

        var lblIcon = new Label
        {
            Text      = icon,
            Font      = new Font("Segoe UI", 12f),
            ForeColor = color,
            Size      = new Size(30, 44),
            Location  = new Point(6, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblMsg = new Label
        {
            Text      = alert.Message,
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextPrimary,
            Size      = new Size(row.Width - 80, 28),
            Location  = new Point(36, 6),
            AutoEllipsis = true,
        };

        var lblTime = new Label
        {
            Text      = FormatAge(alert.OccurredAt),
            Font      = new Font("Segoe UI", 6.5f),
            ForeColor = Theme.Colors.TextSecondary,
            Size      = new Size(row.Width - 80, 14),
            Location  = new Point(36, 28),
        };

        row.Controls.AddRange([lblIcon, lblMsg, lblTime]);
        row.Click     += (_, _) => AlertClicked?.Invoke(alert);
        lblIcon.Click += (_, _) => AlertClicked?.Invoke(alert);
        lblMsg.Click  += (_, _) => AlertClicked?.Invoke(alert);

        // Left accent bar
        row.Paint += (_, e) =>
        {
            using var b = new SolidBrush(color);
            e.Graphics.FillRectangle(b, 0, 0, 3, row.Height);
        };

        return row;
    }

    private static string FormatAge(DateTimeOffset ts)
    {
        var age = DateTimeOffset.Now - ts;
        if (age.TotalMinutes < 1)  return "Ahora mismo";
        if (age.TotalMinutes < 60) return $"Hace {(int)age.TotalMinutes} min";
        return $"Hace {(int)age.TotalHours} h";
    }
}
