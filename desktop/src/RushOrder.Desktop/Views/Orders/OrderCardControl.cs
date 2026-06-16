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
    private static readonly Color BorderIdle   = Color.FromArgb(220, 220, 220);
    private static readonly Color BorderHover  = Color.FromArgb(180, 180, 180);
    private bool _hovered;

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
        BackColor      = Color.White;
        Margin         = new Padding(6, 6, 6, 0);
        Cursor         = Cursors.Hand;
        DoubleBuffered = true;

        Build();
        UpdateTimer();
    }

    private void Build()
    {
        // ── Header: number + timer ────────────────────────────────────────
        _pnlHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
        _lblNumber = new Label
        {
            Text      = $"#{_order.OrderNumber}",
            Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(10, 8),
            AutoSize  = true,
        };
        _lblTimer = new Label
        {
            Text      = "00:00",
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
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
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Italic),
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
            Location  = new Point(8, 4),
            AutoSize  = true,
        };
        _pnlAllergens = new Panel { Location = new Point(0, 0), BackColor = Color.Transparent, AutoSize = true };
        _pnlFooter.Controls.Add(_lblWaiter);
        _pnlFooter.Resize += (_, _) =>
            _pnlAllergens.Location = new Point(_pnlFooter.Width - _pnlAllergens.Width - 8, 3);

        RebuildAllergens();

        // ── Actions ───────────────────────────────────────────────────────
        _pnlActions = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent, Padding = new Padding(8, 4, 8, 4) };
        RebuildActions();

        Controls.AddRange([_pnlHeader, _lblTable, _pnlItems, _lblMoreItems, _pnlFooter, _pnlActions]);

        if (_order.IsOffline)
        {
            var lbl = new Label
            {
                Text      = "⬤  PENDIENTE DE SINCRONIZACIÓN",
                Font      = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(204, 102, 0),
                Dock      = DockStyle.Bottom,
                Height    = 17,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(lbl);
        }

        // Separator line at top
        Paint += (_, e) =>
        {
            var statusColor = GetStatusColor(_order.Status);
            using var b = new SolidBrush(statusColor);
            e.Graphics.FillRectangle(b, 0, 0, 4, Height);

            using var pen = new Pen(_hovered ? BorderHover : BorderIdle, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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
            var row = new Panel { Width = _pnlItems.Width - 20, Height = 18, Location = new Point(0, y), BackColor = Color.Transparent };
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
                Font      = new Font("Segoe UI", 6f, FontStyle.Bold),
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
                _pnlActions.Controls.Add(MakeAction("Confirmar", _theme.Colors.Success, () => ConfirmClicked?.Invoke(this), 0));
                _pnlActions.Controls.Add(MakeAction("Cancelar",  _theme.Colors.Error,   () => CancelClicked?.Invoke(this), 1));
                break;
            case OrderStatus.Preparing:
                _pnlActions.Controls.Add(MakeFullAction("Marcar listo ✓", _theme.Colors.Info, () => MarkReadyClicked?.Invoke(this)));
                break;
            case OrderStatus.Ready:
                _pnlActions.Controls.Add(MakeFullAction("Marcar servido", _theme.Colors.Success, () => MarkServedClicked?.Invoke(this)));
                break;
            case OrderStatus.Served:
                _pnlActions.Controls.Add(MakeFullAction("Ir a cobrar →", _theme.Colors.Primary, () => GoToPayClicked?.Invoke(this)));
                break;
            case OrderStatus.Paid:
                _pnlActions.Height = 0;
                break;
        }
    }

    private Button MakeAction(string text, Color color, Action onClick, int position)
    {
        var w   = (_pnlActions.Width - 20) / 2;
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(w, 26),
            Location  = new Point(position * (w + 4), 0),
            BackColor = Color.FromArgb(20, color.R, color.G, color.B),
            ForeColor = color,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(60, color.R, color.G, color.B);
        btn.FlatAppearance.BorderSize  = 1;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button MakeFullAction(string text, Color color, Action onClick)
    {
        var btn = new Button
        {
            Text      = text,
            Dock      = DockStyle.Fill,
            Height    = 26,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
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
