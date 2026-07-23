using Microsoft.Win32;

namespace RushOrder.Desktop.Theme;

public sealed class ThemeManager
{
    public static ThemeManager Instance { get; private set; } = null!;
    internal static void Initialize(ThemeManager instance) => Instance = instance;

    public bool IsDarkMode { get; private set; }
    public ColorPalette Colors { get; private set; }
    public FontPalette Fonts { get; } = new();

    public ThemeManager()
    {
        IsDarkMode = DetectSystemTheme();
        Colors = IsDarkMode ? ColorPalette.Dark() : ColorPalette.Light();
    }

    public static bool DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int i && i == 0;
        }
        catch { return false; }
    }

    public void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        Colors = IsDarkMode ? ColorPalette.Dark() : ColorPalette.Light();
    }

    public void ApplyTheme(Control root)
    {
        ApplyToControl(root);
        foreach (Control child in root.Controls)
            ApplyTheme(child);
    }

    private void ApplyToControl(Control c)
    {
        c.Font = Fonts.Regular;
        switch (c)
        {
            case Form f:
                f.BackColor = Colors.Background;
                f.ForeColor = Colors.TextPrimary;
                break;
            case Button btn when btn.Tag is "primary":
                btn.BackColor = Colors.Primary;
                btn.ForeColor = Color.White;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.Cursor = Cursors.Hand;
                break;
            case Button btn:
                btn.BackColor = Colors.Surface;
                btn.ForeColor = Colors.TextPrimary;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Colors.Border;
                btn.Cursor = Cursors.Hand;
                break;
            case TextBox tb:
                tb.BackColor = Colors.Input;
                tb.ForeColor = Colors.TextPrimary;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Label lbl:
                lbl.ForeColor = Colors.TextPrimary;
                lbl.BackColor = Color.Transparent;
                break;
            case Panel pnl when pnl.Tag is "surface":
                pnl.BackColor = Colors.Surface;
                break;
        }
    }
}

public sealed class ColorPalette
{
    public Color Primary      { get; init; } = Color.FromArgb(230, 57, 70);
    public Color PrimaryHover { get; init; } = Color.FromArgb(200, 40, 55);
    public Color Background   { get; init; }
    public Color Surface      { get; init; }
    public Color SidebarBg    { get; init; }
    public Color HeaderBg     { get; init; }
    public Color TextPrimary  { get; init; }
    public Color TextSecondary { get; init; }
    public Color Border       { get; init; }
    public Color Input        { get; init; }
    public Color NavHover     { get; init; }
    public Color NavActive    { get; init; }

    public Color Success { get; init; } = Color.FromArgb(76, 175, 80);
    public Color Warning { get; init; } = Color.FromArgb(255, 152, 0);
    public Color Error   { get; init; } = Color.FromArgb(244, 67, 54);
    public Color Info    { get; init; } = Color.FromArgb(33, 150, 243);

    public static ColorPalette Light() => new()
    {
        Background    = Color.FromArgb(248, 248, 248),
        Surface       = Color.White,
        SidebarBg     = Color.FromArgb(29, 29, 29),
        HeaderBg      = Color.White,
        TextPrimary   = Color.FromArgb(29, 29, 29),
        TextSecondary = Color.FromArgb(120, 120, 120),
        Border        = Color.FromArgb(229, 229, 229),
        Input         = Color.White,
        NavHover      = Color.FromArgb(50, 255, 255, 255),
        NavActive     = Color.FromArgb(230, 57, 70),
    };

    public static ColorPalette Dark() => new()
    {
        Background    = Color.FromArgb(18, 18, 18),
        Surface       = Color.FromArgb(29, 29, 29),
        SidebarBg     = Color.FromArgb(15, 15, 15),
        HeaderBg      = Color.FromArgb(29, 29, 29),
        TextPrimary   = Color.FromArgb(240, 240, 240),
        TextSecondary = Color.FromArgb(160, 160, 160),
        Border        = Color.FromArgb(50, 50, 50),
        Input         = Color.FromArgb(40, 40, 40),
        NavHover      = Color.FromArgb(50, 255, 255, 255),
        NavActive     = Color.FromArgb(230, 57, 70),
    };
}

public sealed class FontPalette : IDisposable
{
    public Font Regular  { get; } = new(PoppinsFont.Regular, 9f,  FontStyle.Regular);
    public Font Medium   { get; } = new(PoppinsFont.Medium, 9f,  FontStyle.Regular);
    public Font SemiBold { get; } = new(PoppinsFont.SemiBold, 9f,  FontStyle.Regular);
    public Font Bold     { get; } = new(PoppinsFont.Regular, 11f, FontStyle.Bold);
    public Font Small    { get; } = new(PoppinsFont.Regular, 8f,  FontStyle.Regular);
    public Font Title    { get; } = new(PoppinsFont.Regular, 16f, FontStyle.Bold);
    public Font NavItem  { get; } = new(PoppinsFont.Medium, 9f,  FontStyle.Regular);
    public Font Avatar   { get; } = new(PoppinsFont.SemiBold, 11f, FontStyle.Regular);

    public void Dispose()
    {
        Regular.Dispose(); Medium.Dispose(); SemiBold.Dispose(); Bold.Dispose();
        Small.Dispose(); Title.Dispose(); NavItem.Dispose(); Avatar.Dispose();
    }
}
