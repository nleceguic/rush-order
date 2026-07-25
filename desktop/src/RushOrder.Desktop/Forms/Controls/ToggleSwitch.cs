using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Forms.Controls;

// Pill-shaped on/off switch with no label — a colored track when on, a grey track when off,
// and a round knob that slides between the two ends. Same self-painted approach as
// PillComboBox/PillButton, for the same reason: a native CheckBox has no notion of this shape.
internal sealed class ToggleSwitch : Control
{
    private readonly ThemeManager _theme;
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _knobT; // 0 = off, 1 = on — animated knob position, distinct from Checked
    private bool  _checked;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            _animTimer.Start();
        }
    }

    public event EventHandler? CheckedChanged;

    public ToggleSwitch(ThemeManager theme)
    {
        _theme = theme;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Width  = 40;
        Height = 22;
        Cursor = Cursors.Hand;

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += (_, _) => AnimateStep();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    private void AnimateStep()
    {
        var target = _checked ? 1f : 0f;
        const float step = 0.25f;
        _knobT = Math.Abs(_knobT - target) <= step ? target : _knobT + Math.Sign(target - _knobT) * step;
        Invalidate();
        if (_knobT == target) _animTimer.Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g      = e.Graphics;
        var colors = _theme.Colors;
        g.SetQuality();
        g.Clear(Parent?.BackColor ?? colors.Surface);

        var rect   = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        var radius = Height / 2f;

        // Off-track matches PillButton's idle border color, so the switch reads as part of
        // the same toolbar family as the button sitting right next to it.
        using var trackBrush = new SolidBrush(Lerp(colors.Border, colors.Primary, _knobT));
        g.FillRoundedRectangle(trackBrush, rect, radius);

        var knobDiameter = Height - 5f;
        var minX = rect.X + 2.5f;
        var maxX = rect.Right - 2.5f - knobDiameter;
        var knobRect = new RectangleF(minX + (maxX - minX) * _knobT, rect.Y + 2.5f, knobDiameter, knobDiameter);
        using var knobBrush = new SolidBrush(Color.White);
        g.FillEllipse(knobBrush, knobRect);
    }

    private static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
        a.A + (int)((b.A - a.A) * t),
        a.R + (int)((b.R - a.R) * t),
        a.G + (int)((b.G - a.G) * t),
        a.B + (int)((b.B - a.B) * t));

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }
}
