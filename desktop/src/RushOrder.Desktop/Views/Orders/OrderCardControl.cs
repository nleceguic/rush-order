using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders;

public sealed class OrderCardControl : UserControl
{
    private readonly ThemeManager _theme;
    private OrderDto _order;
    private bool _expanded;

    // Controls
    private Panel  _pnlHeader   = null!;
    private Label  _lblNumber   = null!;
    private Label  _lblTimer    = null!;
    private Label  _lblTable    = null!;
    private Panel  _pnlItems    = null!;
    private Label  _lblMoreItems = null!;
    private Panel  _pnlFooter  = null!;
    private Label  _lblWaiter  = null!;
    private Panel  _pnlAllergens = null!;
    private Panel  _pnlActions  = null!;

    private const int CollapsedItems = 3;
    private const float CornerRadius = 10f;
    // The status stripe is drawn over part of _pnlActions.Padding.Left rather than beside it,
    // so lining a button's left edge up with the raw padding value alone left it visibly
    // closer to the stripe than to the padding-only gap on the right. Applies to every action
    // button layout (paired Confirmar/Cancelar and the single full-width ones) uniformly.
    private const int StripeCompensation = 6;
    // Was a light-gray pair (220/180) sized for a white card — the card itself was
    // Color.White too, so the whole thing sat in light mode inside an otherwise all-dark app.
    private static readonly Color BorderIdle   = Color.FromArgb(60, 60, 60);
    private static readonly Color BorderHover  = Color.FromArgb(100, 100, 100);
    private bool _hovered;

