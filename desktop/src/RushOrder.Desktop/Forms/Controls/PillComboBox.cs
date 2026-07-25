using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Forms.Controls;

// Pill-shaped stand-in for a ComboBoxStyle.DropDownList. A real ComboBox can't be made
// pill-shaped: its native dropdown-arrow button is drawn by Windows on top of any Region
// we set, so clipping the control to a rounded Region just clips the arrow into a
// broken-looking white blob instead of following the curve. This control paints its own
// closed box (so the pill is exact) and opens a ContextMenuStrip for the item list, which
// already closes itself on outside click / Escape / item pick.
internal sealed class PillComboBox : Control
{
    private readonly ThemeManager _theme;
    private int  _selectedIndex = -1;
    private bool _hovered;
    private bool _suspendEvents;

    public List<string> Items { get; } = [];

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value == _selectedIndex) return;
            _selectedIndex = value;
            Invalidate();
            if (!_suspendEvents) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;

    public event EventHandler? SelectedIndexChanged;

    public PillComboBox(ThemeManager theme)
    {
        _theme = theme;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Height = 30;
        Cursor = Cursors.Hand;

        // The pill fill only covers the rounded path — the four bounding-box corners outside
        // that curve stay whatever the base background erase painted, which defaults to
        // SystemColors.Control (light grey) rather than the toolbar's own surface color. Left
        // as default that grey shows through as a bar down each side, worst in dark mode where
        // it contrasts hard against the dark toolbar. Matching BackColor to the toolbar's own
        // Surface makes that leftover area invisible instead of trying to avoid painting it.
        BackColor = theme.Colors.Surface;
    }

    // Mirrors ComboBox.Begin/EndUpdate — swallow the SelectedIndexChanged that resetting
    // SelectedIndex during a bulk repopulate would otherwise fire once per intermediate value.
    public void BeginUpdate() => _suspendEvents = true;
    public void EndUpdate()
    {
        _suspendEvents = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowDropdown();
    }

    private void ShowDropdown()
    {
        if (Items.Count == 0) return;

        var menu = new ContextMenuStrip { Font = _theme.Fonts.Small, ShowImageMargin = false };
        menu.Closed += (_, _) => menu.Dispose();

        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var entry = new ToolStripMenuItem(Items[i]) { Checked = i == _selectedIndex };
            entry.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(entry);
        }

        menu.Show(this, new Point(0, Height + 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g      = e.Graphics;
        var colors = _theme.Colors;
        g.SetQuality();

        // The pill fill only covers the rounded path — the sliver outside the curve at each
        // rounded end (full height, tapering to zero at the vertical midpoint) is left to
        // whatever painted the background. Relying on the default OnPaintBackground/BackColor
        // erase for that didn't take effect here (same UserPaint + AllPaintingInWmPaint
        // combination NavButton works around the same way), leaving it Windows' default grey
        // and showing as a bar down each side. Clearing explicitly first is what actually works.
        g.Clear(colors.Surface);

        var rect   = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        var radius = Height / 2f;

        using var bg = new SolidBrush(colors.Surface);
        g.FillRoundedRectangle(bg, rect, radius);
        using var border = new Pen(_hovered ? colors.Primary : colors.Border, 1f);
        g.DrawRoundedRectangle(border, rect, radius);

        var textRect = new RectangleF(12, 0, Width - 28, Height);
        using var textBrush = new SolidBrush(colors.TextPrimary);
        g.DrawString(SelectedItem ?? "", _theme.Fonts.Small, textBrush, textRect, new StringFormat
        {
            Alignment     = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.EllipsisCharacter,
            FormatFlags   = StringFormatFlags.NoWrap,
        });

        // Chevron, hand-drawn since there's no native dropdown button anymore.
        using var arrowBrush = new SolidBrush(colors.TextSecondary);
        float cx = Width - 14f, cy = Height / 2f;
        PointF[] chevron =
        [
            new PointF(cx - 4, cy - 2),
            new PointF(cx + 4, cy - 2),
            new PointF(cx,     cy + 3),
        ];
        g.FillPolygon(arrowBrush, chevron);
    }
}
