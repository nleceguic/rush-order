using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders;

public sealed class OrdersKanbanControl : UserControl
{
    private readonly ThemeManager _theme;
    private readonly Dictionary<OrderStatus, KanbanColumn> _columns = [];
    private readonly System.Windows.Forms.Timer _timerTick;
    private IReadOnlyList<OrderDto> _allOrders = [];

    // Events bubbled from cards
    public event Action<OrderCardControl>? ConfirmClicked;
    public event Action<OrderCardControl>? CancelClicked;
    public event Action<OrderCardControl>? MarkReadyClicked;
    public event Action<OrderCardControl>? MarkServedClicked;
    public event Action<OrderCardControl>? GoToPayClicked;
    public event Action<OrderCardControl>? PrintComandaClicked;
    public event Action<OrderCardControl>? AssignWaiterClicked;
    public event Action<OrderCardControl>? AddItemClicked;
    public event Action<OrderCardControl>? DetailClicked;
    public event Action<OrderDto, OrderStatus>? CardMoved;

    public OrdersKanbanControl(ThemeManager theme)
    {
        _theme         = theme;
        Dock           = DockStyle.Fill;
        BackColor      = theme.Colors.Background;
        DoubleBuffered = true;

        BuildColumns();

        _timerTick = new System.Windows.Forms.Timer { Interval = 1000 };
        _timerTick.Tick += (_, _) =>
        {
            foreach (var col in _columns.Values)
                col.UpdateAllTimers();
        };
        _timerTick.Start();
    }

    private void BuildColumns()
    {
        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 5,
            RowCount    = 1,
            BackColor   = Color.Transparent,
            Padding     = new Padding(4),
        };
        for (int i = 0; i < 5; i++)
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var statuses = new[] { OrderStatus.New, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served, OrderStatus.Paid };
        for (int i = 0; i < statuses.Length; i++)
        {
            var col = KanbanColumn.Create(statuses[i], _theme);
            WireColumn(col);
            _columns[statuses[i]] = col;
            table.Controls.Add(col, i, 0);
        }

        Controls.Add(table);
    }

    private void WireColumn(KanbanColumn col)
    {
        col.CardDropped       += OnCardDropped;
        col.ConfirmClicked    += c => ConfirmClicked?.Invoke(c);
        col.CancelClicked     += c => CancelClicked?.Invoke(c);
        col.MarkReadyClicked  += c => MarkReadyClicked?.Invoke(c);
        col.MarkServedClicked += c => MarkServedClicked?.Invoke(c);
        col.GoToPayClicked    += c => GoToPayClicked?.Invoke(c);
        col.PrintComandaClicked  += c => PrintComandaClicked?.Invoke(c);
        col.AssignWaiterClicked  += c => AssignWaiterClicked?.Invoke(c);
        col.AddItemClicked    += c => AddItemClicked?.Invoke(c);
        col.DetailClicked     += c => DetailClicked?.Invoke(c);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void LoadOrders(IReadOnlyList<OrderDto> orders)
    {
        if (InvokeRequired) { Invoke(() => LoadOrders(orders)); return; }

        _allOrders = orders;
        RebuildAllColumns(orders);
    }

    private void RebuildAllColumns(IReadOnlyList<OrderDto> orders)
    {
        SuspendLayout();
        foreach (var (status, col) in _columns)
        {
            var statusOrders = orders.Where(o => o.Status == status).ToList();
            col.ClearCards();
            col.AddCards(statusOrders);
        }
        ResumeLayout(true);

        UpdateWorkloadBars();
    }

    public void AddOrUpdateOrder(OrderDto order)
    {
        if (InvokeRequired) { Invoke(() => AddOrUpdateOrder(order)); return; }

        // Check if it exists in any column
        foreach (var (status, col) in _columns)
        {
            var existing = col.FindCard(order.Id);
            if (existing is not null)
            {
                if (status == order.Status)
                {
                    col.UpdateCard(order);
                }
                else
                {
                    col.RemoveCard(order.Id);
                    _columns[order.Status].AddCard(order);
                }
                UpdateWorkloadBars();
                return;
            }
        }

        // New order — add to correct column
        _columns[order.Status].AddCard(order);
        UpdateWorkloadBars();
    }

    public void RemoveOrder(Guid orderId)
    {
        if (InvokeRequired) { Invoke(() => RemoveOrder(orderId)); return; }
        foreach (var col in _columns.Values)
        {
            var card = col.FindCard(orderId);
            if (card is not null) { col.RemoveCard(orderId); break; }
        }
        UpdateWorkloadBars();
    }

    public void ApplyFilter(Func<OrderDto, bool> predicate)
    {
        if (InvokeRequired) { Invoke(() => ApplyFilter(predicate)); return; }
        var filtered = _allOrders.Where(predicate).ToList();
        RebuildAllColumns(filtered);
    }

    // ── Drag & Drop handler ───────────────────────────────────────────────

    private void OnCardDropped(OrderDto order, OrderStatus newStatus)
    {
        if (order.Status == newStatus) return;

        // Move card visually
        _columns[order.Status].RemoveCard(order.Id);
        var updated = order with { Status = newStatus };
        _columns[newStatus].AddCard(updated);
        UpdateWorkloadBars();

        CardMoved?.Invoke(order, newStatus);
    }

    private void UpdateWorkloadBars()
    {
        var max = _columns.Values.Max(c => c.CardCount);
        if (max == 0) max = 1;
        foreach (var col in _columns.Values)
            col.UpdateWorkload(max);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timerTick.Dispose();
        base.Dispose(disposing);
    }
}
