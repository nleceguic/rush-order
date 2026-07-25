using RushOrder.Desktop.Forms.Controls;
using RushOrder.Desktop.Helpers;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Kitchen;

public sealed class KitchenDisplayView : UserControl
{
    private readonly ThemeManager   _theme;
    private readonly KitchenService _kitchen;
    private readonly RealTimeService _realTime;
    private readonly ToastNotificationManager _toasts;

    // Layout panels
    private Panel             _pnlHeader   = null!;
    private Panel             _pnlAlert    = null!;
    private TableLayoutPanel  _pnlBoard    = null!;
    private Panel             _pnlFooter   = null!;
    private readonly Dictionary<OrderStatus, KitchenColumn> _columns = [];

    // Same status columns and colors Pedidos uses for New/Preparing/Ready — Cocina only ever
    // sees these three (the backend fetch already excludes Served/Paid/Cancelled).
    private static readonly (OrderStatus Status, string Label, Color Accent)[] ColumnDefs =
    [
        (OrderStatus.New,       "Nuevo",          Color.FromArgb(59, 130, 246)),
        (OrderStatus.Preparing, "En preparación", Color.FromArgb(245, 158, 11)),
        (OrderStatus.Ready,     "Listo",          Color.FromArgb(34, 197, 94)),
    ];

    // Header controls
    private PillButton[] _stationBtns = [];

    // Footer controls
    private Label   _lblCompleted  = null!;
    private Label   _lblAvgTime    = null!;
    private Label   _lblPending    = null!;
    private Label   _lblShiftStart = null!;

    // Alert banner controls
    private Label   _lblAlertText  = null!;
    private System.Windows.Forms.Timer _alertHideTimer = null!;
    private System.Windows.Forms.Timer _alertAnimTimer = null!;

    // State
    private KitchenStation _currentStation = KitchenStation.All;
    private readonly List<KitchenOrderDto> _allOrders = [];

    private readonly System.Windows.Forms.Timer _dimTimer;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _statsTimer;

    // Dark palette for KDS (always dark, regardless of theme)
    private static readonly Color KdsBg      = Color.FromArgb(15,  15,  15);
    private static readonly Color KdsSurface = Color.FromArgb(28,  28,  28);
    private static readonly Color KdsBorder  = Color.FromArgb(50,  50,  50);
    private static readonly Color KdsText    = Color.FromArgb(240, 240, 240);
    private static readonly Color KdsSubtext = Color.FromArgb(150, 150, 150);

    // ── Constructor ───────────────────────────────────────────────────────

    public KitchenDisplayView(
        ThemeManager theme, KitchenService kitchen, RealTimeService realTime,
        ToastNotificationManager toasts)
    {
        _theme   = theme;
        _kitchen = kitchen;
        _realTime = realTime;
        _toasts  = toasts;

        Dock           = DockStyle.Fill;
        BackColor      = KdsBg;
        DoubleBuffered = true;

        BuildLayout();
        WireRealTime();

        _dimTimer     = new System.Windows.Forms.Timer { Interval = 1000  };
        _dimTimer.Tick    += (_, _) => UpdateReadyDimming();
        _dimTimer.Start();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _refreshTimer.Tick += async (_, _) => await LoadOrdersAsync();
        _refreshTimer.Start();

        _statsTimer   = new System.Windows.Forms.Timer { Interval = 5_000  };
        _statsTimer.Tick += (_, _) => UpdateFooterStats();
        _statsTimer.Start();

        Load += async (_, _) => await LoadOrdersAsync();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        BuildAlertBanner();
        BuildHeader();
        BuildFooter();
        BuildBoard();

        // Dock resolves in reverse of Controls.Add order (see MainForm.InitializeComponent):
        // the LAST control added claims its space against the full original bounds first;
        // earlier ones divide up whatever is left. _pnlBoard (Fill) must be added FIRST so it
        // only claims the leftover space once header/alert/footer have carved out their bands
        // — otherwise it fills the whole panel and the header ends up painted on top of it.
        Controls.AddRange([_pnlBoard, _pnlFooter, _pnlAlert, _pnlHeader]);
    }

    private const int HeaderHeight = 50;

    private void BuildHeader()
    {
        _pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = HeaderHeight,
            BackColor = Color.FromArgb(20, 20, 20),
        };

        // Station filter buttons — pill, no border at rest; pink-red border on hover; full
        // pink-red fill when selected (IsActive marks the current station).
        var stations   = Enum.GetValues<KitchenStation>();
        _stationBtns   = new PillButton[stations.Length];
        for (int i = 0; i < stations.Length; i++)
        {
            var s   = stations[i];
            var btn = new PillButton(_theme, s.DisplayName(),
                idleBackColor: Color.FromArgb(40, 40, 40), idleBorderColor: Color.Transparent, idleTextColor: KdsText)
            {
                IsActive = s == KitchenStation.All,
            };
            var captured = s;
            btn.Click += (_, _) => SelectStation(captured);
            _stationBtns[i] = btn;
        }

