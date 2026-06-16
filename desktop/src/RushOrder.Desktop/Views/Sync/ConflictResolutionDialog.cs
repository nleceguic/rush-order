using Newtonsoft.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Sync;

public sealed class ConflictResolutionDialog : Form
{
    public ConflictResolution Resolution { get; private set; } = ConflictResolution.KeepServer;

    private static readonly Color WarnBg  = Color.FromArgb(255, 243, 205);
    private static readonly Color WarnFg  = Color.FromArgb(92, 64, 0);

    public ConflictResolutionDialog(ConflictInfo info, ThemeManager theme)
    {
        Text            = "Conflicto de sincronización";
        Size            = new Size(820, 520);
        MinimumSize     = new Size(640, 420);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = false;
        BackColor       = theme.Colors.Background;
        DoubleBuffered  = true;

        BuildUI(info, theme);
    }

    private void BuildUI(ConflictInfo info, ThemeManager theme)
    {
        // ── Warning banner ────────────────────────────────────────────────
        var pnlWarn = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 58,
            BackColor = WarnBg,
            Padding   = new Padding(16, 0, 16, 0),
        };
        var lblWarn = new Label
        {
            Text      = $"⚠  Conflicto detectado — operación: {FriendlyType(info.Operation.OperationType)}",
            Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = WarnFg,
            Dock      = DockStyle.Top,
            Height    = 32,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var lblSub = new Label
        {
            Text      = $"Endpoint: {info.Operation.HttpMethod} {info.Operation.Endpoint}  ·  Creada: {info.Operation.CreatedAt:HH:mm dd/MM}  ·  Reintentos: {info.Operation.RetryCount}",
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = WarnFg,
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        pnlWarn.Controls.AddRange([lblSub, lblWarn]);

        // ── Split panes ───────────────────────────────────────────────────
        var split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Vertical,
            SplitterDistance = 390,
            Panel1MinSize    = 200,
            Panel2MinSize    = 200,
        };
        AddJsonPane(split.Panel1, "Tu versión (local)", info.Operation.PayloadJson,
            Color.FromArgb(235, 250, 235), theme);
        AddJsonPane(split.Panel2, "Versión del servidor", info.ServerResponseBody,
            Color.FromArgb(232, 240, 250), theme);

        // ── Button row ────────────────────────────────────────────────────
        var pnlBtns = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 58,
            BackColor = theme.Colors.Surface,
            Padding   = new Padding(12, 10, 12, 10),
        };

        var btnServer = MkBtn("Mantener servidor", theme.Colors.Primary, Color.White, theme);
        btnServer.Click += (_, _) => Resolve(ConflictResolution.KeepServer);

        var btnLocal = MkBtn("Usar mi versión", Color.FromArgb(204, 120, 0), Color.White, theme);
        btnLocal.Click += (_, _) => Resolve(ConflictResolution.ApplyLocal);

        var btnDefer = MkBtn("Decidir después", Color.FromArgb(140, 140, 140), Color.White, theme);
        btnDefer.Click += (_, _) => Resolve(ConflictResolution.Defer);

        pnlBtns.Controls.AddRange([btnServer, btnLocal, btnDefer]);
        pnlBtns.Resize += (_, _) =>
        {
            const int w = 148, gap = 8;
            btnServer.Location = new Point(pnlBtns.Width - (w + gap) * 3 + gap, 10);
            btnLocal.Location  = new Point(pnlBtns.Width - (w + gap) * 2 + gap, 10);
            btnDefer.Location  = new Point(pnlBtns.Width -  w - gap,            10);
        };

        Controls.AddRange([pnlWarn, pnlBtns, split]);
    }

    private void Resolve(ConflictResolution res)
    {
        Resolution   = res;
        DialogResult = res == ConflictResolution.Defer ? DialogResult.Cancel : DialogResult.OK;
    }

    private static void AddJsonPane(Panel host, string title, string json, Color headerBg, ThemeManager theme)
    {
        var lbl = new Label
        {
            Text      = title,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = theme.Colors.TextPrimary,
            BackColor = headerBg,
            Dock      = DockStyle.Top,
            Height    = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(8, 0, 0, 0),
        };
        var txt = new TextBox
        {
            Text       = PrettyJson(json),
            Multiline  = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly   = true,
            Dock       = DockStyle.Fill,
            Font       = new Font("Consolas", 8.5f),
            BackColor  = Color.FromArgb(248, 248, 248),
            ForeColor  = Color.FromArgb(30, 30, 30),
            WordWrap   = false,
            BorderStyle= BorderStyle.None,
        };
        host.Controls.AddRange([lbl, txt]);
    }

    private static Button MkBtn(string text, Color bg, Color fg, ThemeManager theme)
    {
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(148, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = fg,
            Font      = theme.Fonts.Small,
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private static string PrettyJson(string raw)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject(raw);
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        catch { return raw; }
    }

    private static string FriendlyType(string raw) => raw switch
    {
        "CREATE_ORDER"        => "Crear pedido",
        "UPDATE_ORDER_STATUS" => "Actualizar estado de pedido",
        "CHANGE_TABLE_STATUS" => "Cambiar estado de mesa",
        "UPDATE_PRODUCT"      => "Actualizar producto",
        _                     => raw.Replace('_', ' '),
    };
}
