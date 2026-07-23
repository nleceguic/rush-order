using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class ActiveOrdersWidget : KpiWidget
{
    private Label _lblTotal = null!;
    private StatusPillsPanel _pills = null!;
    private StatusDistributionPanel _distribution = null!;

    public ActiveOrdersWidget(ThemeManager theme) : base(theme, "Pedidos activos") { }

    protected override void BuildContent(Panel container)
    {
        _lblTotal = new Label
        {
            Text      = "0",
            Font      = PoppinsFont.New("Poppins", 36f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 60,
            TextAlign = ContentAlignment.BottomLeft,
            // 0, not 8 — at 36pt Bold, "10"'s own glyph bearing (~15px) already puts its ink
            // well past where the title/chart line up; any extra Padding.Left only pushes it
            // further out of line with them.
            Padding   = new Padding(0, 0, 0, 0),
            BackColor = Theme.Colors.Surface,
        };

        _pills = new StatusPillsPanel(Theme) { Dock = DockStyle.Top, Height = 34 };
        _distribution = new StatusDistributionPanel(Theme) { Dock = DockStyle.Fill };

        // Fill sibling first, then same-edge Top ones in reverse visual order — see
        // KpiWidget's note on Dock ordering (the last Top one added ends up outermost/top).
        container.Controls.AddRange([_distribution, _pills, _lblTotal]);
    }

    public void Update(int waiting, int preparing, int ready)
    {
        if (InvokeRequired) { Invoke(() => Update(waiting, preparing, ready)); return; }
        var total      = waiting + preparing + ready;
        _lblTotal.Text = total.ToString();
        _pills.SetData(waiting, preparing, ready);
        _distribution.SetData(waiting, preparing, ready);
    }

    /// <summary>Three status badges, custom-painted instead of Labels. Two Label-based bugs
    /// this replaces: (1) <c>Color.FromArgb(alpha, ...)</c> as a Label's BackColor renders fully
    /// opaque — plain GDI fill ignores the alpha channel, so the intended soft tint came out as
    /// a saturated, squared-off block instead; (2) the pills sat at Location X=0 inside a Panel
    /// whose Padding.Left was set expecting it to apply — Control.Padding only affects
    /// docked/anchored auto-layout, never manually-positioned children, so it silently did
    /// nothing and the row started flush left instead of lined up under _lblTotal above it.</summary>
    private sealed class StatusPillsPanel : Panel
    {
        private readonly ThemeManager _theme;
        private int _waiting, _preparing, _ready;

        public StatusPillsPanel(ThemeManager theme)
        {
            _theme    = theme;
            BackColor = theme.Colors.Surface;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
        }

        public void SetData(int waiting, int preparing, int ready)
        {
            _waiting = waiting; _preparing = preparing; _ready = ready;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            var items = new (string Label, int Value, Color Color)[]
            {
                ("En espera",  _waiting,   Color.FromArgb(255, 152, 0)),
                ("Preparando", _preparing, Color.FromArgb(33, 150, 243)),
                ("Listos",     _ready,     Color.FromArgb(76, 175, 80)),
            };

            using var font = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold);
            const float pillHeight = 22f;
            const float gap        = 6f;
            // Matches _lblTotal's "10" above (Padding.Left 0, ink starting ~15px in from its
            // own glyph bearing alone) — see the note on that Padding for why.
            const float startX = 15f;

            var x = startX;
            var y = (Height - pillHeight) / 2f;

            foreach (var (label, value, color) in items)
            {
                var text = $"{value} {label}";
                var size = g.MeasureString(text, font);
                var pillWidth = size.Width + 16f;

                using var path = GdiExtensions.CreateRoundedRect(
                    new RectangleF(x, y, pillWidth, pillHeight), pillHeight / 2);
                using (var fillBrush = new SolidBrush(BlendTint(_theme.Colors.Surface, color)))
                    g.FillPath(fillBrush, path);

                using var textBrush = new SolidBrush(color);
                g.DrawString(text, font, textBrush,
                    x + (pillWidth - size.Width) / 2, y + (pillHeight - size.Height) / 2);

                x += pillWidth + gap;
            }
        }

        // Manually blends the badge color into the panel's actual background at low strength —
        // the correct, opaque equivalent of what a translucent BackColor was meant to look like.
        private static Color BlendTint(Color background, Color tint)
        {
            const float amount = 0.18f;
            var r = (byte)(background.R + (tint.R - background.R) * amount);
            var g = (byte)(background.G + (tint.G - background.G) * amount);
            var b = (byte)(background.B + (tint.B - background.B) * amount);
            return Color.FromArgb(r, g, b);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }

    /// <summary>Stacked horizontal bar showing how the active-order total splits across
    /// statuses — the pills above give exact counts, this shows their relative weight and
    /// fills the space that used to sit empty below them.</summary>
    private sealed class StatusDistributionPanel : Panel
    {
        private readonly ThemeManager _theme;
        private int _waiting, _preparing, _ready;

        private static readonly Color WaitingColor   = Color.FromArgb(255, 152, 0);
        private static readonly Color PreparingColor = Color.FromArgb(33, 150, 243);
        private static readonly Color ReadyColor     = Color.FromArgb(76, 175, 80);

        public StatusDistributionPanel(ThemeManager theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            BackColor = theme.Colors.Surface;
        }

        public void SetData(int waiting, int preparing, int ready)
        {
            _waiting = waiting; _preparing = preparing; _ready = ready;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SetQuality();

            // OnPaintBackground is suppressed below, so this control must paint its own fill.
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            // Left is 15 to line the bar's left edge up with _lblTotal/_pills above (their own
            // left inset, see those widgets' notes); right matches it so the bar sits centered
            // in the space below them instead of skewed toward the left edge.
            var pad      = new Padding(15, 4, 15, 4);
            var barWidth = Width - pad.Horizontal;
            if (barWidth <= 0 || Height <= 0) return;

            const float barHeight = 30f;
            const float gap       = 16f;

            using var valueFont = PoppinsFont.New("Poppins", 15f, FontStyle.Bold);
            var lblFont = _theme.Fonts.Small; // shared singleton — never dispose

            var total = _waiting + _preparing + _ready;

            if (total <= 0)
            {
                var msg     = "Sin pedidos activos";
                using var msgFont = PoppinsFont.New("Poppins", 8.5f, FontStyle.Regular);
                var msgSize = g.MeasureString(msg, msgFont);
                var blockH  = barHeight + gap + msgSize.Height;
                var topY0   = Math.Max(pad.Top, (Height - blockH) / 2f);

                using var trackPath0 = GdiExtensions.CreateRoundedRect(
                    new RectangleF(pad.Left, topY0, barWidth, barHeight), barHeight / 2);
                using var trackBrush0 = new SolidBrush(Color.FromArgb(18, _theme.Colors.TextSecondary));
                g.FillPath(trackBrush0, trackPath0);

                using var msgBrush = new SolidBrush(_theme.Colors.TextSecondary);
                g.DrawString(msg, msgFont, msgBrush,
                    pad.Left + (barWidth - msgSize.Width) / 2, topY0 + barHeight + gap - 4);
                return;
            }

            var legendItems = new (string Label, int Value, Color Color)[]
            {
                ("En espera",  _waiting,   WaitingColor),
                ("Preparando", _preparing, PreparingColor),
                ("Listos",     _ready,     ReadyColor),
            };

            var valueHeight  = valueFont.GetHeight(g);
            var labelHeight  = lblFont.GetHeight(g);
            var legendHeight = valueHeight + 2 + labelHeight;
            var contentHeight = barHeight + gap + legendHeight;
            var topY = Math.Max(pad.Top, (Height - contentHeight) / 2f);
            var barY = topY;

            // Track + clipped stacked segments, so the whole bar shares one rounded outline.
            using var trackPath = GdiExtensions.CreateRoundedRect(
                new RectangleF(pad.Left, barY, barWidth, barHeight), barHeight / 2);
            using (var trackBrush = new SolidBrush(Color.FromArgb(18, _theme.Colors.TextSecondary)))
                g.FillPath(trackBrush, trackPath);

            var oldClip = g.Clip;
            g.SetClip(trackPath, System.Drawing.Drawing2D.CombineMode.Replace);

            var x = (float)pad.Left;
            foreach (var item in legendItems)
            {
                if (item.Value <= 0) continue;
                var segWidth = barWidth * item.Value / (float)total;
                using var segBrush = new SolidBrush(item.Color);
                g.FillRectangle(segBrush, x, barY, segWidth, barHeight);
                x += segWidth;
            }

            g.Clip = oldClip;

            // Legend row: count + label per status, centered under its share of the bar.
            var legendY  = barY + barHeight + gap;
            var colWidth = barWidth / (float)legendItems.Length;

            for (var i = 0; i < legendItems.Length; i++)
            {
                var (label, value, color) = legendItems[i];
                var colX = pad.Left + colWidth * i;

                var valueText = value.ToString();
                var valueSize = g.MeasureString(valueText, valueFont);
                using var valueBrush = new SolidBrush(color);
                g.DrawString(valueText, valueFont, valueBrush,
                    colX + (colWidth - valueSize.Width) / 2, legendY);

                var labelSize = g.MeasureString(label, lblFont);
                using var labelBrush = new SolidBrush(_theme.Colors.TextSecondary);
                g.DrawString(label, lblFont, labelBrush,
                    colX + (colWidth - labelSize.Width) / 2, legendY + valueHeight + 2);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