        _pnlHeader.Controls.AddRange(_stationBtns);

        _pnlHeader.Resize += (_, _) => RelayoutHeader();
        RelayoutHeader();

        _pnlHeader.Paint += (_, e) =>
        {
            using var pen = new Pen(KdsBorder, 1f);
            e.Graphics.DrawLine(pen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
        };
    }

    private void RelayoutHeader()
    {
        var w = _pnlHeader.Width;

        // Station buttons centered
        int totalW = _stationBtns.Sum(b => b.Width + 4);
        int startX = (w - totalW) / 2;
        foreach (var btn in _stationBtns)
        {
            btn.Location = new Point(startX, (HeaderHeight - btn.Height) / 2);
            startX += btn.Width + 4;
        }
    }

    private void BuildAlertBanner()
    {
        _pnlAlert = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 0,   // hidden initially
            BackColor = Color.FromArgb(180, 0, 0),
        };

        _lblAlertText = new Label
        {
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = PoppinsFont.New("Poppins", 10f, FontStyle.Bold),
            ForeColor = Color.White,
        };
        _pnlAlert.Controls.Add(_lblAlertText);

        _alertHideTimer = new System.Windows.Forms.Timer { Interval = 6000 };
        _alertHideTimer.Tick += (_, _) =>
        {
            _alertHideTimer.Stop();
            CollapseAlert();
        };

