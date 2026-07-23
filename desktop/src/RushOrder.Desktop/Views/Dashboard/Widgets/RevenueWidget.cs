using System.Drawing.Drawing2D;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class RevenueWidget : KpiWidget
{
    private decimal _revenue;
    private double  _pct;
    private bool    _up;
    private decimal[] _hourly = new decimal[8];
    private SparklinePanel _spark = null!;
    private Label _lblValue = null!;
    private Label _lblDelta = null!;

    public RevenueWidget(ThemeManager theme) : base(theme, "Ingresos del día") { }

    protected override void BuildContent(Panel container)
    {
        _lblValue = new Label
        {
            Text      = "€ 0,00",
            Font      = PoppinsFont.New("Poppins", 22f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 46,
            TextAlign = ContentAlignment.BottomLeft,
            // 6px, not the 10px _lblDelta uses below — this glyph ("€" at 22pt Bold) carries
            // more inherent left-side bearing than the delta line's "▲" at the small font, so
            // equal Padding.Left values leave the value's visible ink sitting further right.
            Padding   = new Padding(6, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };

        _lblDelta = new Label
        {
            Text      = "—",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 22,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };

        _spark = new SparklinePanel(Theme) { Dock = DockStyle.Fill };

        // A Dock=Fill sibling must be added BEFORE the Dock=Top/Bottom ones — WinForms
        // resolves docking back-to-front through the Controls z-order, so a Fill control
        // added last (as this was) computes its bounds as if it had no siblings at all,
        // covering the whole container and hiding _lblValue/_lblDelta underneath it.
        // Among same-edge Dock=Top siblings, the LAST one added ends up outermost (Y=0), so
        // _lblValue (meant to sit above _lblDelta) must be added after it here.
        container.Controls.AddRange([_spark, _lblDelta, _lblValue]);
    }

    public void Update(decimal today, decimal yesterday, decimal[] hourly)
    {
        if (InvokeRequired) { Invoke(() => Update(today, yesterday, hourly)); return; }
        _revenue = today;
        _pct     = GdiExtensions.PercentChange(today, yesterday);
        _up      = today >= yesterday;
        _hourly  = hourly;

        _lblValue.Text      = $"€ {today:N2}";
        var arrow           = _up ? "▲" : "▼";
        _lblDelta.ForeColor = _up ? Theme.Colors.Success : Theme.Colors.Error;
        _lblDelta.Text      = $"{arrow} {Math.Abs(_pct):F1}% vs. ayer";
        _spark.SetData(hourly, _up ? Theme.Colors.Success : Theme.Colors.Error);
    }

    private sealed class SparklinePanel : Panel
    {
        private readonly ThemeManager _theme;
        private decimal[] _data = [];
        private Color _lineColor;

        public SparklinePanel(ThemeManager theme)
        {
            _theme     = theme;
            _lineColor = theme.Colors.Success;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            BackColor = theme.Colors.Surface;
        }

        public void SetData(decimal[] data, Color lineColor)
        {
            _data      = data;
            _lineColor = lineColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();

            // OnPaintBackground is suppressed below, so this control must paint its own fill —
            // otherwise whatever's left in the (possibly stale) buffer shows through instead.
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            if (_data.Length < 2) return;

            // Left/right kept to just the dot marker's own radius (2.5px, rounded up) — any
            // more and the line stops short of the panel's edges; any less and the first/last
            // dots get clipped by the panel bounds. Top/bottom stay wider for the line's
            // vertical range and the hour labels below it.
            var pad    = new Padding(3, 8, 3, 8);
            var w      = Width  - pad.Horizontal;
            var h      = Height - pad.Vertical;
            var minVal = (double)_data.Min();
            var maxVal = (double)_data.Max();
            var range  = maxVal - minVal;
            if (range < 1) range = 1;

            var pts = new PointF[_data.Length];
            for (int i = 0; i < _data.Length; i++)
            {
                var nx = (float)i / (_data.Length - 1);
                var ny = 1f - (float)(((double)_data[i] - minVal) / range);
                pts[i] = new PointF(pad.Left + nx * w, pad.Top + ny * h * 0.9f);
            }

            // Fill gradient under line
            var fillPts = new PointF[pts.Length + 2];
            fillPts[0] = new PointF(pts[0].X, pad.Top + h);
            Array.Copy(pts, 0, fillPts, 1, pts.Length);
            fillPts[^1] = new PointF(pts[^1].X, pad.Top + h);

            using var fillBrush = new LinearGradientBrush(
                new PointF(0, pad.Top), new PointF(0, pad.Top + h),
                Color.FromArgb(60, _lineColor), Color.Transparent);
            g.FillPolygon(fillBrush, fillPts);

            using var linePen = new Pen(_lineColor, 2f) { LineJoin = LineJoin.Round };
            g.DrawLines(linePen, pts);

            // Dots at each point
            using var dotBrush = new SolidBrush(_lineColor);
            foreach (var pt in pts)
                g.FillEllipse(dotBrush, pt.X - 2.5f, pt.Y - 2.5f, 5f, 5f);

            // Hour labels — kept clear of the card's rounded corners (KpiWidget clips the
            // whole widget to a 10px-radius Region). Centering on a fixed "-8" offset instead
            // of the label's real width let the first point's label (drawn at X=0) bleed into
            // that corner and get clipped almost entirely; the last point's overran the right
            // edge by a smaller amount for the same reason.
            using var lblFont  = PoppinsFont.New("Poppins", 6.5f);
            using var lblBrush = new SolidBrush(_theme.Colors.TextSecondary);
            var now       = DateTime.Now.Hour;
            const float cornerMargin = 12f;
            var minX = cornerMargin;
            var maxX = Width - cornerMargin;
            for (int i = 0; i < pts.Length; i++)
            {
                var hr   = ((now - 7 + i + 24) % 24).ToString("D2") + "h";
                var size = g.MeasureString(hr, lblFont);
                var x    = Math.Clamp(pts[i].X - size.Width / 2f, minX, maxX - size.Width);
                // "pad.Top + h - 2" placed the label's TOP 2px above the panel's bottom edge,
                // so its full height (text runs ~10-11px at this font) overshot past Height and
                // got clipped — anchor from the bottom using the label's real measured height.
                var y    = Height - size.Height - 2f;
                g.DrawString(hr, lblFont, lblBrush, x, y);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
