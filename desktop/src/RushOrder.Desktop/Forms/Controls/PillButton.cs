using System.Diagnostics.CodeAnalysis;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Forms.Controls;

// Pill-shaped stand-in for a flat Button. A native FlatAppearance border is drawn around the
// control's rectangular bounds; once that rectangle gets clipped down to a pill via Region
// (see RoundControl in OrdersView), the border only survives on the flat middle stretch of the
// top/bottom edges and disappears entirely around the rounded ends — which is exactly what
// "no border, looks cut off" was. Painting the pill and its border ourselves (same approach as
// PillComboBox) sidesteps that, and gives real hover/pressed states in the brand color instead
// of whatever FlatAppearance manages natively.
internal sealed class PillButton : Control
{
    private readonly ThemeManager _theme;
    private bool _hovered;
    private bool _pressed;

    public PillButton(ThemeManager theme, string text)
    {
        _theme = theme;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Font   = theme.Fonts.Small;
        Height = 30;
        Cursor = Cursors.Hand;
        base.Text = text;
        AutoSizeToText();
    }

    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set { base.Text = value; AutoSizeToText(); Invalidate(); }
    }

    // A flat chars-times-a-guess width estimate is what left the text clipped in the first
    // place — measuring the actual string against the actual font is the only way to guarantee
    // it fits. Uses a throwaway Graphics rather than CreateGraphics() so it doesn't force this
    // control's own HWND into existence before it's even parented.
    private void AutoSizeToText()
    {
        if (string.IsNullOrEmpty(Text)) return;
        using var bmp = new Bitmap(1, 1);
        using var g   = Graphics.FromImage(bmp);
        var size = g.MeasureString(Text, Font);
        Width = (int)Math.Ceiling(size.Width) + 28;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g      = e.Graphics;
        var colors = _theme.Colors;
        g.SetQuality();
        g.Clear(Parent?.BackColor ?? colors.Surface);

        var rect   = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        var radius = Height / 2f;

        using var bg = new SolidBrush(_pressed ? colors.Primary : colors.Surface);
        g.FillRoundedRectangle(bg, rect, radius);

        var borderColor = _pressed || _hovered ? colors.Primary : colors.Border;
        using var border = new Pen(borderColor, 1f);
        g.DrawRoundedRectangle(border, rect, radius);

        using var textBrush = new SolidBrush(_pressed ? Color.White : colors.TextPrimary);
        g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        });
    }
}
