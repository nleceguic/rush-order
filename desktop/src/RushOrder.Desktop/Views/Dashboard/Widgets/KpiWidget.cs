using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal abstract class KpiWidget : UserControl
{
    protected readonly ThemeManager Theme;
    private readonly Label  _lblTitle;
    private readonly Panel  _content;
    private readonly Label  _lblLoading;
    private bool            _loading;

    protected KpiWidget(ThemeManager theme, string title)
    {
        Theme       = theme;
        Dock        = DockStyle.Fill;
        Margin      = new Padding(6);
        DoubleBuffered = true;

        _lblTitle = new Label
        {
            Text      = title.ToUpperInvariant(),
            Font      = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold),
            ForeColor = theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 28,
            // 10px, not 4 — at 4 the title sat visibly left of the value/delta text below it
            // (measured ~6-8px further left on screen), since those use their own larger
            // Padding.Left plus extra glyph bearing from their bigger fonts.
            Padding   = new Padding(10, 8, 0, 0),
            BackColor = theme.Colors.Surface,
        };

        _content = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = theme.Colors.Surface,
        };

        _lblLoading = new Label
        {
            Text      = "Cargando…",
            Font      = theme.Fonts.Small,
            ForeColor = theme.Colors.TextSecondary,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        // Dock=Fill sibling must be added BEFORE Dock=Top ones, or it computes its bounds as
        // if it had no siblings — confirmed via EnumChildWindows: _content was claiming the
        // widget's full rect (including _lblTitle's 28px band) instead of starting below it,
        // so its opaque fill painted over the title.
        Controls.AddRange([_content, _lblTitle]);

        // Card paint
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);

        BuildContent(_content);
        _content.Controls.Add(_lblLoading);

        // OnPaint below draws a rounded card, but _lblTitle/_content fill their own square
        // rectangles right up to the edges, covering that rounding — clip the whole control
        // to the same rounded shape so their sharp corners never show past it.
        Resize += (_, _) => ApplyRoundedRegion();
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = GdiExtensions.CreateRoundedRect(new RectangleF(0, 0, Width, Height), 10);
        Region = new Region(path);
    }

    protected abstract void BuildContent(Panel container);

    protected void SetLoading(bool loading)
    {
        if (InvokeRequired) { Invoke(() => SetLoading(loading)); return; }
        _loading            = loading;
        _lblLoading.Visible = loading;
        _lblLoading.BringToFront();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        using var bgBrush = new SolidBrush(Theme.Colors.Surface);
        g.FillRoundedRectangle(bgBrush, r, 10);
        using var border = new Pen(Theme.Colors.Border, 1f);
        g.DrawRoundedRectangle(border, r, 10);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }  // suppress default
}