        _alertAnimTimer = new System.Windows.Forms.Timer { Interval = 16 };
        int _alertTarget = 0;
        _alertAnimTimer.Tick += (_, _) =>
        {
            var diff = _alertTarget - _pnlAlert.Height;
            if (Math.Abs(diff) <= 1)
            {
                _pnlAlert.Height = _alertTarget;
                _alertAnimTimer.Stop();
                return;
            }
            _pnlAlert.Height += (int)Math.Max(1, diff * 0.35);
        };
    }

    private void BuildFooter()
    {
        _pnlFooter = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 34,
            BackColor = Color.FromArgb(20, 20, 20),
            Padding   = new Padding(12, 0, 12, 0),
        };
        _pnlFooter.Paint += (_, e) =>
        {
            using var pen = new Pen(KdsBorder, 1f);
            e.Graphics.DrawLine(pen, 0, 0, _pnlFooter.Width, 0);
        };

        _lblCompleted = MakeStatLabel("✓ Completados: 0");
        _lblAvgTime   = MakeStatLabel("⌛ Tiempo medio: —");
        _lblPending   = MakeStatLabel("📋 Pendientes: 0");
        _lblShiftStart = MakeStatLabel($"Turno desde: {DateTime.Now:HH:mm}");

        _pnlFooter.Controls.AddRange([_lblCompleted, _lblAvgTime, _lblPending, _lblShiftStart]);
        _pnlFooter.Resize += (_, _) => RelayoutFooter();
        RelayoutFooter();
    }

    private void RelayoutFooter()
    {
        var labels = new[] { _lblCompleted, _lblAvgTime, _lblPending, _lblShiftStart };
        int x = 12;
        foreach (var l in labels)
        {
            l.Location = new Point(x, (_pnlFooter.Height - l.Height) / 2);
            x += l.Width + 28;
        }
    }

    private void BuildBoard()
    {
        _pnlBoard = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = ColumnDefs.Length,
            RowCount    = 1,
            BackColor   = KdsBg,
        };
        for (int i = 0; i < ColumnDefs.Length; i++)
            _pnlBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / ColumnDefs.Length));
        _pnlBoard.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        for (int i = 0; i < ColumnDefs.Length; i++)
        {
            var (status, label, accent) = ColumnDefs[i];
            var col = new KitchenColumn(status, label, accent, KdsBg);
            _columns[status] = col;
            _pnlBoard.Controls.Add(col, i, 0);
        }
    }

    // ── Data ──────────────────────────────────────────────────────────────

    private async Task LoadOrdersAsync()
    {
        var orders = await _kitchen.GetActiveOrdersAsync();
        _allOrders.Clear();
        _allOrders.AddRange(orders);
        RebuildBoard();
        UpdateFooterStats();
    }

    private void RebuildBoard()
    {
        if (InvokeRequired) { Invoke(RebuildBoard); return; }

        foreach (var col in _columns.Values)
            col.ClearCards();

        var visible = FilteredOrders(_allOrders, _currentStation).ToList();
        foreach (var (status, col) in _columns)
        {
            foreach (var order in SortedForColumn(visible, status))
                col.AddCard(CreateCard(order, col));
        }

        RefreshColumnCounts();
    }

    private KitchenOrderCard CreateCard(KitchenOrderDto order, KitchenColumn column)
    {
        var cardW = ComputeCardWidth(column);
        var card  = new KitchenOrderCard(order, _theme, _currentStation)
        {
            Width  = cardW,
            // Zero horizontal margin — the card's left/right edges land exactly on the
            // column's edges, flush with the header/workload bar (see KitchenColumn).
            Margin = new Padding(0, 0, 0, 8),
        };
        card.MarkPreparingClicked += async c => await OnMarkPreparingAsync(c);
        card.MarkReadyClicked     += async c => await OnMarkReadyAsync(c);
        card.ToggleUrgentClicked  += async c => await OnToggleUrgentAsync(c);
        card.SwipeArchived        += OnSwipeArchive;

        if (order.IsReadyAndOld)
            card.Dimmed = true;

        return card;
    }

    // One card per row, flush with the column's own width — at the widths a 3-column KDS
    // board leaves per column, a 2-up split would just make cards uncomfortably narrow.
    private static int ComputeCardWidth(KitchenColumn column) => Math.Max(200, column.ContentWidth);

    private void RefreshColumnCounts()
    {
        var max = _columns.Count > 0 ? _columns.Values.Max(c => c.CardCount) : 0;
        if (max == 0) max = 1;
        foreach (var col in _columns.Values)
            col.RefreshCount(max);
    }

    // ── SignalR ───────────────────────────────────────────────────────────

    private void WireRealTime()
    {
        _realTime.OrderReceived += payload =>
        {
            var dto = _kitchen.AddFromSignalR(payload);
            if (dto is null) return Task.CompletedTask;

            Invoke(() =>
            {
                _allOrders.RemoveAll(o => o.OrderId == dto.OrderId);
                _allOrders.Add(dto);
                AddCardAnimated(dto);

                NotificationSound.PlayNewOrder();
            });
            return Task.CompletedTask;
        };

        _realTime.OrderStatusUpdated += (orderId, statusStr, _) =>
        {
            var dto = _kitchen.UpdateStatusFromSignalR(orderId, statusStr);
            if (dto is null) return Task.CompletedTask;

            Invoke(() =>
            {
                var idx = _allOrders.FindIndex(o => o.OrderId == dto.OrderId);
                if (idx >= 0) _allOrders[idx] = dto;
                UpdateCardInGrid(dto);
            });
            return Task.CompletedTask;
        };

        _realTime.KitchenAlert += (message, severity) =>
        {
            Invoke(() => ShowAlert(message, severity));
            return Task.CompletedTask;
        };
    }

    private void AddCardAnimated(KitchenOrderDto order)
    {
        if (!_columns.TryGetValue(order.Status, out var col)) return;
        if (!PassesStationFilter(order, _currentStation)) return;

        var card = CreateCard(order, col);
        var visible  = FilteredOrders(_allOrders, _currentStation);
        var sortedIds = SortedForColumn(visible, order.Status).Select(o => o.OrderId).ToList();
        int insertIdx = sortedIds.IndexOf(order.OrderId);
        if (insertIdx < 0) insertIdx = 0;
        col.InsertCard(card, insertIdx);
        RefreshColumnCounts();
    }

    private void UpdateCardInGrid(KitchenOrderDto updated)
    {
        // Still in the same column (status unchanged) — update it in place.
        if (_columns.TryGetValue(updated.Status, out var targetCol))
        {
            var existing = targetCol.FindCard(updated.OrderId);
            if (existing is not null)
            {
                existing.UpdateOrder(updated);
                existing.Dimmed = updated.IsReadyAndOld;
                RefreshColumnCounts();
                return;
            }
        }

        // Status changed columns (or this is the first time we've seen it) — remove it from
        // wherever it currently lives and re-add it in the right spot in its new column.
        foreach (var col in _columns.Values)
        {
            var existing = col.FindCard(updated.OrderId);
            if (existing is not null) { col.RemoveCard(existing); break; }
        }

        AddCardAnimated(updated);
    }

    // ── Card action handlers ──────────────────────────────────────────────

    private async Task OnMarkPreparingAsync(KitchenOrderCard card)
    {
        await _kitchen.MarkAsPreparingAsync(card.Order.OrderId);
        var updated = card.Order with { Status = OrderStatus.Preparing };
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;
        RebuildBoard();
    }

    private async Task OnMarkReadyAsync(KitchenOrderCard card)
    {
        await _kitchen.MarkAsReadyAsync(card.Order.OrderId);
        var updated = card.Order with { Status = OrderStatus.Ready };
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;
        RebuildBoard();
        UpdateFooterStats();
    }

    private async Task OnToggleUrgentAsync(KitchenOrderCard card)
    {
        var newUrgent = !card.Order.IsUrgent;
        await _kitchen.SetUrgentAsync(card.Order.OrderId, newUrgent);
        var updated = card.Order with { IsUrgent = newUrgent };
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;

        if (newUrgent) NotificationSound.PlayUrgent();

        // Resort
        RebuildBoard();
    }

    private void OnSwipeArchive(KitchenOrderCard card)
    {
        // Animate card width to 0, then remove
        var t = new System.Windows.Forms.Timer { Interval = 16 };
        t.Tick += (_, _) =>
        {
            card.Width = Math.Max(0, card.Width - 30);
            if (card.Width == 0)
            {
                t.Stop(); t.Dispose();
                _allOrders.RemoveAll(o => o.OrderId == card.Order.OrderId);
                if (_columns.TryGetValue(card.Order.Status, out var col))
                    col.RemoveCard(card);
                RefreshColumnCounts();
            }
        };
        t.Start();
    }

    // ── Station filtering ─────────────────────────────────────────────────

    private void SelectStation(KitchenStation station)
    {
        _currentStation = station;

        // Update button styles
        var stations = Enum.GetValues<KitchenStation>().ToArray();
        for (int i = 0; i < stations.Length && i < _stationBtns.Length; i++)
            _stationBtns[i].IsActive = stations[i] == station;

        // Rebuild — cards are recreated with the new station filter baked in (each
        // KitchenOrderCard applies it in its own constructor), so no separate per-card update.
        RebuildBoard();
    }

    // ── Alert banner ──────────────────────────────────────────────────────

    private void ShowAlert(string message, string severity)
    {
        _lblAlertText.Text      = $"⚠  {message.ToUpper()}";
        _pnlAlert.BackColor     = severity == "Critical"
            ? Color.FromArgb(180, 0, 0)
            : Color.FromArgb(160, 100, 0);

        NotificationSound.PlayAlert();

        ExpandAlert(36);
        _alertHideTimer.Stop();
        _alertHideTimer.Start();
    }

    private void ExpandAlert(int targetH)
    {
        int target = targetH;
        _alertAnimTimer.Stop();
        var tickHandler = (EventHandler?)null;
        tickHandler = (_, _) =>
        {
            var diff = target - _pnlAlert.Height;
            if (Math.Abs(diff) <= 1)
            {
                _pnlAlert.Height = target;
                _alertAnimTimer.Tick -= tickHandler;
                _alertAnimTimer.Stop();
                return;
            }
            _pnlAlert.Height += (int)Math.Max(1, diff * 0.35);
        };
        _alertAnimTimer.Tick += tickHandler;
        _alertAnimTimer.Start();
    }

    private void CollapseAlert() => ExpandAlert(0);

    // ── Helpers ───────────────────────────────────────────────────────────

    private void UpdateReadyDimming()
    {
        // Dim Ready cards after 3 min
        foreach (var col in _columns.Values)
            foreach (var card in col.Cards)
                if (card.IsReadyAndOld) card.Dimmed = true;
    }

    private void UpdateFooterStats()
    {
        if (InvokeRequired) { Invoke(UpdateFooterStats); return; }
        var stats = _kitchen.GetShiftStats();
        _lblCompleted.Text  = $"✓ Completados: {stats.CompletedOrders}";
        _lblAvgTime.Text    = stats.AvgTime == TimeSpan.Zero
            ? "⌛ Tiempo medio: —"
            : $"⌛ Tiempo medio: {stats.AvgTime.TotalMinutes:N1} min";
        _lblPending.Text    = $"📋 Pendientes: {stats.PendingOrders}";
        RelayoutFooter();
    }

    private static bool PassesStationFilter(KitchenOrderDto order, KitchenStation station) =>
        station == KitchenStation.All || order.Items.Any(i => i.Station == station);

    private static IEnumerable<KitchenOrderDto> FilteredOrders(
        IEnumerable<KitchenOrderDto> orders, KitchenStation station) =>
        orders.Where(o => PassesStationFilter(o, station));

    // Status is now the column itself (like Pedidos' Kanban columns), so within a column the
    // sort is just urgent-first then newest-first — matching the
    // OrderByDescending(CreatedAt) Pedidos uses per column.
    private static IEnumerable<KitchenOrderDto> SortedForColumn(
        IEnumerable<KitchenOrderDto> orders, OrderStatus status) =>
        orders.Where(o => o.Status == status)
              .OrderByDescending(o => o.IsUrgent)
              .ThenByDescending(o => o.PlacedAt);

    private static Label MakeStatLabel(string text) => new()
    {
        Text      = text,
        Font      = PoppinsFont.New("Poppins", 8f),
        ForeColor = KdsSubtext,
        AutoSize  = true,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dimTimer.Dispose();
            _refreshTimer.Dispose();
            _statsTimer.Dispose();
            _alertHideTimer.Dispose();
            _alertAnimTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
