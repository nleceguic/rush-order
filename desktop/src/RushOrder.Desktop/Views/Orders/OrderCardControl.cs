using System.Drawing.Drawing2D;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders;

// Fully self-painted — no child Label/Panel/Button controls. It used to be built from ~15-20 of
// them (header labels, one Panel+3 Labels per item row, allergen badges, action buttons...).
// Each is a real native HWND: creating and painting a column's worth of cards meant creating and
// painting a hundred-plus actual Windows every reload, which is what made the board visibly
// populate over several frames instead of appearing at once, no matter how well the reload
// itself was batched upstream (see KanbanColumn.AddCards). One control, one OnPaint, one WM_PAINT
// per card fixes that at the root — same approach as PillButton/PillComboBox/ToggleSwitch.
public sealed class OrderCardControl : UserControl
{
    private readonly ThemeManager _theme;
    private OrderDto _order;
    private bool _expanded;
    private bool _hovered;

    private const int CollapsedItems = 3;
    private const float CornerRadius = 10f;
    private const float ActionRadius = 8f;
    // The status stripe eats into _pnlActions.Padding.Left rather than sitting beside it, so
    // lining a button's left edge up with the raw padding value alone left it visibly closer to
    // the stripe than to the padding-only gap on the right. Applies to every action button
    // layout (paired Confirmar/Cancelar and the single full-width ones) uniformly.
    private const int StripeCompensation = 6;
    private static readonly Color BorderIdle  = Color.FromArgb(60, 60, 60);
    private static readonly Color BorderHover = Color.FromArgb(100, 100, 100);

