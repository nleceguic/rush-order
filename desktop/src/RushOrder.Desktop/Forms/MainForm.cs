using RushOrder.Desktop.Forms.Controls;
using RushOrder.Desktop.Navigation;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Sync;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.Desktop.Views.Dashboard;
using RushOrder.Desktop.Views.FloorPlan;
using RushOrder.Desktop.Views.Kitchen;
using RushOrder.Desktop.Views.Menu;
using RushOrder.Desktop.Views.Orders;
using RushOrder.Desktop.Views.Print;
using RushOrder.Desktop.Views.Statistics;
using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Forms;

public sealed class MainForm : Form
{
    private const int SidebarCollapsed = 60;
    private const int SidebarExpanded  = 220;
    private const int HeaderHeight     = 52;

    private readonly ThemeManager             _theme;
    private readonly NavigationService        _nav;
    private readonly ToastNotificationManager _toasts;
    private readonly AppState                 _state;
    private readonly SyncService              _sync;
    private readonly ConnectivityMonitor      _connectivity; // holds reference so DI keeps it alive
    private readonly PrintService             _print;
    private readonly UpdateService            _update;
    private readonly IServiceProvider         _sp;

    // Update banner
    private Panel   _pnlUpdateBanner = null!;
    private Label   _lblUpdateText   = null!;
    private Button  _btnUpdateNow    = null!;
    private Button  _btnUpdateDismiss= null!;

    // Layout panels
    private Panel   _pnlSidebar    = null!;
    private Panel   _pnlHeader     = null!;
    private Panel   _pnlContent    = null!;
    private Panel   _pnlLoadingOverlay = null!;

    // Sidebar controls
    private Panel   _pnlNavItems   = null!;
    private Panel   _pnlUser       = null!;
    private Label   _lblAvatar     = null!;
    private Label   _lblUserName   = null!;
    private NavButton _btnToggle   = null!;

    // Header controls
    private Label   _lblDot        = null!;
    private Label   _lblStatus     = null!;
    private Label   _lblSyncStatus = null!;
    private Label   _lblSyncBadge  = null!;
    private Label   _lblClock      = null!;

    // Sync status auto-clear timer
    private readonly System.Windows.Forms.Timer _syncClearTimer = new() { Interval = 5000 };

    // Nav buttons
    private readonly List<NavButton> _navButtons = [];
    private NavButton? _activeNav;

    // Sidebar animation
    private readonly System.Windows.Forms.Timer _sidebarTimer = new() { Interval = 12 };
    private bool _sidebarExpanded = true;
    private int  _sidebarTarget   = SidebarExpanded;

