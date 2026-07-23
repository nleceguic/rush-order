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
            BackColor = Theme.Colors.Surface,
        };
        // A bare bottom-margin spacer — added last so it ends up outermost/flush against the
        // widget's true bottom edge (see the Dock-order note elsewhere in these widgets),
        // nudging _lblAvg up off that edge instead of sitting flush against it.
        var bottomSpacer = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 10,
            BackColor = Theme.Colors.Surface,
        };
        container.Controls.AddRange([_arc, _lblAvg, bottomSpacer]);
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
            BackColor = theme.Colors.Surface;
        }

        public void SetData(int occupied, int total) { _occupied = occupied; _total = total; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();

            // OnPaintBackground is suppressed below, so this control must paint its own fill —
            // otherwise whatever's left in the (possibly stale) buffer shows through instead.
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            // A legend band ("N ocupadas / N libres") reserved below the ring — it used to just
            // center in the full panel and repeat the same "Ocupadas" word already shown above,
            // leaving the whole lower portion of the widget empty.
            const float legendBand = 42f;
            var ringArea = Math.Max(0f, Height - legendBand);

            var size  = Math.Min(Width, ringArea) - 16;
            var x     = (Width    - size) / 2;
            var y     = (ringArea - size) / 2;
            var rect  = new RectangleF(x, y, size, size);
            var thick = size * 0.13f;

            var pct       = _total > 0 ? (float)_occupied / _total : 0f;
            var fillColor = pct > 0.8f ? _theme.Colors.Error
                          : pct > 0.6f ? _theme.Colors.Warning
                          : _theme.Colors.Success;

            // Background track arc (270° starting from -225°)
            using var trackPen = new Pen(Color.FromArgb(40, 128, 128, 128), thick)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(trackPen, RectangleF.Inflate(rect, -thick / 2, -thick / 2), 135, 270);

            // Value arc
            if (_total > 0)
            {
                using var fillPen = new Pen(fillColor, thick)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(fillPen, RectangleF.Inflate(rect, -thick / 2, -thick / 2), 135, pct * 270f);
            }

            // Center text
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var bigFont  = PoppinsFont.New("Poppins", size * 0.16f, FontStyle.Bold);
            using var smFont   = PoppinsFont.New("Poppins", size * 0.08f);
            using var textBrush = new SolidBrush(_theme.Colors.TextPrimary);
            using var subBrush  = new SolidBrush(_theme.Colors.TextSecondary);

            var centerRect = new RectangleF(x, y + size * 0.3f, size, size * 0.25f);
            g.DrawString($"{_occupied}/{_total}", bigFont, textBrush, centerRect, sf);

            var subRect = new RectangleF(x, y + size * 0.55f, size, size * 0.15f);
            g.DrawString("Ocupadas", smFont, subBrush, subRect, sf);

            // Legend row: occupied (ring color) vs free, using data already on hand
            // (total - occupied) — fills the band reserved above instead of leaving it blank.
            // Uses the theme's small shared font, NOT `smFont` above (that one is sized
            // relative to the ring's diameter for the big-ratio subtitle and comes out huge
            // at this ring's actual size — a mismatch that made this row nearly as large as
            // the "8/12" text and crowd out "Tiempo medio" below it).
            var free = Math.Max(0, _total - _occupied);
            using var legendValueFont = PoppinsFont.New("Poppins", 13f, FontStyle.Bold);
            var legendLabelFont = _theme.Fonts.Small; // shared singleton — never dispose
            var legendItems = new (string Label, int Value, Color Color)[]
            {
                ("Ocupadas", _occupied, fillColor),
                ("Libres",   free,      _theme.Colors.TextSecondary),
            };

            var colWidth = Width / (float)legendItems.Length;
            var valueH   = legendValueFont.GetHeight(g);
            var lblH     = legendLabelFont.GetHeight(g);
            // Biased a bit above dead-center in the band — sitting exactly centered read as
            // too close to "Tiempo medio" right below it.
            var legendY  = ringArea + (legendBand - (valueH + lblH)) / 2f - 8f;

            for (var i = 0; i < legendItems.Length; i++)
            {
                var (label, value, color) = legendItems[i];
                var colX = colWidth * i;

                var valueText = value.ToString();
                var valueSize = g.MeasureString(valueText, legendValueFont);
                using var valueBrush = new SolidBrush(color);
                g.DrawString(valueText, legendValueFont, valueBrush,
                    colX + (colWidth - valueSize.Width) / 2, legendY);

                var labelSize = g.MeasureString(label, legendLabelFont);
                using var labelBrush = new SolidBrush(_theme.Colors.TextSecondary);
                g.DrawString(label, legendLabelFont, labelBrush,
                    colX + (colWidth - labelSize.Width) / 2, legendY + valueH);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