    // Every one of these used to be a fresh `PoppinsFont.New(...)` call per card (and per
    // allergen badge, per action button) — Font construction isn't free, and worse, none of
    // them were ever disposed (Control.Dispose() doesn't cascade to a Font assigned to it,
    // same as any other GDI handle). Kanban cards get fully rebuilt on every 30s auto-refresh
    // and every status change, so that leaked a GDI font handle per label on every single
    // rebuild — compounding for as long as the Pedidos screen stayed open, which is exactly the
    // kind of thing that makes something feel progressively slower the longer it's used.
    // Shared static instances, created once, are never disposed — same rule as
    // ThemeManager.Fonts.*.
    private static readonly Font NumberFont    = PoppinsFont.New("Poppins", 10.5f, FontStyle.Bold);
    private static readonly Font TimerFont     = PoppinsFont.New("Poppins", 9f, FontStyle.Bold);
    private static readonly Font MoreItemsFont = PoppinsFont.New("Poppins", 7.5f, FontStyle.Italic);
    private static readonly Font AllergenFont  = PoppinsFont.New("Poppins", 6f, FontStyle.Bold);
    private static readonly Font ActionFont    = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold);
    private static readonly Font FullActionFont = PoppinsFont.New("Poppins", 8f, FontStyle.Bold);
    private static readonly Font OfflineFont   = PoppinsFont.New("Poppins", 7f, FontStyle.Bold);

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

        Width          = 220;
        BackColor      = _theme.Colors.Surface;
        // Left is 0, not 6 — the card should start flush with the column's left edge (matching
        // the header/workload-bar), not inset from it.
        Margin         = new Padding(0, 6, 6, 0);
        Cursor         = Cursors.Hand;
        DoubleBuffered = true;

        Build();
        UpdateTimer();

        // OnPaint draws a rounded card, but the child labels/buttons/panels fill their own
        // square rectangles right up to the edges — clip the whole control to the same rounded
        // shape so their sharp corners never show past it (same pattern as KpiWidget).
        Resize += (_, _) => ApplyRoundedRegion();
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = GdiExtensions.CreateRoundedRect(new RectangleF(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }  // suppress default square fill

    private void Build()
    {
        // ── Header: number + timer ────────────────────────────────────────
        _pnlHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
        _lblNumber = new Label
        {
            Text      = $"#{_order.OrderNumber}",
            Font      = NumberFont,
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(10, 8),
            AutoSize  = true,
        };
        _lblTimer = new Label
        {
            Text      = "00:00",
            Font      = TimerFont,
            ForeColor = _theme.Colors.Success,
            AutoSize  = true,
        };
        _pnlHeader.Controls.AddRange([_lblNumber, _lblTimer]);
        _pnlHeader.Resize += (_, _) =>
            _lblTimer.Location = new Point(_pnlHeader.Width - _lblTimer.Width - 8, 9);

        // ── Table row ─────────────────────────────────────────────────────
        _lblTable = new Label
        {
            Text      = $"{_order.TableName}  ·  {_order.GuestCount} pax",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 20,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
        };

        // ── Items panel ───────────────────────────────────────────────────
        _pnlItems = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent, Padding = new Padding(10, 4, 10, 0) };
        _lblMoreItems = new Label
        {
            Text      = "",
            Font      = MoreItemsFont,
            ForeColor = _theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 18,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
            Visible   = false,
        };

        RebuildItems();

        // ── Footer: waiter + allergens ────────────────────────────────────
        _pnlFooter = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent };
        _lblWaiter = new Label
        {
            Text      = $"♟ {_order.WaiterName ?? "Sin asignar"}",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            // 6, not 10 — same font/size as the item rows, but the "♟" glyph itself carries
            // ~4px more left bearing than a plain digit, so equal Location.X values still left
            // the visible ink sitting further right (measured: 265 vs 261 on screen).
            Location  = new Point(6, 4),
            AutoSize  = true,
        };
        _pnlAllergens = new Panel { Location = new Point(0, 0), BackColor = Color.Transparent, AutoSize = true };
        _pnlFooter.Controls.Add(_lblWaiter);
        _pnlFooter.Resize += (_, _) =>
            _pnlAllergens.Location = new Point(_pnlFooter.Width - _pnlAllergens.Width - 8, 3);

        RebuildAllergens();

        // ── Actions ───────────────────────────────────────────────────────
        _pnlActions = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent, Padding = new Padding(8, 4, 8, 4) };
        _pnlActions.Resize += (_, _) =>
        {
            if (_pnlActions.Controls.Count == 2)
                LayoutPairedActions(_pnlActions.Controls[0], _pnlActions.Controls[1]);
            else if (_pnlActions.Controls.Count == 1)
                LayoutSingleAction(_pnlActions.Controls[0]);
        };
        RebuildActions();

        Controls.AddRange([_pnlHeader, _lblTable, _pnlItems, _lblMoreItems, _pnlFooter, _pnlActions]);

        if (_order.IsOffline)
        {
            var lbl = new Label
            {
                Text      = "⬤  PENDIENTE DE SINCRONIZACIÓN",
                Font      = OfflineFont,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(204, 102, 0),
                Dock      = DockStyle.Bottom,
                Height    = 17,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(lbl);
        }

        // Rounded card. OnPaintBackground is suppressed above, so the plain BackColor fill has
        // to happen here too, or the corners outside the rounded path would be left unpainted.
        Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SetQuality();

            // Inset 1px from the Region's own bounds (0,0,Width,Height) — drawing right up to
            // the region's edge left the anti-aliased fringe with nowhere to blend, so the hard
            // (non-AA) region mask clipped it away and left a jagged stairstep instead.
            var full = new RectangleF(1, 1, Width - 2, Height - 2);
            var statusColor = GetStatusColor(_order.Status);

            // The accent "stripe" is two overlapping anti-aliased fills, not a flat rectangle
            // clipped against the rounded corner — a Graphics clip/Region is never anti-aliased
            // in GDI+ regardless of SmoothingMode, so a straight-edged stripe drawn over the
            // curve got hard-cut into the same jagged stairstep. Filling the whole shape with
            // the accent color first, then covering all but a ~4px left sliver with the card's
            // own background, lets that sliver curve smoothly around the same corner instead.
            using (var accentBrush = new SolidBrush(statusColor))
                g.FillRoundedRectangle(accentBrush, full, CornerRadius);

            var inner = new RectangleF(full.X + 4, full.Y, full.Width - 4, full.Height);
            using (var bg = new SolidBrush(BackColor))
                g.FillRoundedRectangle(bg, inner, CornerRadius);

            using var pen = new Pen(_hovered ? BorderHover : BorderIdle, 1f);
            g.DrawRoundedRectangle(pen, full, CornerRadius);
        };

        RecalcHeight();
        MouseEnter += (_, _) => { _hovered = true;  Invalidate(); };
        MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
        MouseDown  += OnMouseDown;
        MouseMove  += OnMouseMove;
        MouseUp    += (_, _) => { };
        Click      += (_, _) => ToggleExpand();
        DoubleClick += (_, _) => DetailClicked?.Invoke(this);
        ContextMenuStrip = BuildContextMenu();
        PassMouseToChildren();
    }

    private void PassMouseToChildren()
    {
        foreach (Control c in Controls)
        {
            c.MouseEnter  += (_, _) => { _hovered = true;  Invalidate(); };
            c.MouseLeave  += (_, _) => { _hovered = false; Invalidate(); };
            c.MouseDown   += OnMouseDown;
            c.MouseMove   += OnMouseMove;
            c.Click       += (_, _) => ToggleExpand();
            c.DoubleClick += (_, _) => DetailClicked?.Invoke(this);
        }
    }

    // ── Item rows ─────────────────────────────────────────────────────────

    private void RebuildItems()
    {
        _pnlItems.Controls.Clear();
        var show  = _expanded ? _order.Items : _order.Items.Take(CollapsedItems).ToList();
        int y     = 0;

        foreach (var item in show)
        {
            // _pnlItems.Padding.Left (10) only affects auto-laid-out children — this row is
            // positioned via explicit Location, so it silently ignored the padding and sat
            // flush against the card's left edge instead of lined up with the header/table
            // text above it.
            var row = new Panel { Width = _pnlItems.Width - 20, Height = 18, Location = new Point(_pnlItems.Padding.Left, y), BackColor = Color.Transparent };
            var qty = new Label { Text = $"{item.Quantity}×", Font = _theme.Fonts.Small, ForeColor = _theme.Colors.Primary, Location = new Point(0, 1), AutoSize = true };
            var name = new Label { Text = item.ProductName, Font = _theme.Fonts.Small, ForeColor = _theme.Colors.TextPrimary, Location = new Point(22, 1), AutoSize = true };
            var price = new Label { Text = $"€{item.LineTotal:N0}", Font = _theme.Fonts.Small, ForeColor = _theme.Colors.TextSecondary, AutoSize = true };
            row.Controls.AddRange([qty, name, price]);
            row.Resize += (_, _) => price.Location = new Point(row.Width - price.Width, 1);
            _pnlItems.Controls.Add(row);
            y += 20;
        }

        _pnlItems.Height = y + 8;

        var hidden = _order.Items.Count - CollapsedItems;
        _lblMoreItems.Visible = !_expanded && hidden > 0;
        _lblMoreItems.Text    = _lblMoreItems.Visible ? $"  + {hidden} artículo{(hidden > 1 ? "s" : "")} más…" : "";
    }

    // ── Allergen badges ───────────────────────────────────────────────────

    private void RebuildAllergens()
    {
        _pnlAllergens.Controls.Clear();
        var allergens = _order.Items.SelectMany(i => i.Allergens).Distinct().Take(4).ToList();
        int x = 0;
        foreach (var a in allergens)
        {
            var badge = new Label
            {
                Text      = AllergenAbbr(a),
                Size      = new Size(20, 16),
                Location  = new Point(x, 0),
                BackColor = AllergenColor(a),
                ForeColor = Color.White,
                Font      = AllergenFont,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _pnlAllergens.Controls.Add(badge);
            x += 22;
        }
        _pnlAllergens.Width = Math.Max(1, x);
        _pnlFooter.Controls.Add(_pnlAllergens);
    }

    // ── Action buttons ────────────────────────────────────────────────────

    private void RebuildActions()
    {
        _pnlActions.Controls.Clear();
        _pnlActions.Height = 36;

        switch (_order.Status)
        {
            case OrderStatus.New:
                var confirm = MakeAction("Confirmar", _theme.Colors.Success, () => ConfirmClicked?.Invoke(this));
                var cancel  = MakeAction("Cancelar",  _theme.Colors.Error,   () => CancelClicked?.Invoke(this));
                _pnlActions.Controls.AddRange([confirm, cancel]);
                LayoutPairedActions(confirm, cancel);
                break;
            case OrderStatus.Preparing:
                var ready = MakeFullAction("Marcar listo", _theme.Colors.Info, () => MarkReadyClicked?.Invoke(this));
                _pnlActions.Controls.Add(ready);
                LayoutSingleAction(ready);
                break;
            case OrderStatus.Ready:
                var served = MakeFullAction("Marcar servido", _theme.Colors.Success, () => MarkServedClicked?.Invoke(this));
                _pnlActions.Controls.Add(served);
                LayoutSingleAction(served);
                break;
            case OrderStatus.Served:
                var pay = MakeFullAction("Ir a cobrar", _theme.Colors.Primary, () => GoToPayClicked?.Invoke(this));
                _pnlActions.Controls.Add(pay);
                LayoutSingleAction(pay);
                break;
            case OrderStatus.Paid:
                _pnlActions.Height = 0;
                break;
        }
    }

    // Reads _pnlActions.Width/Height, so it must run once the panel is actually parented and
    // Dock-laid-out — computing this eagerly inside MakeAction read Panel's own *unparented*
    // DefaultSize (200×100, not the card's real width), giving Cancelar a right-hand gap a few
    // pixels off from Confirmar's left-hand one. Hooked to _pnlActions.Resize (below) so it
    // also stays correct if the card itself is resized later (KanbanColumn does this after
    // construction, once the column's real width is known).
    private void LayoutPairedActions(Control first, Control second)
    {
        var left = _pnlActions.Padding.Left + StripeCompensation;
        var w = (_pnlActions.Width - left - _pnlActions.Padding.Right - 4) / 2;
        var h = _pnlActions.Height - _pnlActions.Padding.Vertical;
        first.Size      = new Size(w, h);
        first.Location  = new Point(left, _pnlActions.Padding.Top);
        second.Size     = new Size(w, h);
        second.Location = new Point(left + w + 4, _pnlActions.Padding.Top);
    }

    // Same stripe-compensated inset as the paired layout above, for the single full-width
    // action button (Marcar listo / Marcar servido / Ir a cobrar). No longer Dock=Fill, since
    // that would inset evenly off _pnlActions.Padding and reproduce the same left/right
    // mismatch the paired buttons had.
    private void LayoutSingleAction(Control btn)
    {
        var left = _pnlActions.Padding.Left + StripeCompensation;
        btn.Size     = new Size(_pnlActions.Width - left - _pnlActions.Padding.Right,
                                 _pnlActions.Height - _pnlActions.Padding.Vertical);
        btn.Location = new Point(left, _pnlActions.Padding.Top);
    }

    private CardActionButton MakeAction(string text, Color color, Action onClick)
    {
        var btn = new CardActionButton(text, ActionFont, BackColor,
            fill: Color.FromArgb(20, color.R, color.G, color.B),
            foreColor: color,
            border: Color.FromArgb(60, color.R, color.G, color.B));
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private CardActionButton MakeFullAction(string text, Color color, Action onClick)
    {
        var btn = new CardActionButton(text, FullActionFont, BackColor, fill: color, foreColor: Color.White, border: null);
        btn.Click += (_, _) => onClick();
        return btn;
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
        _lblNumber.Text = $"#{_order.OrderNumber}";
        _lblTable.Text  = $"{_order.TableName}  ·  {_order.GuestCount} pax";
        _lblWaiter.Text = $"♟ {_order.WaiterName ?? "Sin asignar"}";
        RebuildItems();
        RebuildAllergens();
        RebuildActions();
        RecalcHeight();
        Invalidate();
    }

    public void UpdateTimer()
    {
        var age     = _order.Age;
        var minutes = (int)age.TotalMinutes;
        var txt     = age.TotalHours >= 1
            ? $"{(int)age.TotalHours}:{age.Minutes:D2}h"
            : $"{minutes:D2}:{age.Seconds:D2}";

        _lblTimer.Text      = txt;
        _lblTimer.ForeColor = minutes < 10 ? _theme.Colors.Success
                            : minutes < 20 ? _theme.Colors.Warning
                                           : _theme.Colors.Error;
        _pnlHeader.Invalidate();
    }

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        RebuildItems();
        RecalcHeight();
    }

    private void RecalcHeight()
    {
        int h = _pnlHeader.Height + _lblTable.Height + _pnlItems.Height
              + (_lblMoreItems.Visible ? _lblMoreItems.Height : 0)
              + _pnlFooter.Height + _pnlActions.Height + 8;
        Height = h;
        Parent?.PerformLayout();
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _mouseDownPt = PointToScreen(e.Location);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var cur   = PointToScreen(e.Location);
        var delta = new Size(cur.X - _mouseDownPt.X, cur.Y - _mouseDownPt.Y);
        if (Math.Abs(delta.Width) > 8 || Math.Abs(delta.Height) > 8)
            DoDragDrop(_order, DragDropEffects.Move);
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

// Plain Button + Region-clip rounding produces a jagged, non-anti-aliased corner (same issue
// fixed on the card's own outer shape) — but Region on a control whose parent panel has
// BackColor=Transparent (as _pnlActions does) fought with WinForms' background-relay mechanism
// for transparent controls and produced garbled/overlapping paint instead. Since there's
// nothing translucent behind these buttons, the simplest fix skips Region entirely: paint the
// real backdrop color as a plain (opaque, default) background, then draw the rounded shape and
// text on top — the anti-aliased path renders cleanly with no clipping involved at all.
internal sealed class CardActionButton : Control
{
    private const float Radius = 8f;
    private readonly Color  _fill;
    private readonly Color? _border;
    private readonly Color  _foreColor;

    public CardActionButton(string text, Font font, Color backdrop, Color fill, Color foreColor, Color? border)
    {
        Text       = text;
        Font       = font;
        BackColor  = backdrop;
        _fill      = fill;
        _foreColor = foreColor;
        _border    = border;
        Cursor     = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using (var brush = new SolidBrush(_fill))
            g.FillRoundedRectangle(brush, r, Radius);

        if (_border is { } borderColor)
        {
            using var pen = new Pen(borderColor, 1f);
            g.DrawRoundedRectangle(pen, r, Radius);
        }

        using var textBrush = new SolidBrush(_foreColor);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), format);
    }
}