    // Clock
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };

    // Content transition
    private System.Windows.Forms.Timer? _slideTimer;

    public MainForm(
        ThemeManager theme, NavigationService nav,
        ToastNotificationManager toasts, AppState state,
        SyncService sync, ConnectivityMonitor connectivity,
        PrintService print, UpdateService update,
        IServiceProvider sp)
    {
        _theme        = theme;
        _nav          = nav;
        _toasts       = toasts;
        _state        = state;
        _sync         = sync;
        _connectivity = connectivity;
        _print        = print;
        _update       = update;
        _sp           = sp;

        InitializeComponent();
        WireUpEvents();
    }

    private void InitializeComponent()
    {
        Text            = "Rush Order";
        Size            = new Size(1280, 800);
        MinimumSize     = new Size(900, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = _theme.Colors.Background;
        DoubleBuffered  = true;

        BuildSidebar();
        BuildHeader();
        BuildContent();

        // Dock resolves in reverse of Controls.Add order: the LAST control added claims its
        // space against the form's full original bounds; earlier ones divide up whatever is
        // left. _pnlSidebar is added last so its Dock=Left column runs the full window height
        // (past the header, not just below it); _pnlHeader (Top) then only spans the remaining
        // area to the right of the sidebar; _pnlContent (Fill) is added first so it always ends
        // up with whatever's left over after both.
        Controls.AddRange([_pnlContent, _pnlHeader, _pnlSidebar]);

        Resize += (_, _) => RelayoutMain();
        RelayoutMain();
    }

    // ── Sidebar ──────────────────────────────────────────────────────────

    private void BuildSidebar()
    {
        _pnlSidebar = new Panel
        {
            Width     = SidebarExpanded,
            BackColor = _theme.Colors.SidebarBg,
            Dock      = DockStyle.Left,
        };
        // Only the right corners are rounded — the left edge is flush against the window's
        // own edge, so rounding it would just cut a notch out of a straight border.
        _pnlSidebar.Resize += (_, _) => ApplySidebarRegion();
        ApplySidebarRegion();

        _pnlNavItems = new Panel
        {
            Location  = new Point(0, 0),
            Size      = new Size(SidebarExpanded, 500),
            BackColor = Color.Transparent,
        };

        var navDefs = new (string Icon, string Label, Action Navigate)[]
        {
            ("⊞",  "Dashboard",    () => _nav.ClearAndNavigateTo<DashboardView>()),
            ("⬛",  "Mesas",        () => _nav.ClearAndNavigateTo<FloorPlanView>()),
            ("≡",  "Pedidos",      () => _nav.ClearAndNavigateTo<OrdersView>()),
            ("◈",  "Cocina",       () => KitchenDisplayForm.Launch(_sp.GetRequiredService<KitchenDisplayForm>(), this)),
            ("☰",  "Menú",         () => _nav.ClearAndNavigateTo<MenuManagementControl>()),
            ("♟",  "Camareros",    () => { }),
            ("◻",  "Reservas",     () => { }),
            ("≈",  "Estadísticas", () => _nav.ClearAndNavigateTo<StatisticsView>()),
            ("◇",  "Previsión",    () => _nav.ClearAndNavigateTo<Views.Forecast.DemandForecastControl>()),
            ("✦",  "Panel IA",     () => _nav.ClearAndNavigateTo<Views.AiDashboard.AiDashboardView>()),
            ("$",  "Facturación",  () => { }),
            ("⚙",  "Config.",      () => { using var d = new PrinterConfigDialog(_print, _theme); d.ShowDialog(this); }),
        };

        int y = 0;
        foreach (var (icon, label, navigate) in navDefs)
        {
            var btn = new NavButton
            {
                IconText  = icon,
                LabelText = label,
                Width     = SidebarExpanded,
                Height    = 46,
                Location  = new Point(0, y),
            };
            var capturedNavigate = navigate;
            var capturedBtn      = btn;
            btn.Click += (_, _) => ActivateNav(capturedBtn, capturedNavigate);
            _navButtons.Add(btn);
            _pnlNavItems.Controls.Add(btn);
            y += 46;
        }

        // User section at bottom
        _pnlUser = new Panel
        {
            Height    = 64,
            BackColor = Color.FromArgb(20, 20, 20),
            Dock      = DockStyle.Bottom,
        };
        _lblAvatar = new Label
        {
            Text      = "?", // placeholder until OnUserChanged fills in real initials
            Size      = new Size(36, 36),
            Location  = new Point(12, 14),
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            Font      = _theme.Fonts.Avatar,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _lblUserName = new Label
        {
            Text      = "",
            Font      = _theme.Fonts.Small,
            ForeColor = Color.FromArgb(200, 200, 200),
            Size      = new Size(140, 36),
            Location  = new Point(54, 14),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // Sidebar toggle button — icon only, left-aligned like the nav items above it
        // (IsCollapsed stays false: that's the mode NavButton uses to draw the icon at a
        // fixed left offset instead of centered across the whole width).
        _btnToggle = new NavButton
        {
            IconText  = "◀",
            LabelText = "",
            Width     = SidebarExpanded,
            Height    = 38,
            Dock      = DockStyle.Bottom,
        };
        _btnToggle.Click += (_, _) => ToggleSidebar();

        _pnlUser.Controls.AddRange([_lblAvatar, _lblUserName]);
        _pnlSidebar.Controls.AddRange([_pnlNavItems, _pnlUser, _btnToggle]);

        // Sidebar animation tick
        _sidebarTimer.Tick += OnSidebarAnimTick;
    }

    private void ApplySidebarRegion()
    {
        if (_pnlSidebar.Width <= 0 || _pnlSidebar.Height <= 0) return;
        using var path = GdiExtensions.CreateRightRoundedRect(
            new RectangleF(0, 0, _pnlSidebar.Width, _pnlSidebar.Height), 16);
        _pnlSidebar.Region = new Region(path);
    }

    private void ActivateNav(NavButton btn, Action navigate)
    {
        if (_activeNav == btn) return;
        if (_activeNav is not null) _activeNav.IsActive = false;
        _activeNav = btn;
        btn.IsActive = true;
        navigate();
    }

    private void ToggleSidebar()
    {
        _sidebarExpanded = !_sidebarExpanded;
        _sidebarTarget   = _sidebarExpanded ? SidebarExpanded : SidebarCollapsed;
        _btnToggle.IconText  = _sidebarExpanded ? "◀" : "▶";
        _sidebarTimer.Start();
    }

    private void OnSidebarAnimTick(object? sender, EventArgs e)
    {
        var current = _pnlSidebar.Width;
        var delta   = (_sidebarTarget - current);

        if (Math.Abs(delta) <= 1)
        {
            _pnlSidebar.Width = _sidebarTarget;
            _sidebarTimer.Stop();
            UpdateSidebarCollapsedState(_pnlSidebar.Width == SidebarCollapsed);
            return;
        }

        _pnlSidebar.Width = current + (int)(delta * 0.25);
        UpdateSidebarCollapsedState(_pnlSidebar.Width < (SidebarCollapsed + SidebarExpanded) / 2);
    }

    private void UpdateSidebarCollapsedState(bool collapsed)
    {
        _lblUserName.Visible = !collapsed;
        foreach (var btn in _navButtons)
        {
            btn.Width       = _pnlSidebar.Width;
            btn.IsCollapsed = collapsed;
        }
        _btnToggle.Width = _pnlSidebar.Width;
    }

    // ── Header ───────────────────────────────────────────────────────────

    private void BuildHeader()
    {
        _pnlHeader = new Panel
        {
            Height    = HeaderHeight,
            BackColor = _theme.Colors.HeaderBg,
            Dock      = DockStyle.Top,
        };

        _lblDot = new Label
        {
            Text      = "●",
            Font      = PoppinsFont.New("Poppins", 10f),
            ForeColor = _theme.Colors.Success,
            AutoSize  = true,
        };
        _lblStatus = new Label
        {
            Text      = "Online",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            AutoSize  = true,
        };

        _lblClock = new Label
        {
            Text      = DateTime.Now.ToString("HH:mm:ss"),
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextSecondary,
            AutoSize  = true,
        };

        _lblSyncStatus = new Label
        {
            Text      = "",
            Font      = PoppinsFont.New("Poppins", 8f),
            ForeColor = _theme.Colors.TextSecondary,
            AutoSize  = true,
            Visible   = false,
        };

        _lblSyncBadge = new Label
        {
            Text      = "",
            Size      = new Size(22, 18),
            BackColor = Color.FromArgb(204, 102, 0),
            ForeColor = Color.White,
            Font      = PoppinsFont.New("Poppins", 7f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        _syncClearTimer.Tick += (_, _) =>
        {
            _syncClearTimer.Stop();
            _lblSyncStatus.Visible = false;
            RelayoutHeader();
        };

        _pnlHeader.Controls.AddRange([_lblDot, _lblStatus,
            _lblSyncStatus, _lblSyncBadge, _lblClock]);
        _pnlHeader.Resize += (_, _) => RelayoutHeader();
        RelayoutHeader();

        _clockTimer.Tick += (_, _) =>
            _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
    }

    private void RelayoutHeader()
    {
        var w = _pnlHeader.Width;
        int cy = HeaderHeight / 2;

        _lblClock.Location  = new Point(w - _lblClock.Width - 16, cy - _lblClock.Height / 2);

        // Sync badge (pending count) just left of the clock
        if (_lblSyncBadge.Visible)
            _lblSyncBadge.Location = new Point(_lblClock.Left - _lblSyncBadge.Width - 10, cy - 9);

        // Sync status message to the right of the connection dot
        _lblDot.Location    = new Point(16, cy - _lblDot.Height / 2);
        _lblStatus.Location = new Point(_lblDot.Right + 4, cy - _lblStatus.Height / 2);

        if (_lblSyncStatus.Visible)
            _lblSyncStatus.Location = new Point(_lblStatus.Right + 16, cy - _lblSyncStatus.Height / 2);
    }

    // ── Content area ─────────────────────────────────────────────────────

    private void BuildContent()
    {
        _pnlContent = new Panel
        {
            BackColor = _theme.Colors.Background,
            Dock      = DockStyle.Fill,
        };

        // Update banner — sits at top of content area, hidden until update found
        _pnlUpdateBanner = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            Visible   = false,
            BackColor = Color.FromArgb(255, 193, 7), // amber
        };
        _lblUpdateText = new Label
        {
            Text      = "",
            Font      = PoppinsFont.New("Poppins", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 33, 33),
            AutoSize  = true,
            Location  = new Point(12, 10),
        };
        _btnUpdateNow = new Button
        {
            Text      = "Actualizar",
            Size      = new Size(90, 24),
            Location  = new Point(0, 7),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(33, 33, 33),
            ForeColor = Color.White,
            Font      = PoppinsFont.New("Poppins", 8.5f),
            Cursor    = Cursors.Hand,
        };
        _btnUpdateNow.FlatAppearance.BorderSize = 0;
        _btnUpdateNow.Click += OnUpdateNowClick;

        _btnUpdateDismiss = new Button
        {
            Text      = "×",
            Size      = new Size(28, 24),
            Location  = new Point(0, 7),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(33, 33, 33),
            Font      = PoppinsFont.New("Poppins", 11f),
            Cursor    = Cursors.Hand,
        };
        _btnUpdateDismiss.FlatAppearance.BorderSize = 0;
        _btnUpdateDismiss.Click += (_, _) => _pnlUpdateBanner.Visible = false;
        _pnlUpdateBanner.Resize += (_, _) => PositionBannerButtons();
        _pnlUpdateBanner.Controls.AddRange([_lblUpdateText, _btnUpdateNow, _btnUpdateDismiss]);

        _pnlLoadingOverlay = new LoadingOverlay
        {
            Dock    = DockStyle.Fill,
            Visible = false,
        };

        // Banner must be added FIRST so it docks at the outer edge (top)
        _pnlContent.Controls.Add(_pnlUpdateBanner);
        _pnlContent.Controls.Add(_pnlLoadingOverlay);
    }

    private void PositionBannerButtons()
    {
        int cy = _pnlUpdateBanner.Height / 2;
        _btnUpdateNow.Location     = new Point(_pnlUpdateBanner.Width - 140, cy - 12);
        _btnUpdateDismiss.Location = new Point(_pnlUpdateBanner.Width - 44,  cy - 12);
    }

    private void RelayoutMain()
    {
        // Panels are Dock-based, nothing manual needed unless we have fixed calculations.
    }

    // ── Navigation events ────────────────────────────────────────────────

    private void WireUpEvents()
    {
        _nav.Navigated += OnNavigated;
        _state.UserChanged += OnUserChanged;
        _state.OnlineStatusChanged += OnOnlineStatusChanged;

        _sync.StatusMessage      += OnSyncStatusMessage;
        _sync.PendingCountChanged += OnPendingCountChanged;
        _sync.ConflictHandler     = ShowConflictDialog;

        _update.UpdateAvailable += OnUpdateAvailable;
        _update.InstallReady    += OnInstallReady;
        _update.DownloadProgress += pct => { };

        Load += (_, _) => OnLoadAsync();
    }

    private void OnSyncStatusMessage(string msg)
    {
        if (InvokeRequired) { Invoke(() => OnSyncStatusMessage(msg)); return; }
        _syncClearTimer.Stop();
        if (string.IsNullOrEmpty(msg))
        {
            _lblSyncStatus.Visible = false;
        }
        else
        {
            _lblSyncStatus.Text    = msg;
            _lblSyncStatus.Visible = true;
            // Persistent "✓" messages auto-clear; spinner messages stay until replaced
            if (msg.StartsWith("✓"))
                _syncClearTimer.Start();
        }
        RelayoutHeader();
    }

    private void OnPendingCountChanged(int count)
    {
        if (InvokeRequired) { Invoke(() => OnPendingCountChanged(count)); return; }
        _lblSyncBadge.Text    = count > 99 ? "99+" : count.ToString();
        _lblSyncBadge.Visible = count > 0;
        RelayoutHeader();
    }

    private async Task<ConflictResolution> ShowConflictDialog(ConflictInfo info)
    {
        var tcs = new TaskCompletionSource<ConflictResolution>();
        Invoke(() =>
        {
            using var dlg    = new ConflictResolutionDialog(info, _theme);
            var dialogResult = dlg.ShowDialog(this);
            tcs.SetResult(dialogResult == DialogResult.OK ? dlg.Resolution : ConflictResolution.Defer);
        });
        return await tcs.Task;
    }

    private void OnLoadAsync()
    {
        _toasts.SetOwner(this);

        // Navigate to default view
        _nav.ClearAndNavigateTo<DashboardView>();
        ActivateNav(_navButtons[0], () => { });

        // Check for updates in background — non-blocking
        _ = _update.CheckForUpdatesAsync();
    }

    private void OnNavigated(UserControl view)
    {
        if (InvokeRequired) { Invoke(() => OnNavigated(view)); return; }

        FadeToView(view);
        RelayoutHeader();
    }

    private void FadeToView(UserControl newView)
    {
        // Swap view immediately — preserve LoadingOverlay and update banner. The old view just
        // disappears (same as the PWA's checkout steps: the outgoing step unmounts with no exit
        // animation); only the INCOMING view slides in.
        for (int i = _pnlContent.Controls.Count - 1; i >= 0; i--)
        {
            var c = _pnlContent.Controls[i];
            if (c is not LoadingOverlay && c != _pnlUpdateBanner)
            {
                _pnlContent.Controls.RemoveAt(i);
                c.Dispose();
            }
        }

        _slideTimer?.Stop();
        _slideTimer?.Dispose();
        _slideTimer = null;

        if (_pnlContent.Width <= 0 || _pnlContent.Height <= 0)
        {
            newView.Dock = DockStyle.Fill;
            _pnlContent.Controls.Add(newView);
            newView.BringToFront();
            return;
        }

        // Slide the real control in directly — no bitmap snapshot, no separate layered Form.
        // The previous version rendered the new view to a full-size Bitmap and animated it via
        // a borderless Form's Opacity; a Form with Opacity set at all runs as a layered window,
        // which forces Windows to alpha-recomposite that entire bitmap on every single frame.
        // For a near-full-window image at a 15ms tick that was expensive enough to visibly drop
        // frames. Animating Control.Location on the live control instead is a plain move+repaint
        // — one of the cheapest operations WinForms has — and keeps the view interactive/loading
        // live during the slide instead of showing a frozen frame of it.
        const int slideDistance = 40;
        const int durationMs    = 200;

        newView.Dock   = DockStyle.None;
        newView.Bounds = new Rectangle(slideDistance, 0, _pnlContent.Width, _pnlContent.Height);
        _pnlContent.Controls.Add(newView);
        newView.BringToFront();

        var elapsed = 0;
        var timer   = new System.Windows.Forms.Timer { Interval = 15 };
        _slideTimer = timer;
        timer.Tick += (_, _) =>
        {
            elapsed += timer.Interval;
            var t     = Math.Min(1.0, elapsed / (double)durationMs);
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            newView.Left = (int)(slideDistance * (1 - eased));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();
                if (_slideTimer == timer) _slideTimer = null;
                newView.Dock = DockStyle.Fill; // resume normal responsive layout once settled
            }
        };
        timer.Start();
    }

    private void OnUserChanged(UserInfo? user)
    {
        if (InvokeRequired) { Invoke(() => OnUserChanged(user)); return; }
        _lblAvatar.Text  = user?.AvatarInitials ?? "?";
        _lblUserName.Text = user?.FullName ?? "";
    }

    private void OnOnlineStatusChanged(bool isOnline)
    {
        if (InvokeRequired) { Invoke(() => OnOnlineStatusChanged(isOnline)); return; }
        _lblDot.ForeColor    = isOnline ? _theme.Colors.Success : _theme.Colors.Error;
        _lblStatus.Text      = isOnline ? "Online" : "Offline";
        RelayoutHeader();
    }

    public void ShowLoading(bool show) => _pnlLoadingOverlay.Visible = show;

    private void OnUpdateAvailable(UpdateInfo info)
    {
        if (InvokeRequired) { Invoke(() => OnUpdateAvailable(info)); return; }

        var tag = info.IsCritical ? " [CRÍTICA]" : "";
        _lblUpdateText.Text         = $"⬆ Nueva versión {info.Version} disponible{tag}";
        _pnlUpdateBanner.BackColor  = info.IsCritical
            ? Color.FromArgb(220, 53, 69)
            : Color.FromArgb(255, 193, 7);
        _lblUpdateText.ForeColor    = info.IsCritical ? Color.White : Color.FromArgb(33, 33, 33);
        _btnUpdateDismiss.Visible   = !info.IsCritical;
        _pnlUpdateBanner.Visible    = true;
        PositionBannerButtons();
    }

    private void OnInstallReady()
    {
        if (InvokeRequired) { Invoke(OnInstallReady); return; }
        _lblUpdateText.Text = $"✓ Versión {_update.PendingUpdate?.Version} lista — reinicia para aplicar";
        _btnUpdateNow.Text  = "Reiniciar";
    }

    private async void OnUpdateNowClick(object? sender, EventArgs e)
    {
        if (_update.PendingUpdate is null) return;

        if (_btnUpdateNow.Text == "Reiniciar")
        {
            _update.ApplyUpdate();
            return;
        }

        _btnUpdateNow.Enabled = false;
        _btnUpdateNow.Text    = "Descargando…";
        await _update.DownloadAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sidebarTimer.Dispose();
        _clockTimer.Dispose();
        _syncClearTimer.Dispose();
        _connectivity.Dispose();
        _update.Dispose();
        _theme.Fonts.Dispose();
        base.OnFormClosed(e);
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

internal sealed class LoadingOverlay : Panel
{
    private readonly Label _spinner;
    private readonly System.Windows.Forms.Timer _spinTimer;
    private int _frame;

    public LoadingOverlay()
    {
        BackColor = Color.FromArgb(120, 0, 0, 0);
        _spinner  = new Label
        {
            Text      = "◐",
            Font      = new Font("Segoe UI Symbol", 32f),
            ForeColor = Color.White,
            AutoSize  = true,
        };
        Controls.Add(_spinner);

        _spinTimer = new System.Windows.Forms.Timer { Interval = 120 };
        string[] frames = ["◐", "◓", "◑", "◒"];
        _spinTimer.Tick += (_, _) =>
        {
            _frame = (_frame + 1) % frames.Length;
            _spinner.Text = frames[_frame];
        };

        VisibleChanged += (_, _) =>
        {
            if (Visible) { CenterSpinner(); _spinTimer.Start(); }
            else _spinTimer.Stop();
        };
        Resize += (_, _) => CenterSpinner();
    }

    private void CenterSpinner()
    {
        _spinner.Location = new Point(
            (Width  - _spinner.Width)  / 2,
            (Height - _spinner.Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _spinTimer.Dispose();
        base.Dispose(disposing);
    }
}
