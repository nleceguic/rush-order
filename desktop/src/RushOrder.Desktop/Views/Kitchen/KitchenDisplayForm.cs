using RushOrder.Desktop.Helpers;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Kitchen;

public sealed class KitchenDisplayForm : Form
{
    private readonly ThemeManager   _theme;
    private readonly KitchenService _kitchen;
    private readonly RealTimeService _realTime;
    private readonly AppState       _state;
    private readonly ToastNotificationManager _toasts;

    // Layout panels
    private Panel           _pnlHeader   = null!;
    private Panel           _pnlAlert    = null!;
    private FlowLayoutPanel _pnlCards    = null!;
    private Panel           _pnlFooter   = null!;

    // Header controls
    private Label   _lblClock      = null!;
    private Label   _lblRestaurant = null!;
    private Label   _lblConnect    = null!;
    private Button  _btnMute       = null!;
    private Button[] _stationBtns  = [];

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
    private bool           _isMuted        = false;
    private readonly List<KitchenOrderDto> _allOrders = [];

    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _statsTimer;

    // Dark palette for KDS (always dark, regardless of theme)
    private static readonly Color KdsBg      = Color.FromArgb(15,  15,  15);
    private static readonly Color KdsSurface = Color.FromArgb(28,  28,  28);
    private static readonly Color KdsBorder  = Color.FromArgb(50,  50,  50);
    private static readonly Color KdsText    = Color.FromArgb(240, 240, 240);
    private static readonly Color KdsSubtext = Color.FromArgb(150, 150, 150);

    // ── Constructor ───────────────────────────────────────────────────────

    public KitchenDisplayForm(
        ThemeManager theme, KitchenService kitchen, RealTimeService realTime,
        AppState state, ToastNotificationManager toasts)
    {
        _theme   = theme;
        _kitchen = kitchen;
        _realTime = realTime;
        _state   = state;
        _toasts  = toasts;

        Text            = "RushOrder — Kitchen Display System";
        Size            = new Size(1920, 1080);
        MinimumSize     = new Size(1024, 600);
        BackColor       = KdsBg;
        DoubleBuffered  = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState     = FormWindowState.Maximized;
        ShowInTaskbar   = true;
        KeyPreview      = true;

        BuildLayout();
        WireRealTime();
        WireKeys();

        _clockTimer   = new System.Windows.Forms.Timer { Interval = 1000  };
        _clockTimer.Tick  += (_, _) => UpdateClock();
        _clockTimer.Start();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _refreshTimer.Tick += async (_, _) => await LoadOrdersAsync();
        _refreshTimer.Start();

        _statsTimer   = new System.Windows.Forms.Timer { Interval = 5_000  };
        _statsTimer.Tick += (_, _) => UpdateFooterStats();
        _statsTimer.Start();

        Load += async (_, _) => await LoadOrdersAsync();
    }

    // ── Multi-screen launcher ─────────────────────────────────────────────

    public static void Launch(KitchenDisplayForm form, Form? owner = null)
    {
        if (Screen.AllScreens.Length > 1)
        {
            var secondary = Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.PrimaryScreen!;
            form.StartPosition = FormStartPosition.Manual;
            form.Location      = secondary.WorkingArea.Location;
            form.WindowState   = FormWindowState.Maximized;
        }
        else
        {
            form.StartPosition = FormStartPosition.CenterScreen;
        }

        if (owner is not null) form.Show(owner);
        else                   form.Show();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        BuildAlertBanner();
        BuildHeader();
        BuildFooter();
        BuildCardGrid();

        Controls.AddRange([_pnlHeader, _pnlAlert, _pnlCards, _pnlFooter]);
    }

    private void BuildHeader()
    {
        _pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 80,
            BackColor = Color.FromArgb(20, 20, 20),
        };

        // Restaurant name
        _lblRestaurant = new Label
        {
            Text      = _state.CurrentRestaurant?.Name ?? "Kitchen Display",
            Font      = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = KdsText,
            Location  = new Point(20, 20),
            AutoSize  = true,
        };

