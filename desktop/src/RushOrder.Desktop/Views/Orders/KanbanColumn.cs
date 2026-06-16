using System.Drawing.Drawing2D;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders;

internal sealed class KanbanColumn : UserControl
{
    private readonly ThemeManager _theme;
    private readonly OrderStatus  _status;
    private readonly Color        _accentColor;
    private readonly Color        _bgColor;

    private Panel             _pnlHeader    = null!;
    private Label             _lblTitle     = null!;
    private Label             _lblCount     = null!;
    private Panel             _pnlWorkload  = null!;
    private FlowLayoutPanel   _flow         = null!;
    private bool              _dragOver;

    public event Action<OrderDto, OrderStatus>? CardDropped;
    public event Action<OrderCardControl>?      ConfirmClicked;
    public event Action<OrderCardControl>?      CancelClicked;
    public event Action<OrderCardControl>?      MarkReadyClicked;
    public event Action<OrderCardControl>?      MarkServedClicked;
    public event Action<OrderCardControl>?      GoToPayClicked;
    public event Action<OrderCardControl>?      PrintComandaClicked;
    public event Action<OrderCardControl>?      AssignWaiterClicked;
    public event Action<OrderCardControl>?      AddItemClicked;
    public event Action<OrderCardControl>?      DetailClicked;

    private static readonly (OrderStatus Status, string Label, Color Accent, Color Bg)[] ColumnDefs =
    [
        (OrderStatus.New,       "Nuevo",           Color.FromArgb(59, 130, 246),  Color.FromArgb(239, 246, 255)),
        (OrderStatus.Preparing, "En preparación",  Color.FromArgb(245, 158, 11),  Color.FromArgb(255, 251, 235)),
        (OrderStatus.Ready,     "Listo",           Color.FromArgb(34, 197, 94),   Color.FromArgb(240, 253, 244)),
        (OrderStatus.Served,    "Servido",         Color.FromArgb(148, 163, 184), Color.FromArgb(248, 250, 252)),
        (OrderStatus.Paid,      "Cobrado",         Color.FromArgb(21, 128, 61),   Color.FromArgb(220, 252, 231)),
    ];

    public static KanbanColumn Create(OrderStatus status, ThemeManager theme)
    {
        var def = ColumnDefs.First(d => d.Status == status);
        return new KanbanColumn(theme, status, def.Label, def.Accent, def.Bg);
    }

    public OrderStatus Status => _status;
    public int CardCount => _flow.Controls.Count;

    private KanbanColumn(ThemeManager theme, OrderStatus status, string label, Color accent, Color bg)
    {
        _theme       = theme;
        _status      = status;
        _accentColor = accent;
        _bgColor     = bg;

        Dock        = DockStyle.Fill;
        BackColor   = bg;
        Margin      = new Padding(4, 0, 4, 0);
        AllowDrop   = true;
        MinimumSize = new Size(200, 0);

        Build(label);
        WireDragDrop();
    }

    private void Build(string label)
    {
        // ── Header ────────────────────────────────────────────────────────
        _pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = Color.Transparent,
            Padding   = new Padding(12, 0, 12, 0),
        };
        _pnlHeader.Paint += PaintHeader;

        _lblTitle = new Label
        {
            Text      = label,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = _accentColor,
            Location  = new Point(12, 14),
            AutoSize  = true,
        };
        _lblCount = new Label
        {
            Text      = "0",
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = _accentColor,
            Size      = new Size(24, 20),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _pnlHeader.Controls.AddRange([_lblTitle, _lblCount]);
        _pnlHeader.Resize += (_, _) =>
            _lblCount.Location = new Point(_pnlHeader.Width - 32, 14);

        // ── Workload bar ──────────────────────────────────────────────────
        _pnlWorkload = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 4,
            BackColor = Color.Transparent,
        };
        _pnlWorkload.Paint += PaintWorkload;

