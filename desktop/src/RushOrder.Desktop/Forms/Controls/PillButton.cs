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
    private readonly Color _idleBackColor;
    private readonly Color _idleBorderColor;
    private readonly Color? _idleTextColor;
    private bool _hovered;
    private bool _pressed;
    private bool _active;

    // idleBackColor/idleBorderColor accept Color.Transparent (alpha 0) as an explicit "don't
    // draw this in the idle state" sentinel — e.g. the KDS header buttons want no border at
    // rest. idleTextColor overrides colors.TextPrimary for callers (like KDS) that force a dark
    // background regardless of the app's light/dark theme, where TextPrimary would be unreadable.
    public PillButton(
        ThemeManager theme, string text,
        Color? idleBackColor = null, Color? idleBorderColor = null, Color? idleTextColor = null)
    {
        _theme           = theme;
        _idleBackColor   = idleBackColor   ?? theme.Colors.Surface;
        _idleBorderColor = idleBorderColor ?? theme.Colors.Border;
        _idleTextColor   = idleTextColor;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Font   = theme.Fonts.Small;
        Height = 30;
        Cursor = Cursors.Hand;
        base.Text = text;
        AutoSizeToText();
    }

    // Persistent selected/toggled look — same full pink-red fill as a mouse-down press, but
    // sticks until explicitly cleared. Used for station filters and the mute toggle.
    public bool IsActive
    {
        get => _active;
        set { if (_active == value) return; _active = value; Invalidate(); }
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

        bool filled  = _pressed || _active;
        var bgColor  = filled ? colors.Primary : _idleBackColor;
        if (bgColor.A > 0)
        {
            using var bg = new SolidBrush(bgColor);
            g.FillRoundedRectangle(bg, rect, radius);
        }

        var borderColor = filled || _hovered ? colors.Primary : _idleBorderColor;
        if (borderColor.A > 0)
        {
            using var border = new Pen(borderColor, 1f);
            g.DrawRoundedRectangle(border, rect, radius);
        }

        using var textBrush = new SolidBrush(filled ? Color.White : (_idleTextColor ?? colors.TextPrimary));
        g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        });
    }
}
