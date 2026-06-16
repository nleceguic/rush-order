using RushOrder.Desktop.Core.Hubs;
using RushOrder.Desktop.Helpers;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.FloorPlan;
using RushOrder.Desktop.Views.Orders.Dialogs;

namespace RushOrder.Desktop.Views.Orders;

public sealed class OrdersView : UserControl
{
    private readonly ThemeManager             _theme;
    private readonly OrderService             _orders;
    private readonly ProductSearchService     _products;
    private readonly TableService             _tables;
    private readonly RealTimeService          _realTime;
    private readonly ToastNotificationManager _toasts;
    private readonly AppState                 _state;

    private OrdersKanbanControl _kanban    = null!;
    private ComboBox  _cmbWaiter  = null!;
    private ComboBox  _cmbTable   = null!;
    private TextBox   _txtSearch  = null!;
    private CheckBox  _chkMine    = null!;
    private Button    _btnFab     = null!;

    private string? _filterWaiter;
    private string? _filterTable;
    private string  _filterSearch = "";
    private bool    _filterMine;

    private IReadOnlyList<OrderDto> _allOrders = [];

    public OrdersView(
        ThemeManager theme, OrderService orders, ProductSearchService products,
        TableService tables, RealTimeService realTime,
        ToastNotificationManager toasts, AppState state)
    {
        _theme    = theme;
        _orders   = orders;
        _products = products;
        _tables   = tables;
        _realTime = realTime;
        _toasts   = toasts;
        _state    = state;

        Dock  = DockStyle.Fill;
        BackColor = theme.Colors.Background;
        DoubleBuffered = true;

        BuildLayout();
        WireRealTime();
        Load += async (_, _) => await LoadOrdersAsync();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // ── Filter toolbar ────────────────────────────────────────────────
        var pnlToolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(8, 8, 8, 8),
        };
        pnlToolbar.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
        };

        _cmbWaiter = MakeCombo(120, "Todos los camareros");
        _cmbTable  = MakeCombo(100, "Todas las mesas");
        _txtSearch = new TextBox
        {
            Width           = 160,
            Font            = _theme.Fonts.Regular,
            PlaceholderText = "Buscar #pedido…",
            BorderStyle     = BorderStyle.FixedSingle,
        };
        _txtSearch.TextChanged += (_, _) => { _filterSearch = _txtSearch.Text; ApplyFilters(); };

        _chkMine = new CheckBox
        {
            Text      = "Solo mis mesas",
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextPrimary,
            AutoSize  = true,
        };
        _chkMine.CheckedChanged += (_, _) => { _filterMine = _chkMine.Checked; ApplyFilters(); };

        var btnRefresh = MakeToolButton("⟳ Actualizar");
        btnRefresh.Click += async (_, _) => await LoadOrdersAsync();

        int x = 0;
        foreach (Control c in new Control[] { _cmbWaiter, _cmbTable, _txtSearch, _chkMine, btnRefresh })
        {
            c.Location = new Point(x, 8);
            if (c is Button)   { c.Size = new Size(c.Width, 30); }
            if (c is CheckBox) { c.Location = new Point(x, 14); }
            pnlToolbar.Controls.Add(c);
            x += c.Width + 8;
        }

        // ── Kanban ────────────────────────────────────────────────────────
        _kanban = new OrdersKanbanControl(_theme);
        _kanban.ConfirmClicked    += async c => await OnConfirmAsync(c);
        _kanban.CancelClicked     += async c => await OnCancelAsync(c);
        _kanban.MarkReadyClicked  += async c => await OnUpdateStatusAsync(c, OrderStatus.Ready);
        _kanban.MarkServedClicked += async c => await OnUpdateStatusAsync(c, OrderStatus.Served);
        _kanban.GoToPayClicked    += c => OnGoToPay(c);
        _kanban.PrintComandaClicked  += c => OnPrintComanda(c);
        _kanban.AssignWaiterClicked  += async c => await OnAssignWaiterAsync(c);
        _kanban.AddItemClicked    += c => OnAddItem(c);
        _kanban.DetailClicked     += c => OnShowDetail(c);
        _kanban.CardMoved         += async (order, status) => await OnCardMovedAsync(order, status);

        // ── FAB ───────────────────────────────────────────────────────────
        _btnFab = BuildFab();

        Controls.AddRange([pnlToolbar, _kanban, _btnFab]);
        Resize += (_, _) => PositionFab();
        PositionFab();
    }

    // ── Data ──────────────────────────────────────────────────────────────

    private async Task LoadOrdersAsync()
    {
        _allOrders = await _orders.GetOrdersAsync();

        var waiters = _allOrders.Select(o => o.WaiterName).Where(w => w is not null).Distinct().ToList();
        var tables  = _allOrders.Select(o => o.TableName).Distinct().ToList();

        PopulateCombo(_cmbWaiter, waiters!, "Todos los camareros");
        PopulateCombo(_cmbTable,  tables,   "Todas las mesas");

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allOrders.Where(order =>
        {
            if (_filterWaiter is not null && order.WaiterName != _filterWaiter) return false;
            if (_filterTable  is not null && order.TableName  != _filterTable)  return false;
            if (!string.IsNullOrWhiteSpace(_filterSearch) &&
                !order.OrderNumber.Contains(_filterSearch, StringComparison.OrdinalIgnoreCase)) return false;
            if (_filterMine && order.WaiterName != _state.CurrentUser?.FullName) return false;
            return true;
        }).ToList();
        _kanban.LoadOrders(filtered);
    }

    // ── SignalR ───────────────────────────────────────────────────────────

    private void WireRealTime()
    {
        _realTime.OrderReceived += async payload =>
        {
            var order = PayloadToDto(payload);
            _kanban.AddOrUpdateOrder(order);
            NotifyNewOrder(order);
            await Task.CompletedTask;
        };

        _realTime.OrderStatusUpdated += async (orderId, statusStr, _) =>
        {
            if (!Guid.TryParse(orderId, out var id)) return;
            if (!Enum.TryParse<OrderStatus>(statusStr, out var status)) return;
            var existing = _allOrders.FirstOrDefault(o => o.Id == id);
            if (existing is not null)
            {
                var updated = existing with { Status = status };
                _kanban.AddOrUpdateOrder(updated);
            }
            await Task.CompletedTask;
        };
    }

    private void NotifyNewOrder(OrderDto order)
    {
        if (InvokeRequired) { Invoke(() => NotifyNewOrder(order)); return; }

        NotificationSound.PlayNewOrder();
        var parentForm = FindForm();
        if (parentForm is not null && !parentForm.Focused)
            TaskbarFlash.Flash(parentForm);

        _toasts.Show($"Nuevo pedido #{order.OrderNumber} — {order.TableName}", ToastType.Info, 5000);
    }

    // ── Card actions ──────────────────────────────────────────────────────

    private async Task OnConfirmAsync(OrderCardControl card)
    {
        await _orders.UpdateStatusAsync(card.Order.Id, OrderStatus.Preparing);
        var updated = card.Order with { Status = OrderStatus.Preparing };
        _kanban.AddOrUpdateOrder(updated);
        _toasts.Show($"Pedido #{card.Order.OrderNumber} en preparación", ToastType.Success);
    }

    private async Task OnCancelAsync(OrderCardControl card)
    {
        if (MessageBox.Show($"¿Cancelar pedido #{card.Order.OrderNumber}?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        await _orders.CancelOrderAsync(card.Order.Id);
        _kanban.RemoveOrder(card.Order.Id);
        _toasts.Show($"Pedido #{card.Order.OrderNumber} cancelado", ToastType.Warning);
    }

    private async Task OnUpdateStatusAsync(OrderCardControl card, OrderStatus status)
    {
        await _orders.UpdateStatusAsync(card.Order.Id, status);
        var updated = card.Order with { Status = status };
        _kanban.AddOrUpdateOrder(updated);
    }

    private void OnGoToPay(OrderCardControl card)
    {
        using var dlg = new PaymentDialog(card.Order, _theme);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result is null) return;

        _ = Task.Run(async () =>
        {
            await _orders.ProcessPaymentAsync(dlg.Result);
            var updated = card.Order with { Status = OrderStatus.Paid };
            _kanban.AddOrUpdateOrder(updated);
            _toasts.Show($"Pedido #{card.Order.OrderNumber} cobrado ✓", ToastType.Success);
        });
    }

    private void OnPrintComanda(OrderCardControl card)
    {
        var bytes = EscPosBuilder.BuildComanda(card.Order);
        var ok    = RawPrinterHelper.SendToDefaultPrinter(bytes, $"Comanda #{card.Order.OrderNumber}");
        if (!ok)
            _toasts.Show("Sin impresora térmica disponible", ToastType.Warning);
        else
            _toasts.Show("Comanda enviada a impresora", ToastType.Success);
    }

    private async Task OnAssignWaiterAsync(OrderCardControl card)
    {
        using var dlg = new WaiterInputDialog(_theme, card.Order.WaiterName);
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.WaiterName)) return;

        await _orders.AssignWaiterAsync(card.Order.Id, dlg.WaiterName);
        var updated = card.Order with { WaiterName = dlg.WaiterName };
        _kanban.AddOrUpdateOrder(updated);
    }

    private void OnAddItem(OrderCardControl card)
    {
        _toasts.Show("Añadir artículo: función próximamente", ToastType.Info, 2000);
    }

    private void OnShowDetail(OrderCardControl card)
    {
        using var dlg = new OrderDetailDialog(card.Order, _theme);
        dlg.ShowDialog(this);
    }

    private async Task OnCardMovedAsync(OrderDto order, OrderStatus newStatus)
    {
        await _orders.UpdateStatusAsync(order.Id, newStatus);
        _toasts.Show($"#{order.OrderNumber} → {StatusLabel(newStatus)}", ToastType.Info, 2000);
    }

    // ── FAB ───────────────────────────────────────────────────────────────

    private Button BuildFab()
    {
        var btn = new Button
        {
            Size      = new Size(52, 52),
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Text      = "+",
            Font      = new Font("Segoe UI", 22f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;

        // Circular shape
        btn.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SetQuality();
            using var b = new SolidBrush(_theme.Colors.Primary);
            g.FillEllipse(b, 0, 0, btn.Width - 1, btn.Height - 1);
            using var tb = new SolidBrush(Color.White);
            using var f  = new Font("Segoe UI", 22f, FontStyle.Bold);
            g.DrawString("+", f, tb, new RectangleF(0, -3, btn.Width, btn.Height),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        };
        btn.Region = new System.Drawing.Region(new System.Drawing.Drawing2D.GraphicsPath());
        var circle = new System.Drawing.Drawing2D.GraphicsPath();
        circle.AddEllipse(0, 0, 52, 52);
        btn.Region = new System.Drawing.Region(circle);

        btn.Click    += OnNewOrderClicked;
        btn.MouseEnter += (_, _) => btn.BackColor = _theme.Colors.PrimaryHover;
        btn.MouseLeave += (_, _) => btn.BackColor = _theme.Colors.Primary;

        return btn;
    }

    private void PositionFab() =>
        _btnFab.Location = new Point(Width - 72, Height - 72);

    private async void OnNewOrderClicked(object? sender, EventArgs e)
    {
        using var dlg = new NewOrderDialog(_theme, _products, _tables, _state);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result is null) return;

        _btnFab.Enabled = false;
        try
        {
            var order = await _orders.CreateOrderAsync(dlg.Result);
            if (order is not null)
            {
                _kanban.AddOrUpdateOrder(order);
                _toasts.Show($"Pedido #{order.OrderNumber} creado", ToastType.Success);
            }
        }
        finally { _btnFab.Enabled = true; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private ComboBox MakeCombo(int width, string placeholder)
    {
        var cb = new ComboBox
        {
            Width         = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = _theme.Fonts.Small,
            BackColor     = _theme.Colors.Surface,
            ForeColor     = _theme.Colors.TextPrimary,
        };
        cb.Items.Add(placeholder);
        cb.SelectedIndex = 0;
        cb.SelectedIndexChanged += (_, _) =>
        {
            var val = cb.SelectedIndex == 0 ? null : cb.SelectedItem?.ToString();
            if (cb == _cmbWaiter) { _filterWaiter = val; ApplyFilters(); }
            else                  { _filterTable  = val; ApplyFilters(); }
        };
        return cb;
    }

    private Button MakeToolButton(string text) => new()
    {
        Text      = text,
        Width     = text.Length * 7 + 16,
        Height    = 30,
        FlatStyle = FlatStyle.Flat,
        BackColor = _theme.Colors.Surface,
        ForeColor = _theme.Colors.TextPrimary,
        Font      = _theme.Fonts.Small,
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderColor = _theme.Colors.Border, BorderSize = 1 },
    };

    private static void PopulateCombo(ComboBox cb, IEnumerable<string> items, string placeholder)
    {
        var selected = cb.SelectedIndex > 0 ? cb.SelectedItem?.ToString() : null;
        cb.BeginUpdate();
        cb.Items.Clear();
        cb.Items.Add(placeholder);
        foreach (var i in items) cb.Items.Add(i);
        cb.SelectedIndex = selected is not null
            ? Math.Max(0, cb.Items.IndexOf(selected))
            : 0;
        cb.EndUpdate();
    }

    private static string StatusLabel(OrderStatus s) => s switch
    {
        OrderStatus.New       => "Nuevo",
        OrderStatus.Preparing => "En preparación",
        OrderStatus.Ready     => "Listo",
        OrderStatus.Served    => "Servido",
        OrderStatus.Paid      => "Cobrado",
        _                     => s.ToString(),
    };

    private static OrderDto PayloadToDto(OrderReceivedPayload p) => new(
        p.OrderId, p.OrderNumber, p.RestaurantId, p.TableId, p.TableName, 2,
        OrderStatus.New, p.Source, null,
        p.Items.Select(i => new OrderItemDto(Guid.NewGuid(), i.ProductId, i.Name,
            i.Quantity, 0, 0, i.Notes, [], i.Modifiers)).ToList(),
        p.Total, Math.Round(p.Total * 0.1m, 2), p.Total, p.Notes, p.PlacedAt,
        null, null, null, null);
}
