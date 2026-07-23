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
            BackColor     = Theme.Colors.Surface,
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

        // Color.FromArgb(15, ...) as a Label/Panel BackColor renders fully opaque — plain GDI
        // fill ignores the alpha channel — so this used to come out just as saturated as the
        // solid accent bar drawn below, with nothing actually distinguishing them. Blending
        // manually against the real background gives two genuinely different, intentional
        // strengths: a strong tint behind the icon, a much lighter wash across the rest of
        // the row (inherited as this row Panel's own BackColor — lblMsg/lblTime below don't
        // set their own, so they pick it up as an ambient property).
        var rowTint  = BlendTint(Theme.Colors.Surface, color, 0.07f);
        var iconTint = BlendTint(Theme.Colors.Surface, color, 0.30f);

        var row = new Panel
        {
            Width     = _flow.ClientSize.Width - 8,
            Height    = 44,
            BackColor = rowTint,
            Margin    = new Padding(0, 2, 0, 0),
            Cursor    = Cursors.Hand,
        };

        // The stronger-tinted block is a 1:1 square (matching the row's own height) instead of
        // the previous 30-wide rectangle — a deliberate shape now that its color is actually
        // distinct from the row background, rather than an arbitrary width.
        const int iconInset = 6;
        const int iconSize  = 44; // == row.Height
        const int textGap   = 8;
        const int rightMargin = 44;
        var textX = iconInset + iconSize + textGap;

        var lblIcon = new Label
        {
            Text      = icon,
            Font      = PoppinsFont.New("Poppins", 12f),
            ForeColor = color,
            BackColor = iconTint,
            Size      = new Size(iconSize, iconSize),
            Location  = new Point(iconInset, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblMsg = new Label
        {
            Text      = alert.Message,
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextPrimary,
            Size      = new Size(row.Width - textX - rightMargin, 20),
            Location  = new Point(textX, 6),
            AutoEllipsis = true,
        };

        var lblTime = new Label
        {
            Text      = FormatAge(alert.OccurredAt),
            Font      = PoppinsFont.New("Poppins", 6.5f),
            ForeColor = Theme.Colors.TextSecondary,
            Size      = new Size(row.Width - textX - rightMargin, 14),
            Location  = new Point(textX, 26),
        };

        row.Controls.AddRange([lblIcon, lblMsg, lblTime]);
        // lblTime didn't get this — clicking directly on the "Hace X min" text (its own band,
        // not just leftover row background) silently did nothing.
        row.Click     += (_, _) => AlertClicked?.Invoke(alert);
        lblIcon.Click += (_, _) => AlertClicked?.Invoke(alert);
        lblMsg.Click  += (_, _) => AlertClicked?.Invoke(alert);
        lblTime.Click += (_, _) => AlertClicked?.Invoke(alert);

        // Left accent bar
        row.Paint += (_, e) =>
        {
            using var b = new SolidBrush(color);
            e.Graphics.FillRectangle(b, 0, 0, 3, row.Height);
        };

        return row;
    }

    private static Color BlendTint(Color background, Color tint, float amount)
    {
        var r = (byte)(background.R + (tint.R - background.R) * amount);
        var g = (byte)(background.G + (tint.G - background.G) * amount);
        var b = (byte)(background.B + (tint.B - background.B) * amount);
        return Color.FromArgb(r, g, b);
    }

    private static string FormatAge(DateTimeOffset ts)
    {
        var age = DateTimeOffset.Now - ts;
        if (age.TotalMinutes < 1)  return "Ahora mismo";
        if (age.TotalMinutes < 60) return $"Hace {(int)age.TotalMinutes} min";
        return $"Hace {(int)age.TotalHours} h";
    }
}