        // Clock
        _lblClock = new Label
        {
            Text      = DateTime.Now.ToString("HH:mm:ss"),
            Font      = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 200, 100),
            AutoSize  = true,
        };

        // Connection dot
        _lblConnect = new Label
        {
            Text      = "● Online",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(34, 197, 94),
            AutoSize  = true,
        };

        // Mute toggle
        _btnMute = MakeHeaderButton("🔔 Sonido ON", Color.FromArgb(40, 40, 40));
        _btnMute.Click += (_, _) =>
        {
            _isMuted = !_isMuted;
            _btnMute.Text      = _isMuted ? "🔕 Silencio" : "🔔 Sonido ON";
            _btnMute.BackColor = _isMuted ? Color.FromArgb(80, 0, 0) : Color.FromArgb(40, 40, 40);
        };

        // Station filter buttons
        var stations   = Enum.GetValues<KitchenStation>();
        _stationBtns   = new Button[stations.Length];
        for (int i = 0; i < stations.Length; i++)
        {
            var s   = stations[i];
            var btn = MakeStationButton(s.DisplayName(), s == KitchenStation.All);
            var captured = s;
            btn.Click += (_, _) => SelectStation(captured);
            _stationBtns[i] = btn;
        }

        _pnlHeader.Controls.AddRange(
            new Control[] { _lblRestaurant, _lblClock, _lblConnect, _btnMute }
            .Concat(_stationBtns).ToArray());

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

        // Right side: clock + connect + mute
        _lblClock.Location   = new Point(w - _lblClock.Width   - 16, (80 - _lblClock.Height)   / 2);
        _btnMute.Location    = new Point(w - _lblClock.Width - 140, 24);
        _lblConnect.Location = new Point(w - _lblClock.Width - 140, 54);

        // Station buttons in the center
        int totalW = _stationBtns.Sum(b => b.Width + 6);
        int startX = (w - totalW) / 2;
        foreach (var btn in _stationBtns)
        {
            btn.Location = new Point(startX, 20);
            startX += btn.Width + 6;
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
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
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
            Height    = 48,
            BackColor = Color.FromArgb(20, 20, 20),
            Padding   = new Padding(16, 0, 16, 0),
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
        int x = 16;
        foreach (var l in labels)
        {
            l.Location = new Point(x, (_pnlFooter.Height - l.Height) / 2);
            x += l.Width + 40;
        }
    }

    private void BuildCardGrid()
    {
        _pnlCards = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = true,
            BackColor     = KdsBg,
            Padding       = new Padding(8),
        };
    }

    // ── Data ──────────────────────────────────────────────────────────────

    private async Task LoadOrdersAsync()
    {
        var orders = await _kitchen.GetActiveOrdersAsync();
        _allOrders.Clear();
        _allOrders.AddRange(orders);
        RebuildCardGrid();
        UpdateFooterStats();
    }

    private void RebuildCardGrid()
    {
        if (InvokeRequired) { Invoke(RebuildCardGrid); return; }

        // Dispose old cards
        foreach (KitchenOrderCard c in _pnlCards.Controls.OfType<KitchenOrderCard>().ToList())
        {
            _pnlCards.Controls.Remove(c);
            c.Dispose();
        }

        var sorted = SortedOrders(_allOrders, _currentStation);

        _pnlCards.SuspendLayout();
        foreach (var order in sorted)
            _pnlCards.Controls.Add(CreateCard(order));
        _pnlCards.ResumeLayout();
    }

    private KitchenOrderCard CreateCard(KitchenOrderDto order)
    {
        var cardW = ComputeCardWidth();
        var card  = new KitchenOrderCard(order, _theme, _currentStation)
        {
            Width  = cardW,
            Margin = new Padding(8),
        };
        card.MarkPreparingClicked += async c => await OnMarkPreparingAsync(c);
        card.MarkReadyClicked     += async c => await OnMarkReadyAsync(c);
        card.ToggleUrgentClicked  += async c => await OnToggleUrgentAsync(c);
        card.SwipeArchived        += OnSwipeArchive;

        if (order.IsReadyAndOld)
            card.Dimmed = true;

        return card;
    }

    private int ComputeCardWidth()
    {
        var cols = _pnlCards.Width switch
        {
            > 1600 => 4,
            > 1200 => 3,
            > 800  => 2,
            _      => 1,
        };
        return (_pnlCards.Width - 16 * (cols + 1)) / cols;
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

                if (!_isMuted)
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

        _state.OnlineStatusChanged += online =>
        {
            if (InvokeRequired) { Invoke(() => UpdateConnectionLabel(online)); return; }
            UpdateConnectionLabel(online);
        };
    }

    private void AddCardAnimated(KitchenOrderDto order)
    {
        var card = CreateCard(order);
        var sorted = SortedOrders(_allOrders, _currentStation);
        int insertIdx = sorted.ToList().FindIndex(o => o.OrderId == order.OrderId);
        if (insertIdx < 0) insertIdx = 0;
        _pnlCards.Controls.Add(card);
        _pnlCards.Controls.SetChildIndex(card, insertIdx);
    }

    private void UpdateCardInGrid(KitchenOrderDto updated)
    {
        var card = _pnlCards.Controls.OfType<KitchenOrderCard>()
            .FirstOrDefault(c => c.Order.OrderId == updated.OrderId);

        if (card is null)
        {
            AddCardAnimated(updated);
            return;
        }

        card.UpdateOrder(updated);

        // Move to end if Ready
        if (updated.Status == OrderStatus.Ready)
        {
            _pnlCards.Controls.SetChildIndex(card, _pnlCards.Controls.Count - 1);
            card.Dimmed = updated.IsReadyAndOld;
        }
    }

    // ── Card action handlers ──────────────────────────────────────────────

    private async Task OnMarkPreparingAsync(KitchenOrderCard card)
    {
        await _kitchen.MarkAsPreparingAsync(card.Order.OrderId);
        var updated = card.Order with { Status = OrderStatus.Preparing };
        card.UpdateOrder(updated);
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;
    }

    private async Task OnMarkReadyAsync(KitchenOrderCard card)
    {
        await _kitchen.MarkAsReadyAsync(card.Order.OrderId);
        var updated = card.Order with { Status = OrderStatus.Ready };
        card.UpdateOrder(updated);
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;
        // Move to end of grid
        _pnlCards.Controls.SetChildIndex(card, _pnlCards.Controls.Count - 1);
        UpdateFooterStats();
    }

    private async Task OnToggleUrgentAsync(KitchenOrderCard card)
    {
        var newUrgent = !card.Order.IsUrgent;
        await _kitchen.SetUrgentAsync(card.Order.OrderId, newUrgent);
        var updated = card.Order with { IsUrgent = newUrgent };
        card.UpdateOrder(updated);
        var idx = _allOrders.FindIndex(o => o.OrderId == card.Order.OrderId);
        if (idx >= 0) _allOrders[idx] = updated;

        if (newUrgent && !_isMuted) NotificationSound.PlayUrgent();

        // Resort
        RebuildCardGrid();
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
                _pnlCards.Controls.Remove(card);
                card.Dispose();
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
        {
            var active = stations[i] == station;
            _stationBtns[i].BackColor  = active ? _theme.Colors.Primary : Color.FromArgb(40, 40, 40);
            _stationBtns[i].ForeColor  = Color.White;
            _stationBtns[i].FlatAppearance.BorderColor = active ? _theme.Colors.Primary : KdsBorder;
        }

        // Update all visible cards
        foreach (KitchenOrderCard card in _pnlCards.Controls.OfType<KitchenOrderCard>())
            card.ApplyStationFilter(station);

        // Reorder cards
        RebuildCardGrid();
    }

    // ── Alert banner ──────────────────────────────────────────────────────

    private void ShowAlert(string message, string severity)
    {
        _lblAlertText.Text      = $"⚠  {message.ToUpper()}";
        _pnlAlert.BackColor     = severity == "Critical"
            ? Color.FromArgb(180, 0, 0)
            : Color.FromArgb(160, 100, 0);

        if (!_isMuted) NotificationSound.PlayAlert();

        ExpandAlert(52);
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

    // ── Keyboard shortcuts ────────────────────────────────────────────────

    private void WireKeys()
    {
        KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.F11:
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    break;
                case Keys.M:
                    _btnMute.PerformClick();
                    break;
                case Keys.Escape when WindowState != FormWindowState.Maximized:
                    Close();
                    break;
            }
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void UpdateClock()
    {
        _lblClock.Text     = DateTime.Now.ToString("HH:mm:ss");
        _lblClock.Location = new Point(_pnlHeader.Width - _lblClock.Width - 16, (_pnlHeader.Height - _lblClock.Height) / 2);

        // Dim Ready cards after 3 min
        foreach (KitchenOrderCard card in _pnlCards.Controls.OfType<KitchenOrderCard>())
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

    private void UpdateConnectionLabel(bool online)
    {
        _lblConnect.Text      = online ? "● Online"  : "● Offline";
        _lblConnect.ForeColor = online ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
    }

    private static IEnumerable<KitchenOrderDto> SortedOrders(
        IEnumerable<KitchenOrderDto> orders, KitchenStation station)
    {
        var filtered = station == KitchenStation.All
            ? orders
            : orders.Where(o => o.Items.Any(i => i.Station == station));

        return filtered
            .OrderBy(o => o.Status == OrderStatus.Ready ? 1 : 0)   // Ready last
            .ThenByDescending(o => o.IsUrgent)                      // Urgent first
            .ThenBy(o => o.PlacedAt);                               // FIFO
    }

    private static Button MakeHeaderButton(string text, Color bg) => new()
    {
        Text      = text,
        AutoSize  = true,
        Padding   = new Padding(8, 4, 8, 4),
        BackColor = bg,
        ForeColor = KdsText,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 9f),
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderColor = KdsBorder, BorderSize = 1 },
    };

    private static Button MakeStationButton(string text, bool active) => new()
    {
        Text      = text,
        AutoSize  = true,
        Padding   = new Padding(12, 6, 12, 6),
        BackColor = active ? Color.FromArgb(230, 57, 70) : Color.FromArgb(40, 40, 40),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderColor = active ? Color.FromArgb(230, 57, 70) : KdsBorder, BorderSize = 1 },
    };

    private static Label MakeStatLabel(string text) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 10f),
        ForeColor = KdsSubtext,
        AutoSize  = true,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer.Dispose();
            _refreshTimer.Dispose();
            _statsTimer.Dispose();
            _alertHideTimer.Dispose();
            _alertAnimTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
