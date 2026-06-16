using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Forms.Controls;

internal sealed class NavButton : Control
{
    private bool _isActive;
    private bool _isHovered;
    private bool _isCollapsed;

    public string IconText  { get; set; } = "";
    public string LabelText { get; set; } = "";

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; Invalidate(); }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set { _isCollapsed = value; Invalidate(); }
    }

    public NavButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);
        Height = 46;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g      = e.Graphics;
        var colors = ThemeManager.Instance?.Colors;

        g.Clear(_isActive
            ? Color.FromArgb(230, 57, 70)
            : _isHovered
                ? Color.FromArgb(50, 255, 255, 255)
                : Color.Transparent);

        // Active indicator bar
        if (_isActive)
        {
            using var brush = new SolidBrush(Color.White);
            g.FillRectangle(brush, 0, 8, 3, Height - 16);
        }

        // Icon
        var iconFont  = new Font("Segoe UI Symbol", 14f);
        var textColor = Color.FromArgb(_isActive ? 255 : 200, 255, 255, 255);
        using var textBrush = new SolidBrush(textColor);

        if (_isCollapsed)
        {
            var iconRect = new Rectangle(0, 0, Width, Height);
            g.DrawString(IconText, iconFont, textBrush, iconRect,
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        else
        {
            var iconRect  = new Rectangle(12, 0, 36, Height);
            var labelRect = new Rectangle(52, 0, Width - 60, Height);
            g.DrawString(IconText, iconFont, textBrush, iconRect,
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

            var labelFont = new Font("Segoe UI", 9f, _isActive ? FontStyle.Bold : FontStyle.Regular);
            g.DrawString(LabelText, labelFont, textBrush, labelRect,
                new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center });
            labelFont.Dispose();
        }

        iconFont.Dispose();
    }
}