    // Shared, never disposed — same rule as ThemeManager.Fonts.*: a fresh Font per card (and per
    // allergen badge, per action button) was never disposed either, leaking a GDI handle on every
    // single rebuild.
    private static readonly Font NumberFont     = PoppinsFont.New("Poppins", 10.5f, FontStyle.Bold);
    private static readonly Font TimerFont      = PoppinsFont.New("Poppins", 9f, FontStyle.Bold);
    private static readonly Font MoreItemsFont  = PoppinsFont.New("Poppins", 7.5f, FontStyle.Italic);
    private static readonly Font AllergenFont   = PoppinsFont.New("Poppins", 6f, FontStyle.Bold);
    private static readonly Font ActionFont     = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold);
    private static readonly Font FullActionFont = PoppinsFont.New("Poppins", 8f, FontStyle.Bold);
    private static readonly Font OfflineFont    = PoppinsFont.New("Poppins", 7f, FontStyle.Bold);

    // For measuring text (right-aligning the timer, sizing action buttons, etc.) without a real
    // Graphics — CreateGraphics() would force this control's own HWND into existence before it's
    // even parented. A throwaway 1x1 bitmap, shared and never disposed, does the same job.
    private static readonly Bitmap  MeasureBmp = new(1, 1);
    private static readonly Graphics MeasureGx = Graphics.FromImage(MeasureBmp);

    // ── Computed layout (recalculated in RecalcLayout whenever size/order/expanded changes) ──
    private string _numberText = "";
    private PointF _numberPos;
    private string _timerText = "";
    private PointF _timerPos;
    private Color  _timerColor;
    private string _tableText = "";
    private float  _tableY;
    private readonly List<(RectangleF Row, string Qty, string Name, string Price)> _itemRows = [];
    private bool   _moreItemsVisible;
    private string _moreItemsText = "";
    private float  _moreItemsY;
    private string _waiterText = "";
    private float  _footerY;
    private readonly List<(RectangleF Rect, string Abbr, Color Color)> _allergenBadges = [];
    private readonly List<ActionButtonLayout> _actionButtons = [];

    private sealed record ActionButtonLayout(
        RectangleF Rect, string Text, Font Font, Color Fill, Color? Border, Color ForeColor, Action OnClick);

    // Drag support
    private Point _mouseDownPt;

    // Events
    public event Action<OrderCardControl>? ConfirmClicked;
    public event Action<OrderCardControl>? CancelClicked;
    public event Action<OrderCardControl>? MarkReadyClicked;
    public event Action<OrderCardControl>? MarkServedClicked;
    public event Action<OrderCardControl>? GoToPayClicked;
    public event Action<OrderCardControl>? PrintComandaClicked;
    public event Action<OrderCardControl>? AssignWaiterClicked;
    public event Action<OrderCardControl>? AddItemClicked;
    public event Action<OrderCardControl>? DetailClicked;

    public OrderDto Order => _order;

    public OrderCardControl(OrderDto order, ThemeManager theme)
    {
        _order = order;
        _theme = theme;

        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

        Width     = 220;
        BackColor = _theme.Colors.Surface;
        // Left is 0, not 6 — the card should start flush with the column's left edge (matching
        // the header/workload-bar), not inset from it.
        Margin    = new Padding(0, 6, 6, 0);
        Cursor    = Cursors.Hand;

        _timerText  = "00:00";
        _timerColor = _theme.Colors.Success;

        ContextMenuStrip = BuildContextMenu();
        Resize += (_, _) => RecalcLayout();
        RecalcLayout();
        UpdateTimer();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void RecalcLayout()
    {
        const int pad = 10;
        float y = 0;

        // Header
        _numberText = $"#{_order.OrderNumber}";
        _numberPos  = new PointF(pad, y + 8);
        RecalcTimerPosition();
        y += 36;

        // Table row
        _tableText = $"{_order.TableName}  ·  {_order.GuestCount} pax";
        _tableY    = y;
        y += 20;

        // Items
        _itemRows.Clear();
        var show  = _expanded ? _order.Items : _order.Items.Take(CollapsedItems).ToList();
        var itemsTop = y;
        float localY = 0;
        foreach (var item in show)
        {
            var row = new RectangleF(pad, itemsTop + localY, Width - 2 * pad, 18);
            _itemRows.Add((row, $"{item.Quantity}×", item.ProductName, $"€{item.LineTotal:N0}"));
            localY += 20;
        }
        y += localY + 8;

        // "+ N more"
        var hidden = _order.Items.Count - CollapsedItems;
        _moreItemsVisible = !_expanded && hidden > 0;
        _moreItemsText    = _moreItemsVisible ? $"  + {hidden} artículo{(hidden > 1 ? "s" : "")} más…" : "";
        if (_moreItemsVisible)
        {
            _moreItemsY = y;
            y += 18;
        }

        // Footer: waiter + allergens
        _footerY    = y;
        _waiterText = $"♟ {_order.WaiterName ?? "Sin asignar"}";
        RecalcAllergens();
        y += 22;

        // Actions
        RecalcActions(y, out var actionsHeight);
        y += actionsHeight;

        y += 8;
        Height = (int)Math.Ceiling(y);
        Parent?.PerformLayout();
        Invalidate();
    }

    private void RecalcTimerPosition()
    {
        var size = MeasureGx.MeasureString(_timerText, TimerFont);
        _timerPos = new PointF(Width - size.Width - 8, 9);
    }

    private void RecalcAllergens()
    {
        _allergenBadges.Clear();
        var allergens = _order.Items.SelectMany(i => i.Allergens).Distinct().Take(4).ToList();
        var totalWidth = allergens.Count * 22;
        var left = Width - totalWidth - 8;
        var x = 0f;
        foreach (var a in allergens)
        {
            _allergenBadges.Add((new RectangleF(left + x, _footerY + 3, 20, 16), AllergenAbbr(a), AllergenColor(a)));
            x += 22;
        }
    }

    private void RecalcActions(float top, out float actionsHeight)
    {
        _actionButtons.Clear();
        if (_order.Status == OrderStatus.Paid) { actionsHeight = 0; return; }

        actionsHeight = 36;
        const int padLeft = 8, padRight = 8, padVertical = 4;
        var left = padLeft + StripeCompensation;
        var h    = actionsHeight - padVertical * 2;
        var rowY = top + padVertical;

        switch (_order.Status)
        {
            case OrderStatus.New:
                var w = (Width - left - padRight - 4) / 2f;
                _actionButtons.Add(MakeAction(new RectangleF(left, rowY, w, h),
                    "Confirmar", ActionFont, _theme.Colors.Success, () => ConfirmClicked?.Invoke(this)));
                _actionButtons.Add(MakeAction(new RectangleF(left + w + 4, rowY, w, h),
                    "Cancelar", ActionFont, _theme.Colors.Error, () => CancelClicked?.Invoke(this)));
                break;
            case OrderStatus.Preparing:
                _actionButtons.Add(MakeFullAction(new RectangleF(left, rowY, Width - left - padRight, h),
                    "Marcar listo", _theme.Colors.Info, () => MarkReadyClicked?.Invoke(this)));
                break;
            case OrderStatus.Ready:
                _actionButtons.Add(MakeFullAction(new RectangleF(left, rowY, Width - left - padRight, h),
                    "Marcar servido", _theme.Colors.Success, () => MarkServedClicked?.Invoke(this)));
                break;
            case OrderStatus.Served:
                _actionButtons.Add(MakeFullAction(new RectangleF(left, rowY, Width - left - padRight, h),
                    "Ir a cobrar", _theme.Colors.Primary, () => GoToPayClicked?.Invoke(this)));
                break;
        }
    }

    private static ActionButtonLayout MakeAction(RectangleF rect, string text, Font font, Color color, Action onClick) =>
        new(rect, text, font,
            Fill: Color.FromArgb(20, color.R, color.G, color.B),
            Border: Color.FromArgb(60, color.R, color.G, color.B),
            ForeColor: color,
            OnClick: onClick);

    private ActionButtonLayout MakeFullAction(RectangleF rect, string text, Color color, Action onClick) =>
        new(rect, text, FullActionFont, Fill: color, Border: null, ForeColor: Color.White, OnClick: onClick);

    // ── Painting ─────────────────────────────────────────────────────────

    protected override void OnPaintBackground(PaintEventArgs e) { }  // suppress default square fill

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        // Inset 1px from the control's own bounds — drawing right up to the edge left the
        // anti-aliased fringe with nothing to blend into, giving a jagged stairstep corner.
        var full = new RectangleF(1, 1, Width - 2, Height - 2);
        var statusColor = GetStatusColor(_order.Status);

        // The accent "stripe" is two overlapping anti-aliased fills, not a flat rectangle
        // clipped against the rounded corner — a Graphics clip/Region is never anti-aliased in
        // GDI+ regardless of SmoothingMode, so a straight-edged stripe drawn over the curve got
        // hard-cut into the same jagged stairstep. Filling the whole shape with the accent color
        // first, then covering all but a ~4px left sliver with the card's own background, lets
        // that sliver curve smoothly around the same corner instead.
        using (var accentBrush = new SolidBrush(statusColor))
            g.FillRoundedRectangle(accentBrush, full, CornerRadius);
        var inner = new RectangleF(full.X + 4, full.Y, full.Width - 4, full.Height);
        using (var bg = new SolidBrush(BackColor))
            g.FillRoundedRectangle(bg, inner, CornerRadius);
        using (var pen = new Pen(_hovered ? BorderHover : BorderIdle, 1f))
            g.DrawRoundedRectangle(pen, full, CornerRadius);

        using var textPrimary   = new SolidBrush(_theme.Colors.TextPrimary);
        using var textSecondary = new SolidBrush(_theme.Colors.TextSecondary);
        using var primary       = new SolidBrush(_theme.Colors.Primary);

        g.DrawString(_numberText, NumberFont, textPrimary, _numberPos);
        using (var timerBrush = new SolidBrush(_timerColor))
            g.DrawString(_timerText, TimerFont, timerBrush, _timerPos);

        g.DrawString(_tableText, _theme.Fonts.Small, textSecondary, new PointF(10, _tableY));

        foreach (var (row, qty, name, price) in _itemRows)
        {
            g.DrawString(qty, _theme.Fonts.Small, primary, new PointF(row.X, row.Y + 1));
            g.DrawString(name, _theme.Fonts.Small, textPrimary, new PointF(row.X + 22, row.Y + 1));
            var priceSize = g.MeasureString(price, _theme.Fonts.Small);
            g.DrawString(price, _theme.Fonts.Small, textSecondary, new PointF(row.Right - priceSize.Width, row.Y + 1));
        }

        if (_moreItemsVisible)
            g.DrawString(_moreItemsText, MoreItemsFont, textSecondary, new PointF(10, _moreItemsY));

        // ♟, not a plain digit — carries ~4px more left bearing, so an equal X to the item rows
        // still left the visible ink sitting further right (measured: 265 vs 261 on screen).
        g.DrawString(_waiterText, _theme.Fonts.Small, textSecondary, new PointF(6, _footerY + 4));

        var centerFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        foreach (var (rect, abbr, color) in _allergenBadges)
        {
            using var badgeBrush = new SolidBrush(color);
            g.FillRectangle(badgeBrush, rect);
            using var badgeText = new SolidBrush(Color.White);
            g.DrawString(abbr, AllergenFont, badgeText, rect, centerFmt);
        }

        foreach (var btn in _actionButtons)
        {
            using var fillBrush = new SolidBrush(btn.Fill);
            g.FillRoundedRectangle(fillBrush, btn.Rect, ActionRadius);
            if (btn.Border is { } borderColor)
            {
                using var borderPen = new Pen(borderColor, 1f);
                g.DrawRoundedRectangle(borderPen, btn.Rect, ActionRadius);
            }
            using var btnText = new SolidBrush(btn.ForeColor);
            g.DrawString(btn.Text, btn.Font, btnText, btn.Rect, centerFmt);
        }

        if (_order.IsOffline)
        {
            var bannerRect = new RectangleF(0, Height - 17, Width, 17);
            // Clipped to the same rounded path as the card itself (not independently rounded)
            // so its bottom corners follow exactly the same curve — a banner rounded on its own
            // would clamp to a different radius against its own 17px height and leave a seam.
            using var cardPath = GdiExtensions.CreateRoundedRect(full, CornerRadius);
            var state = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            using (var bannerBg = new SolidBrush(Color.FromArgb(204, 102, 0)))
                g.FillRectangle(bannerBg, bannerRect);
            using (var bannerText = new SolidBrush(Color.White))
                g.DrawString("⬤  PENDIENTE DE SINCRONIZACIÓN", OfflineFont, bannerText, bannerRect, centerFmt);
            g.Restore(state);
        }
    }

    // ── Context menu ──────────────────────────────────────────────────────

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip { Font = _theme.Fonts.Regular };
        menu.Items.Add("Imprimir comanda",  null, (_, _) => PrintComandaClicked?.Invoke(this));
        menu.Items.Add("Asignar camarero",  null, (_, _) => AssignWaiterClicked?.Invoke(this));
        menu.Items.Add("Añadir artículo",   null, (_, _) => AddItemClicked?.Invoke(this));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Cancelar pedido",   null, (_, _) => CancelClicked?.Invoke(this));
        return menu;
    }

    // ── Public update methods ─────────────────────────────────────────────

    public void UpdateOrder(OrderDto order)
    {
        _order = order;
        RecalcLayout();
    }

    public void UpdateTimer()
    {
        var age     = _order.Age;
        var minutes = (int)age.TotalMinutes;
        _timerText  = age.TotalHours >= 1
            ? $"{(int)age.TotalHours}:{age.Minutes:D2}h"
            : $"{minutes:D2}:{age.Seconds:D2}";
        _timerColor = minutes < 10 ? _theme.Colors.Success
                    : minutes < 20 ? _theme.Colors.Warning
                                   : _theme.Colors.Error;
        RecalcTimerPosition();
        Invalidate(new Rectangle(0, 0, Width, 36));
    }

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        RecalcLayout();
    }

    // ── Mouse handling ────────────────────────────────────────────────────

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _mouseDownPt = PointToScreen(e.Location);
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var cur   = PointToScreen(e.Location);
            var delta = new Size(cur.X - _mouseDownPt.X, cur.Y - _mouseDownPt.Y);
            if (Math.Abs(delta.Width) > 8 || Math.Abs(delta.Height) > 8)
                DoDragDrop(_order, DragDropEffects.Move);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var button = _actionButtons.FirstOrDefault(b => b.Rect.Contains(e.Location));
            if (button is not null) button.OnClick();
            else ToggleExpand();
        }
        base.OnMouseClick(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        DetailClicked?.Invoke(this);
        base.OnMouseDoubleClick(e);
    }

    // ── Statics ───────────────────────────────────────────────────────────

    private static Color GetStatusColor(OrderStatus s) => s switch
    {
        OrderStatus.New       => Color.FromArgb(33, 150, 243),
        OrderStatus.Preparing => Color.FromArgb(255, 152, 0),
        OrderStatus.Ready     => Color.FromArgb(76, 175, 80),
        OrderStatus.Served    => Color.FromArgb(120, 120, 120),
        OrderStatus.Paid      => Color.FromArgb(27, 94, 32),
        _                     => Color.Gray,
    };

    private static string AllergenAbbr(string a) => a switch
    {
        "Gluten"      => "G",
        "Dairy"       => "L",
        "Eggs"        => "H",
        "Nuts"        => "F",
        "Shellfish"   => "M",
        "Fish"        => "P",
        "Sulphites"   => "S",
        "Crustaceans" => "C",
        _             => a[..1].ToUpper(),
    };

    private static Color AllergenColor(string a) => a switch
    {
        "Gluten"      => Color.FromArgb(139, 90, 43),
        "Dairy"       => Color.FromArgb(100, 149, 237),
        "Eggs"        => Color.FromArgb(218, 165, 32),
        "Nuts"        => Color.FromArgb(160, 82, 45),
        "Shellfish"   => Color.FromArgb(205, 92, 92),
        "Fish"        => Color.FromArgb(70, 130, 180),
        "Sulphites"   => Color.FromArgb(147, 112, 219),
        _             => Color.Gray,
    };
}
