using RushOrder.Desktop.Models;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Print;

public sealed class PrinterConfigDialog : Form
{
    private readonly PrintService  _print;
    private readonly ThemeManager  _theme;

    private ComboBox _cbCaja    = null!;
    private ComboBox _cbCocina  = null!;
    private ComboBox _cbBarra   = null!;
    private TextBox  _txtName   = null!;
    private TextBox  _txtAddr   = null!;
    private TextBox  _txtCif    = null!;
    private TextBox  _txtThanks = null!;
    private TextBox  _txtQrBase = null!;
    private Label    _lblStatus = null!;

    public PrinterConfigDialog(PrintService print, ThemeManager theme)
    {
        _print = print;
        _theme = theme;

        Text            = "Configuración de Impresoras";
        Size            = new Size(540, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = theme.Colors.Background;

        Build();
        Load += OnLoad;
    }

    private void Build()
    {
        var printers = PrintService.GetAvailablePrinters();
        string[] choices = ["(ninguna)", .. printers];

        int y = 12;

        // ── Printer assignments ───────────────────────────────────────────
        AddSection("IMPRESORAS", ref y);

        _cbCaja   = AddPrinterRow("Caja (recibos):", choices, ref y);
        _cbCocina = AddPrinterRow("Cocina (comandas):", choices, ref y);
        _cbBarra  = AddPrinterRow("Barra:", choices, ref y);

        y += 4;
        AddSection("DATOS DEL RESTAURANTE", ref y);

        _txtName   = AddField("Nombre:",    ref y);
        _txtAddr   = AddField("Dirección:", ref y);
        _txtCif    = AddField("CIF/NIF:",   ref y);
        _txtThanks = AddField("Mensaje:",   ref y);
        _txtQrBase = AddField("URL recibo:",ref y);

        // ── Status label ─────────────────────────────────────────────────
        _lblStatus = new Label
        {
            AutoSize  = true,
            Location  = new Point(16, y + 8),
            ForeColor = _theme.Colors.Success,
            Font      = _theme.Fonts.Small,
        };
        Controls.Add(_lblStatus);

        // ── Bottom buttons ────────────────────────────────────────────────
        var btnSave = new Button
        {
            Text      = "Guardar",
            Size      = new Size(100, 32),
            Location  = new Point(320, 448),
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Regular,
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += OnSave;

        var btnCancel = new Button
        {
            Text      = "Cancelar",
            Size      = new Size(100, 32),
            Location  = new Point(428, 448),
            BackColor = Color.FromArgb(100, 100, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Regular,
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange([btnSave, btnCancel]);
    }

    private void AddSection(string title, ref int y)
    {
        var lbl = new Label
        {
            Text      = title,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            AutoSize  = true,
            Location  = new Point(16, y),
        };
        Controls.Add(lbl);
        y += 22;

        var sep = new Panel
        {
            Location  = new Point(16, y),
            Size      = new Size(ClientSize.Width - 32, 1),
            BackColor = Color.FromArgb(229, 229, 229),
        };
        Controls.Add(sep);
        y += 10;
    }

    private ComboBox AddPrinterRow(string label, string[] choices, ref int y)
    {
        Controls.Add(new Label
        {
            Text      = label,
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextPrimary,
            Size      = new Size(140, 22),
            Location  = new Point(16, y + 2),
        });

        var cb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size          = new Size(280, 26),
            Location      = new Point(158, y),
            Font          = _theme.Fonts.Regular,
        };
        cb.Items.AddRange(choices);
        cb.SelectedIndex = 0;
        Controls.Add(cb);

        var btnTest = new Button
        {
            Text      = "Prueba",
            Size      = new Size(70, 26),
            Location  = new Point(445, y),
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Small,
        };
        var captured = cb;
        btnTest.Click += (_, _) => TestPrint(captured);
        Controls.Add(btnTest);

        y += 36;
        return cb;
    }

    private TextBox AddField(string label, ref int y)
    {
        Controls.Add(new Label
        {
            Text      = label,
            Font      = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextPrimary,
            Size      = new Size(100, 22),
            Location  = new Point(16, y + 2),
        });

        var tb = new TextBox
        {
            Size     = new Size(395, 26),
            Location = new Point(120, y),
            Font     = _theme.Fonts.Regular,
        };
        Controls.Add(tb);
        y += 34;
        return tb;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        var cfg = _print.Config;
        SelectComboItem(_cbCaja,   cfg.CajaPrinterName);
        SelectComboItem(_cbCocina, cfg.CocinaPrinterName);
        SelectComboItem(_cbBarra,  cfg.BarraPrinterName);
        _txtName.Text   = cfg.RestaurantName;
        _txtAddr.Text   = cfg.RestaurantAddress;
        _txtCif.Text    = cfg.RestaurantCif;
        _txtThanks.Text = cfg.ThanksMessage;
        _txtQrBase.Text = cfg.ReceiptBaseUrl;
    }

    private static void SelectComboItem(ComboBox cb, string? value)
    {
        if (value is null) return;
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (string.Equals(cb.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedIndex = i;
                return;
            }
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var cfg = new PrinterConfig
        {
            CajaPrinterName   = NullIfNone(_cbCaja.SelectedItem?.ToString()),
            CocinaPrinterName = NullIfNone(_cbCocina.SelectedItem?.ToString()),
            BarraPrinterName  = NullIfNone(_cbBarra.SelectedItem?.ToString()),
            RestaurantName    = _txtName.Text.Trim(),
            RestaurantAddress = _txtAddr.Text.Trim(),
            RestaurantCif     = _txtCif.Text.Trim(),
            ThanksMessage     = _txtThanks.Text.Trim(),
            ReceiptBaseUrl    = _txtQrBase.Text.Trim(),
        };
        _print.SaveConfig(cfg);
        _lblStatus.Text = "✓ Configuración guardada";
        _lblStatus.ForeColor = _theme.Colors.Success;
    }

    private void TestPrint(ComboBox cb)
    {
        var name = NullIfNone(cb.SelectedItem?.ToString());
        if (name is null)
        {
            _lblStatus.Text      = "Seleccione una impresora primero";
            _lblStatus.ForeColor = _theme.Colors.Error;
            return;
        }
        var ok = _print.PrintTestPage(name);
        _lblStatus.Text      = ok ? $"✓ Página enviada a {name}" : "✗ Error al imprimir";
        _lblStatus.ForeColor = ok ? _theme.Colors.Success : _theme.Colors.Error;
    }

    private static string? NullIfNone(string? s) =>
        string.IsNullOrEmpty(s) || s == "(ninguna)" ? null : s;
}