        // ── Card flow ─────────────────────────────────────────────────────
        _flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoScroll    = true,
            AllowDrop     = true,
            BackColor     = Color.Transparent,
            Padding       = new Padding(4, 4, 4, 12),
        };

        Controls.AddRange([_pnlHeader, _pnlWorkload, _flow]);

        // Also wire flow panel for DnD
        _flow.DragEnter += OnDragEnter;
        _flow.DragOver  += OnDragOver;
        _flow.DragLeave += OnDragLeave;
        _flow.DragDrop  += OnDragDrop;
    }

    // ── Card management ───────────────────────────────────────────────────

    public void AddCard(OrderDto order)
    {
        var card = CreateCard(order);
        _flow.Controls.Add(card);
        card.Width = _flow.ClientSize.Width - 12;
        _flow.Resize += (_, _) => card.Width = Math.Max(180, _flow.ClientSize.Width - 12);
        UpdateHeader();
    }

    public void RemoveCard(Guid orderId)
    {
        var card = FindCard(orderId);
        if (card is null) return;
        _flow.Controls.Remove(card);
        card.Dispose();
        UpdateHeader();
    }

    public void UpdateCard(OrderDto order)
    {
        var card = FindCard(order.Id);
        if (card is not null) card.UpdateOrder(order);
    }

    public void UpdateAllTimers()
    {
        foreach (OrderCardControl card in _flow.Controls.OfType<OrderCardControl>())
            card.UpdateTimer();
    }

    public void UpdateWorkload(int maxCards)
    {
        _pnlWorkload.Tag = maxCards;
        _pnlWorkload.Invalidate();
    }

    public OrderCardControl? FindCard(Guid orderId) =>
        _flow.Controls.OfType<OrderCardControl>()
             .FirstOrDefault(c => c.Order.Id == orderId);

    private OrderCardControl CreateCard(OrderDto order)
    {
        var card = new OrderCardControl(order, _theme);
        card.ConfirmClicked    += c => ConfirmClicked?.Invoke(c);
        card.CancelClicked     += c => CancelClicked?.Invoke(c);
        card.MarkReadyClicked  += c => MarkReadyClicked?.Invoke(c);
        card.MarkServedClicked += c => MarkServedClicked?.Invoke(c);
        card.GoToPayClicked    += c => GoToPayClicked?.Invoke(c);
        card.PrintComandaClicked  += c => PrintComandaClicked?.Invoke(c);
        card.AssignWaiterClicked  += c => AssignWaiterClicked?.Invoke(c);
        card.AddItemClicked    += c => AddItemClicked?.Invoke(c);
        card.DetailClicked     += c => DetailClicked?.Invoke(c);
        return card;
    }

    private void UpdateHeader()
    {
        _lblCount.Text = CardCount.ToString();
        Invalidate();
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────

    private void WireDragDrop()
    {
        DragEnter += OnDragEnter;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        DragDrop  += OnDragDrop;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(OrderDto)) == true)
        {
            e.Effect   = DragDropEffects.Move;
            _dragOver  = true;
            BackColor  = Color.FromArgb(30, _accentColor.R, _accentColor.G, _accentColor.B);
            _flow.BackColor = Color.Transparent;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(OrderDto)) == true)
            e.Effect = DragDropEffects.Move;
    }

    private void OnDragLeave(object? sender, EventArgs e)
    {
        _dragOver = false;
        BackColor = _bgColor;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        _dragOver = false;
        BackColor = _bgColor;
        if (e.Data?.GetData(typeof(OrderDto)) is OrderDto order)
            CardDropped?.Invoke(order, _status);
    }

    // ── Painting ─────────────────────────────────────────────────────────

    private void PaintHeader(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        // Bottom separator
        using var pen = new Pen(Color.FromArgb(40, _accentColor.R, _accentColor.G, _accentColor.B), 1f);
        g.DrawLine(pen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
    }

    private void PaintWorkload(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(30, _accentColor.R, _accentColor.G, _accentColor.B));
        var max  = _pnlWorkload.Tag is int m && m > 0 ? m : 1;
        var pct  = Math.Min(1f, (float)CardCount / max);
        if (pct > 0)
        {
            using var b = new SolidBrush(_accentColor);
            g.FillRectangle(b, 0, 0, (int)(_pnlWorkload.Width * pct), _pnlWorkload.Height);
        }
    }
}
