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
        Text            = "RushOrder — Iniciar sesión";
        Size            = new Size(440, 580);
        MinimumSize     = new Size(440, 580);
        MaximumSize     = new Size(440, 580);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        BackColor       = _theme.Colors.Background;

        // ── Logo / Brand ──────────────────────────────────────────────────
        var pnlBrand = new Panel
        {
            Size     = new Size(440, 120),
            Location = new Point(0, 0),
            BackColor = _theme.Colors.SidebarBg,
        };
        var lblBrand = new Label
        {
            Text      = "RushOrder",
            Font      = _theme.Fonts.Title,
            ForeColor = Color.FromArgb(230, 57, 70),
            AutoSize  = false,
            Size      = new Size(440, 60),
            Location  = new Point(0, 30),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var lblTagline = new Label
        {
            Text      = "Gestión de restaurante en tiempo real",
            Font      = _theme.Fonts.Small,
            ForeColor = Color.FromArgb(160, 160, 160),
            AutoSize  = false,
            Size      = new Size(440, 24),
            Location  = new Point(0, 88),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        pnlBrand.Controls.AddRange([lblBrand, lblTagline]);

        // ── Login card ───────────────────────────────────────────────────
        var pnlCard = new Panel
        {
            Size      = new Size(360, 340),
            Location  = new Point(40, 140),
            BackColor = _theme.Colors.Surface,
            Tag       = "surface",
        };
        RoundPanel(pnlCard);

        var lblEmailHdr = MakeFieldLabel("Correo electrónico", new Point(20, 20));
        _txtEmail = new TextBox
        {
            Size        = new Size(320, 32),
            Location    = new Point(20, 44),
            BackColor   = _theme.Colors.Input,
            ForeColor   = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = _theme.Fonts.Regular,
            PlaceholderText = "usuario@restaurante.com",
        };

        var lblPwdHdr = MakeFieldLabel("Contraseña", new Point(20, 88));
        _txtPassword = new TextBox
        {
            Size            = new Size(290, 32),
            Location        = new Point(20, 112),
            BackColor       = _theme.Colors.Input,
            ForeColor       = _theme.Colors.TextPrimary,
            BorderStyle     = BorderStyle.FixedSingle,
            Font            = _theme.Fonts.Regular,
            UseSystemPasswordChar = true,
            PlaceholderText = "••••••••",
        };
        _lblShowHide = new Label
        {
            Text      = "👁",
            Size      = new Size(30, 32),
            Location  = new Point(310, 112),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI Symbol", 12f),
            ForeColor = _theme.Colors.TextSecondary,
        };
        _lblShowHide.Click += (_, _) =>
        {
            _txtPassword.UseSystemPasswordChar = !_txtPassword.UseSystemPasswordChar;
            _lblShowHide.ForeColor = _txtPassword.UseSystemPasswordChar
                ? _theme.Colors.TextSecondary
                : _theme.Colors.Primary;
        };

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
            lblEmailHdr, _txtEmail,
            lblPwdHdr, _txtPassword, _lblShowHide,
            _chkRemember, _lblError, _btnLogin, lblForgot,
        ]);

        // ── MFA panel (hidden until required) ────────────────────────────
        _pnlMfa = new Panel
        {
            Size      = new Size(360, 200),
            Location  = new Point(40, 140),
            BackColor = _theme.Colors.Surface,
            Visible   = false,
            Tag       = "surface",
        };
        RoundPanel(_pnlMfa);

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
        _txtTotp = new TextBox
        {
            Size        = new Size(320, 32),
            Location    = new Point(20, 114),
            BackColor   = _theme.Colors.Input,
            ForeColor   = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            MaxLength   = 6,
            Font        = new Font("Segoe UI", 18f, FontStyle.Bold),
            TextAlign   = HorizontalAlignment.Center,
        };
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

        _pnlMfa.Controls.AddRange([lblMfaTitle, lblMfaDesc, lblTotpHdr, _txtTotp, _lblMfaError, _btnVerifyMfa]);

        Controls.AddRange([pnlBrand, pnlCard, _pnlMfa]);

        // Enter key submits
        AcceptButton = _btnLogin;
        KeyPreview   = true;
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
        AcceptButton = _btnVerifyMfa;
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

    private Label MakeFieldLabel(string text, Point location) => new()
    {
        Text      = text,
        Location  = location,
        AutoSize  = true,
        Font      = _theme.Fonts.Small,
        ForeColor = _theme.Colors.TextSecondary,
    };

    private static void RoundPanel(Panel panel)
    {
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        };
    }
}
