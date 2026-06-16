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
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 28,
            Padding   = new Padding(4, 8, 0, 0),
            BackColor = Color.Transparent,
        };

        _content = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.Transparent,
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

        Controls.AddRange([_lblTitle, _content]);

        // Card paint
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);

        BuildContent(_content);
        _content.Controls.Add(_lblLoading);
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
