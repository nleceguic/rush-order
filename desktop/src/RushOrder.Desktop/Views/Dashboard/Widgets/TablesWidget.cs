using System.Drawing.Drawing2D;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class TablesWidget : KpiWidget
{
    private int _occupied, _total;
    private double _avgMinutes;
    private Label _lblAvg = null!;
    private ArcPanel _arc = null!;

    public TablesWidget(ThemeManager theme) : base(theme, "Mesas") { }

    protected override void BuildContent(Panel container)
    {
        _arc = new ArcPanel(Theme) { Dock = DockStyle.Fill };
        _lblAvg = new Label
        {
            Text      = "Tiempo medio: —",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Bottom,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        container.Controls.AddRange([_arc, _lblAvg]);
    }

    public void Update(int occupied, int total, double avgOccupancyMinutes)
    {
        if (InvokeRequired) { Invoke(() => Update(occupied, total, avgOccupancyMinutes)); return; }
        _occupied   = occupied;
        _total      = total;
        _avgMinutes = avgOccupancyMinutes;
        _lblAvg.Text = $"Tiempo medio: {(int)avgOccupancyMinutes} min";
        _arc.SetData(occupied, total);
    }

    private sealed class ArcPanel : Panel
    {
        private readonly ThemeManager _theme;
        private int _occupied, _total;

        public ArcPanel(ThemeManager theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
        }

        public void SetData(int occupied, int total) { _occupied = occupied; _total = total; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();

            var size  = Math.Min(Width, Height) - 24;
            var x     = (Width  - size) / 2;
            var y     = (Height - size) / 2;
            var rect  = new RectangleF(x, y, size, size);
            var thick = size * 0.13f;

            // Background track arc (270° starting from -225°)
            using var trackPen = new Pen(Color.FromArgb(40, 128, 128, 128), thick)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(trackPen, RectangleF.Inflate(rect, -thick / 2, -thick / 2), 135, 270);

            // Value arc
            if (_total > 0)
            {
                var pct      = (float)_occupied / _total;
                var sweep    = pct * 270f;
                var fillColor = pct > 0.8f ? _theme.Colors.Error
                              : pct > 0.6f ? _theme.Colors.Warning
                              : _theme.Colors.Success;

                using var fillPen = new Pen(fillColor, thick)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(fillPen, RectangleF.Inflate(rect, -thick / 2, -thick / 2), 135, sweep);
            }

            // Center text
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var bigFont  = new Font("Segoe UI", size * 0.16f, FontStyle.Bold);
            using var smFont   = new Font("Segoe UI", size * 0.08f);
            using var textBrush = new SolidBrush(_theme.Colors.TextPrimary);
            using var subBrush  = new SolidBrush(_theme.Colors.TextSecondary);

            var centerRect = new RectangleF(x, y + size * 0.3f, size, size * 0.25f);
            g.DrawString($"{_occupied}/{_total}", bigFont, textBrush, centerRect, sf);

            var subRect = new RectangleF(x, y + size * 0.55f, size, size * 0.15f);
            g.DrawString("Ocupadas", smFont, subBrush, subRect, sf);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
