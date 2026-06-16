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
    private readonly AuthService              _auth;
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
    private Label   _lblLogo       = null!;
    private Panel   _pnlNavItems   = null!;
    private Panel   _pnlUser       = null!;
    private Label   _lblAvatar     = null!;
    private Label   _lblUserName   = null!;
    private NavButton _btnToggle   = null!;

    // Header controls
    private Label   _lblTitle      = null!;
    private Label   _lblDot        = null!;
    private Label   _lblStatus     = null!;
    private Label   _lblSyncStatus = null!;
    private Label   _lblSyncBadge  = null!;
    private Label   _lblClock      = null!;
    private Button  _btnNotify     = null!;
    private Label   _lblBadge      = null!;
    private int     _notifyCount   = 0;

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

    // Content fade
    private Form? _fadeOverlay;

    public MainForm(
        ThemeManager theme, NavigationService nav,
        ToastNotificationManager toasts, AppState state, AuthService auth,
        SyncService sync, ConnectivityMonitor connectivity,
        PrintService print, UpdateService update,
        IServiceProvider sp)
    {
        _theme        = theme;
        _nav          = nav;
        _toasts       = toasts;
        _state        = state;
        _auth         = auth;
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
        Text            = "RushOrder";
        Size            = new Size(1280, 800);
        MinimumSize     = new Size(900, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = _theme.Colors.Background;
        DoubleBuffered  = true;

        BuildSidebar();
        BuildHeader();
        BuildContent();

        Controls.AddRange([_pnlSidebar, _pnlHeader, _pnlContent]);

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

        _lblLogo = new Label
        {
            Text      = "RushOrder",
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 57, 70),
            Size      = new Size(SidebarExpanded, 56),
            Location  = new Point(0, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _pnlNavItems = new Panel
        {
            Location  = new Point(0, 60),
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

        // Sidebar toggle button
        _btnToggle = new NavButton
        {
            IconText  = "◀",
            LabelText = "Colapsar",
            Width     = SidebarExpanded,
            Height    = 38,
            Dock      = DockStyle.Bottom,
        };
        _btnToggle.Click += (_, _) => ToggleSidebar();

        _pnlUser.Controls.AddRange([_lblAvatar, _lblUserName]);
        _pnlSidebar.Controls.AddRange([_lblLogo, _pnlNavItems, _pnlUser, _btnToggle]);

        // Sidebar animation tick
        _sidebarTimer.Tick += OnSidebarAnimTick;
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
        _btnToggle.LabelText = _sidebarExpanded ? "Colapsar" : "";
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
        _lblLogo.Text = collapsed ? "R" : "RushOrder";
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
        AddBottomBorder(_pnlHeader);

        _lblTitle = new Label
        {
            Text      = "Dashboard",
            Font      = _theme.Fonts.Bold,
            ForeColor = _theme.Colors.TextPrimary,
            AutoSize  = true,
            Location  = new Point(16, 14),
        };

        _lblDot = new Label
        {
            Text      = "●",
            Font      = new Font("Segoe UI", 10f),
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

        _btnNotify = new Button
        {
            Text      = "🔔",
            Size      = new Size(36, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = _theme.Colors.TextPrimary,
            Font      = new Font("Segoe UI Symbol", 14f),
            Cursor    = Cursors.Hand,
        };
        _btnNotify.FlatAppearance.BorderSize = 0;

        _lblBadge = new Label
        {
            Text      = "",
            Size      = new Size(18, 18),
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 7f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        _lblSyncStatus = new Label
        {
            Text      = "",
            Font      = new Font("Segoe UI", 8f),
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
            Font      = new Font("Segoe UI", 7f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        _syncClearTimer.Tick += (_, _) =>
        {
            _syncClearTimer.Stop();
            _lblSyncStatus.Visible = false;
            RelayoutHeader();
        };

        _pnlHeader.Controls.AddRange([_lblTitle, _lblDot, _lblStatus,
            _lblSyncStatus, _lblSyncBadge, _lblClock, _btnNotify, _lblBadge]);
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
        _btnNotify.Location = new Point(_lblClock.Left - 44,       cy - 18);
        _lblBadge.Location  = new Point(_btnNotify.Right - 10,     _btnNotify.Top - 4);

        // Sync badge (pending count) just left of notify bell
        if (_lblSyncBadge.Visible)
            _lblSyncBadge.Location = new Point(_btnNotify.Left - _lblSyncBadge.Width - 6, cy - 9);

        // Sync status message to the right of the connection dot
        _lblDot.Location    = new Point(200, cy - _lblDot.Height / 2);
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
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
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
            Font      = new Font("Segoe UI", 8.5f),
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
            Font      = new Font("Segoe UI", 11f),
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

        Load += async (_, _) => await OnLoadAsync();
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

    private async Task OnLoadAsync()
    {
        _toasts.SetOwner(this);

        var autoLoggedIn = await _auth.TryAutoLoginAsync();
        if (!autoLoggedIn)
        {
            using var login = new LoginForm(_auth, _theme);
            var result = login.ShowDialog(this);
            if (result != DialogResult.OK)
            {
                Application.Exit();
                return;
            }
        }

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
        _lblTitle.Text = GetViewTitle(view);
        RelayoutHeader();
    }

    private void FadeToView(UserControl newView)
    {
        // Capture current content as overlay
        if (_pnlContent.Width > 0 && _pnlContent.Height > 0 && _pnlContent.Controls.Count > 1)
        {
            var bmp    = new Bitmap(_pnlContent.Width, _pnlContent.Height);
            _pnlContent.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            var bounds = _pnlContent.RectangleToScreen(new Rectangle(0, 0, _pnlContent.Width, _pnlContent.Height));

            _fadeOverlay?.Close();
            _fadeOverlay = new FadeOverlayForm(bounds, bmp);
            _fadeOverlay.Show(this);
        }

        // Swap view — preserve LoadingOverlay and update banner
        for (int i = _pnlContent.Controls.Count - 1; i >= 0; i--)
        {
            var c = _pnlContent.Controls[i];
            if (c is not LoadingOverlay && c != _pnlUpdateBanner)
            {
                _pnlContent.Controls.RemoveAt(i);
                c.Dispose();
            }
        }
        newView.Dock = DockStyle.Fill;
        _pnlContent.Controls.Add(newView);
        newView.BringToFront();

        // Fade out the overlay
        if (_fadeOverlay is FadeOverlayForm fof)
        {
            var captured = fof;
            fof.FadeOut(() => { if (!captured.IsDisposed) captured.Close(); });
        }
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

    public void IncrementNotificationBadge()
    {
        _notifyCount++;
        _lblBadge.Text    = _notifyCount > 9 ? "9+" : _notifyCount.ToString();
        _lblBadge.Visible = true;
        RelayoutHeader();
    }

    public void ClearNotificationBadge()
    {
        _notifyCount  = 0;
        _lblBadge.Visible = false;
    }

    private static string GetViewTitle(UserControl view) => view.GetType().Name
        .Replace("View", "")
        .Replace("Panel", "");

    private static void AddBottomBorder(Control c)
    {
        c.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(229, 229, 229), 1);
            e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
        };
    }

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

internal sealed class FadeOverlayForm : Form
{
    private double _opacity = 1.0;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };

    public FadeOverlayForm(Rectangle screenBounds, Bitmap capture)
    {
        FormBorderStyle       = FormBorderStyle.None;
        ShowInTaskbar         = false;
        TopMost               = false;
        Opacity               = 1.0;
        Bounds                = screenBounds;
        BackgroundImage       = capture;
        BackgroundImageLayout = ImageLayout.Stretch;
        StartPosition         = FormStartPosition.Manual;
    }

    public void FadeOut(Action onComplete)
    {
        _timer.Tick += (_, _) =>
        {
            _opacity = Math.Max(0, _opacity - 0.15);
            Opacity  = _opacity;
            if (_opacity <= 0)
            {
                _timer.Stop();
                _timer.Dispose();
                onComplete();
            }
        };
        _timer.Start();
    }
}
