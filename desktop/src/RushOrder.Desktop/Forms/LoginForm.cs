using System.Drawing.Drawing2D;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Forms;

public sealed class LoginForm : Form
{
    private readonly AuthService _auth;
    private readonly ThemeManager _theme;

    // Login panel controls
    private TextBox   _txtEmail     = null!;
    private TextBox   _txtPassword  = null!;
    private CheckBox  _chkRemember  = null!;
    private Button    _btnLogin     = null!;
    private Label     _lblError     = null!;
    private Label     _lblShowHide  = null!;

    // MFA panel controls
    private Panel    _pnlMfa       = null!;
    private TextBox  _txtTotp      = null!;
    private Button   _btnVerifyMfa = null!;
    private Label    _lblMfaError  = null!;
    private string?  _pendingMfaToken;

    public bool LoginSucceeded { get; private set; }

    public LoginForm(AuthService auth, ThemeManager theme)
    {
        _auth  = auth;
        _theme = theme;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text            = "Rush Order — Iniciar sesión";
        Size            = new Size(440, 580);
        MinimumSize     = new Size(440, 580);
        MaximumSize     = new Size(440, 580);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        BackColor       = _theme.Colors.Background;

        // Center content on the actual client area, not the outer window Size —
        // FixedSingle's non-client border otherwise skews the left/right margins.
        var contentWidth = ClientSize.Width;
        const int cardWidth = 360;
        var cardX = (contentWidth - cardWidth) / 2;

        // ── Logo / Brand ──────────────────────────────────────────────────
        var pnlBrand = new Panel
        {
            Size     = new Size(contentWidth, 120),
            Location = new Point(0, 0),
            BackColor = _theme.Colors.SidebarBg,
        };
        // AutoSize so each label's box hugs its actual text (no more, no less) — with fixed
        // oversized boxes, moving the tagline up to balance the gap made its box overlap
        // lblBrand's box and get partially painted over. Centered manually since AutoSize
        // gives up the full-width Size we used for TextAlign.MiddleCenter.
        var lblBrand = new Label
        {
            Text      = "Rush Order",
            Font      = _theme.Fonts.Title,
            ForeColor = Color.FromArgb(230, 57, 70),
            AutoSize  = true,
        };
        // AutoSize's own Width isn't reliable to read immediately — it only reflects the real
        // measured text size once the control is parented/laid out. PreferredSize measures it
        // directly instead, so centering doesn't end up using the Label's stale default 100x23.
        var brandSize = lblBrand.PreferredSize;
        lblBrand.Location = new Point((contentWidth - brandSize.Width) / 2, 30);

        var lblTagline = new Label
        {
            Text      = "Gestión de restaurante en tiempo real",
            Font      = _theme.Fonts.Small,
            ForeColor = Color.FromArgb(160, 160, 160),
            AutoSize  = true,
        };
        var taglineSize = lblTagline.PreferredSize;
        lblTagline.Location = new Point(
            (contentWidth - taglineSize.Width) / 2,
            lblBrand.Location.Y + brandSize.Height + 27);

        pnlBrand.Controls.AddRange([lblBrand, lblTagline]);

        // ── Login card ───────────────────────────────────────────────────
        var pnlCard = new Panel
        {
            Size      = new Size(cardWidth, 340),
            Location  = new Point(cardX, 140),
            BackColor = _theme.Colors.Surface,
            Tag       = "surface",
        };
        RoundCorners(pnlCard, 16);

        var lblEmailHdr = MakeFieldLabel("Correo electrónico", new Point(20, 20));
        // A bordered container holding a borderless TextBox. A single-line TextBox silently
        // ignores any Height it's given — Windows auto-sizes it to fit the font exactly — so
        // setting Height directly on _txtEmail/_txtPassword can't guarantee they match each
        // other. Here the *container* owns the height (Panel honors it) and the borderless
        // TextBox inside is centered using its own real (auto-computed) Height, read back
        // right after construction — that guarantees exact vertical centering regardless of
        // what that auto height turns out to be.
        var pnlEmailBox = new Panel
        {
            Size        = new Size(320, 32),
            Location    = new Point(20, 44),
            BackColor   = _theme.Colors.Input,
        };
        RoundCornersWithBorder(pnlEmailBox, 8, _theme.Colors.Border);
        _txtEmail = new TextBox
        {
            Width       = pnlEmailBox.Width - 8,
            BackColor   = _theme.Colors.Input,
            ForeColor   = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.None,
            Font        = _theme.Fonts.Regular,
            PlaceholderText = "usuario@restaurante.com",
        };
        _txtEmail.Location = new Point(4, (pnlEmailBox.Height - _txtEmail.Height) / 2);
        pnlEmailBox.Controls.Add(_txtEmail);

        var lblPwdHdr = MakeFieldLabel("Contraseña", new Point(20, 88));

        // Same bordered-container pattern, plus the eye icon — overlaying the icon directly on
        // a bordered TextBox would erase the border stroke wherever the icon's fill covers it.
        var pnlPasswordBox = new Panel
        {
            Size        = new Size(320, 32),
            Location    = new Point(20, 112),
            BackColor   = _theme.Colors.Input,
        };
        RoundCornersWithBorder(pnlPasswordBox, 8, _theme.Colors.Border);

        _txtPassword = new TextBox
        {
            Width           = pnlPasswordBox.Width - 8 - 30,
            BackColor       = _theme.Colors.Input,
            ForeColor       = _theme.Colors.TextPrimary,
            BorderStyle     = BorderStyle.None,
            Font            = _theme.Fonts.Regular,
            UseSystemPasswordChar = true,
            PlaceholderText = "••••••••",
        };
        _txtPassword.Location = new Point(4, (pnlPasswordBox.Height - _txtPassword.Height) / 2);

        _lblShowHide = new Label
        {
            Text      = "👁",
            Size      = new Size(26, 24),
            Location  = new Point(pnlPasswordBox.Width - 26 - 4, (pnlPasswordBox.Height - 24) / 2),
            BackColor = _theme.Colors.Input,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand,
            // "Segoe UI Symbol" doesn't cover this emoji, so Windows silently falls back to
            // "Segoe UI Emoji" to draw it — but its very different line metrics threw off
            // MiddleCenter's vertical centering. Declaring the real font fixes it.
            Font      = new Font("Segoe UI Emoji", 10f),
            ForeColor = _theme.Colors.TextSecondary,
        };
        _lblShowHide.Click += (_, _) =>
        {
            _txtPassword.UseSystemPasswordChar = !_txtPassword.UseSystemPasswordChar;
            _lblShowHide.ForeColor = _txtPassword.UseSystemPasswordChar
                ? _theme.Colors.TextSecondary
                : _theme.Colors.Primary;
        };
        pnlPasswordBox.Controls.AddRange([_txtPassword, _lblShowHide]);

        _chkRemember = new CheckBox
        {
            Text      = "Recordar sesión",
            Location  = new Point(20, 158),
            AutoSize  = true,
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextSecondary,
        };

        _lblError = new Label
        {
            Size      = new Size(320, 32),
            Location  = new Point(20, 190),
            ForeColor = _theme.Colors.Error,
            Font      = _theme.Fonts.Small,
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible   = false,
        };

        _btnLogin = new Button
        {
            Text      = "Iniciar sesión",
            Size      = new Size(320, 42),
            Location  = new Point(20, 228),
            Tag       = "primary",
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.SemiBold,
            Cursor    = Cursors.Hand,
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += OnLoginClick;
        RoundCorners(_btnLogin, 8);

        var lblForgot = new Label
        {
            Text      = "¿Olvidaste tu contraseña?",
            Location  = new Point(20, 282),
            AutoSize  = true,
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.Primary,
            Cursor    = Cursors.Hand,
        };

        pnlCard.Controls.AddRange([
            lblEmailHdr, pnlEmailBox,
            lblPwdHdr, pnlPasswordBox,
            _chkRemember, _lblError, _btnLogin, lblForgot,
        ]);

        // ── MFA panel (hidden until required) ────────────────────────────
        _pnlMfa = new Panel
        {
            Size      = new Size(cardWidth, 200),
            Location  = new Point(cardX, 140),
            BackColor = _theme.Colors.Surface,
            Visible   = false,
            Tag       = "surface",
        };
        RoundCorners(_pnlMfa, 16);

        var lblMfaTitle = new Label
        {
            Text      = "Verificación en dos pasos",
            Font      = _theme.Fonts.Bold,
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(20, 20),
            AutoSize  = true,
        };
        var lblMfaDesc = new Label
        {
            Text      = "Introduce el código de tu aplicación de autenticación.",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            Size      = new Size(320, 36),
            Location  = new Point(20, 48),
            AutoSize  = false,
        };
        var lblTotpHdr = MakeFieldLabel("Código TOTP (6 dígitos)", new Point(20, 90));
        var pnlTotpBox = new Panel
        {
            Size      = new Size(320, 32),
            Location  = new Point(20, 114),
            BackColor = _theme.Colors.Input,
        };
        RoundCornersWithBorder(pnlTotpBox, 8, _theme.Colors.Border);
        _txtTotp = new TextBox
        {
            Width       = pnlTotpBox.Width - 8,
            BackColor   = _theme.Colors.Input,
            ForeColor   = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.None,
            MaxLength   = 6,
            Font        = PoppinsFont.New("Poppins", 18f, FontStyle.Bold),
            TextAlign   = HorizontalAlignment.Center,
        };
        _txtTotp.Location = new Point(4, (pnlTotpBox.Height - _txtTotp.Height) / 2);
        pnlTotpBox.Controls.Add(_txtTotp);
        _lblMfaError = new Label
        {
            Size      = new Size(320, 24),
            Location  = new Point(20, 148),
            ForeColor = _theme.Colors.Error,
            Font      = _theme.Fonts.Small,
            Visible   = false,
        };
        _btnVerifyMfa = new Button
        {
            Text      = "Verificar",
            Size      = new Size(320, 42),
            Location  = new Point(20, 144),
            Tag       = "primary",
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.SemiBold,
            Cursor    = Cursors.Hand,
        };
        _btnVerifyMfa.FlatAppearance.BorderSize = 0;
        _btnVerifyMfa.Click += OnMfaVerifyClick;
        RoundCorners(_btnVerifyMfa, 8);

        _pnlMfa.Controls.AddRange([lblMfaTitle, lblMfaDesc, lblTotpHdr, pnlTotpBox, _lblMfaError, _btnVerifyMfa]);

        Controls.AddRange([pnlBrand, pnlCard, _pnlMfa]);

        // Enter key submits — handled manually instead of via Form.AcceptButton, which makes
        // Windows keep re-drawing a "default button" highlight around the button whenever focus
        // moves to another control (NotifyDefault(false) only suppresses it once, until the
        // next focus change re-applies it).
        KeyPreview = true;
        KeyDown   += OnFormKeyDown;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        if (_pnlMfa.Visible)
            OnMfaVerifyClick(_btnVerifyMfa, EventArgs.Empty);
        else
            OnLoginClick(_btnLogin, EventArgs.Empty);
    }

    private async void OnLoginClick(object? sender, EventArgs e)
    {
        _lblError.Visible = false;

        if (string.IsNullOrWhiteSpace(_txtEmail.Text) || string.IsNullOrWhiteSpace(_txtPassword.Text))
        {
            ShowError(_lblError, "Por favor completa todos los campos.");
            return;
        }

        SetLoginLoading(true);
        try
        {
            var result = await _auth.LoginAsync(
                _txtEmail.Text.Trim(), _txtPassword.Text, _chkRemember.Checked);

            if (result.IsSuccess)
            {
                LoginSucceeded = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (result.NeedsMfa)
            {
                _pendingMfaToken = result.PendingMfaToken;
                ShowMfaPanel();
            }
            else if (result.IsNetworkError)
            {
                ShowError(_lblError, "No se pudo conectar al servidor. Verifica tu conexión.");
            }
            else
            {
                ShowError(_lblError, "Credenciales incorrectas. Inténtalo de nuevo.");
            }
        }
        finally
        {
            SetLoginLoading(false);
        }
    }

    private async void OnMfaVerifyClick(object? sender, EventArgs e)
    {
        _lblMfaError.Visible = false;

        if (_txtTotp.Text.Length != 6 || !_txtTotp.Text.All(char.IsDigit))
        {
            ShowError(_lblMfaError, "El código debe ser de 6 dígitos.");
            return;
        }

        _btnVerifyMfa.Enabled = false;
        _btnVerifyMfa.Text    = "Verificando…";
        try
        {
            var result = await _auth.VerifyMfaAsync(
                _pendingMfaToken!, _txtTotp.Text, _chkRemember.Checked);

            if (result.IsSuccess)
            {
                LoginSucceeded = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                ShowError(_lblMfaError, "Código incorrecto o expirado.");
            }
        }
        finally
        {
            _btnVerifyMfa.Enabled = true;
            _btnVerifyMfa.Text    = "Verificar";
        }
    }

    private void ShowMfaPanel()
    {
        Controls.OfType<Panel>().FirstOrDefault(p => p.Tag is "surface" && p != _pnlMfa)
            !.Visible = false;
        _pnlMfa.Visible   = true;
        _txtTotp.Focus();
    }

    private void SetLoginLoading(bool loading)
    {
        _btnLogin.Enabled = !loading;
        _btnLogin.Text    = loading ? "Iniciando sesión…" : "Iniciar sesión";
        _txtEmail.Enabled    = !loading;
        _txtPassword.Enabled = !loading;
    }

    private static void ShowError(Label lbl, string message)
    {
        lbl.Text    = message;
        lbl.Visible = true;
    }

    // GDI's DrawText reserves a few pixels of left margin before the glyph ink, so an
    // AutoSize label's Text appears to start a few px right of its Location — nudge left
    // to visually align with the textbox border underneath, which has no such margin.
    private Label MakeFieldLabel(string text, Point location) => new()
    {
        Text      = text,
        Location  = new Point(location.X - 3, location.Y),
        AutoSize  = true,
        Font      = _theme.Fonts.Small,
        ForeColor = _theme.Colors.TextSecondary,
    };

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Clips a control to a rounded-rectangle shape (no drawn stroke).</summary>
    private static void RoundCorners(Control control, int radius)
    {
        void Apply()
        {
            using var path = RoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region = new Region(path);
        }
        control.Resize += (_, _) => Apply();
        Apply();
    }

    /// <summary>Clips the control to a rounded rectangle and hand-draws a matching border stroke —
    /// used instead of BorderStyle.FixedSingle, which always renders square corners.</summary>
    private static void RoundCornersWithBorder(Control control, int radius, Color borderColor)
    {
        RoundCorners(control, radius);
        control.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRectPath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
            using var pen = new Pen(borderColor);
            e.Graphics.DrawPath(pen, path);
        };
    }
}
