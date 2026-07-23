using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class AvgTicketWidget : KpiWidget
{
    private Label _lblValue = null!;
    private Label _lblDelta = null!;
    private Label _lblSub   = null!;
    private ComparisonBarPanel _bars = null!;

    public AvgTicketWidget(ThemeManager theme) : base(theme, "Ticket medio") { }

    protected override void BuildContent(Panel container)
    {
        _lblValue = new Label
        {
            Text      = "€ 0,00",
            Font      = PoppinsFont.New("Poppins", 26f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 52,
            TextAlign = ContentAlignment.BottomLeft,
            // 4px, not the 10px _lblDelta/_lblSub use below — this glyph ("€" at 26pt Bold)
            // carries more inherent left-side bearing than the small-font lines below it, so
            // equal Padding.Left values leave the value's visible ink sitting further right
            // (measured ~5px off on screen before this adjustment).
            Padding   = new Padding(4, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };
        _lblDelta = new Label
        {
            Text      = "",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 22,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };
        _lblSub = new Label
        {
            Text      = "por comensal",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 20,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };

        _bars = new ComparisonBarPanel(Theme) { Dock = DockStyle.Fill };

        // Fill sibling first, then same-edge Top ones in reverse visual order — see
        // KpiWidget's note on Dock ordering (the last Top one added ends up outermost/top).
        container.Controls.AddRange([_bars, _lblDelta, _lblSub, _lblValue]);
    }

    public void Update(decimal today, decimal yesterday)
    {
        if (InvokeRequired) { Invoke(() => Update(today, yesterday)); return; }
        _lblValue.Text = $"€ {today:N2}";
        var pct  = GdiExtensions.PercentChange(today, yesterday);
        var up   = today >= yesterday;
        _lblDelta.ForeColor = up ? Theme.Colors.Success : Theme.Colors.Error;
        _lblDelta.Text = $"{(up ? "▲" : "▼")} {Math.Abs(pct):F1}% vs. ayer (€ {yesterday:N2})";
        _bars.SetData(today, yesterday, up ? Theme.Colors.Success : Theme.Colors.Error);
    }

    /// <summary>Simple "Hoy" vs "Ayer" bar comparison — the KPI only carries these two
    /// values (no hourly breakdown like Revenue's), so a sparkline isn't possible here.</summary>
    private sealed class ComparisonBarPanel : Panel
    {
        private readonly ThemeManager _theme;
        private decimal _today, _yesterday;
        private Color _todayColor;

        public ComparisonBarPanel(ThemeManager theme)
        {
            _theme      = theme;
            _todayColor = theme.Colors.Success;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            BackColor = theme.Colors.Surface;
        }

        public void SetData(decimal today, decimal yesterday, Color todayColor)
        {
            _today      = today;
            _yesterday  = yesterday;
            _todayColor = todayColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();

            // OnPaintBackground is suppressed below, so this control must paint its own fill.
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            if (_today <= 0 && _yesterday <= 0) return;

            var pad    = new Padding(24, 26, 24, 28);
            var chartW = Width  - pad.Horizontal;
            var chartH = Height - pad.Vertical;
            if (chartW <= 0 || chartH <= 0) return;

            var maxVal = Math.Max(_today, _yesterday);
            if (maxVal <= 0) maxVal = 1;

            var barWidth = Math.Min(64f, chartW * 0.28f);
            var gap      = chartW * 0.16f;
            var startX   = pad.Left + (chartW - (barWidth * 2 + gap)) / 2f;

            DrawBar(g, startX, pad.Top, barWidth, chartH,
                (float)(_yesterday / maxVal), Color.FromArgb(90, 90, 90), "Ayer", _yesterday);
            DrawBar(g, startX + barWidth + gap, pad.Top, barWidth, chartH,
                (float)(_today / maxVal), _todayColor, "Hoy", _today);
        }

        private void DrawBar(Graphics g, float x, float top, float width, float chartH,
            float fraction, Color barColor, string label, decimal value)
        {
            fraction = Math.Clamp(fraction, 0f, 1f);
            var barH   = Math.Max(4f, chartH * fraction);
            var barTop = top + (chartH - barH);
            var rect   = new RectangleF(x, barTop, width, barH);

            using var brush = new SolidBrush(barColor);
            using var path  = GdiExtensions.CreateRoundedRect(rect, Math.Min(8f, width / 2));
            g.FillPath(brush, path);

            // PoppinsFont.New creates a fresh Font each call (unlike Theme.Fonts.*, which is
            // shared) — this one needs disposing.
            using var valueFont = PoppinsFont.New("Poppins", 8f, FontStyle.Bold);
            var valueText  = $"€{value:N0}";
            var valueSize  = g.MeasureString(valueText, valueFont);
            using var valueBrush = new SolidBrush(_theme.Colors.TextPrimary);
            g.DrawString(valueText, valueFont, valueBrush,
                x + (width - valueSize.Width) / 2, barTop - valueSize.Height - 4);

            var lblFont = _theme.Fonts.Small;
            var lblSize = g.MeasureString(label, lblFont);
            using var lblBrush = new SolidBrush(_theme.Colors.TextSecondary);
            g.DrawString(label, lblFont, lblBrush,
                x + (width - lblSize.Width) / 2, top + chartH + 6);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
