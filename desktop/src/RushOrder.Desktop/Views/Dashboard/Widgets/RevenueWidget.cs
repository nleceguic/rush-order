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
            Font      = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 46,
            TextAlign = ContentAlignment.BottomLeft,
            Padding   = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
        };

        _lblDelta = new Label
        {
            Text      = "—",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 22,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
        };

        _spark = new SparklinePanel(Theme) { Dock = DockStyle.Fill };

        container.Controls.AddRange([_lblValue, _lblDelta, _spark]);
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
            BackColor = Color.Transparent;
        }

        public void SetData(decimal[] data, Color lineColor)
        {
            _data      = data;
            _lineColor = lineColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_data.Length < 2) return;
            var g = e.Graphics;
            g.SetQuality();

            var pad    = new Padding(8, 8, 8, 8);
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

            // Hour labels
            using var lblFont  = new Font("Segoe UI", 6.5f);
            using var lblBrush = new SolidBrush(_theme.Colors.TextSecondary);
            var now   = DateTime.Now.Hour;
            for (int i = 0; i < pts.Length; i++)
            {
                var hr = ((now - 7 + i + 24) % 24).ToString("D2") + "h";
                g.DrawString(hr, lblFont, lblBrush, pts[i].X - 8, pad.Top + h - 2);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
