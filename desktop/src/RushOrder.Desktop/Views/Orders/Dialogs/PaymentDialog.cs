using RushOrder.Desktop.Helpers;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders.Dialogs;

public sealed class PaymentDialog : Form
{
    private readonly ThemeManager _theme;
    private readonly OrderDto     _order;

    // Method selection
    private RadioButton _rbCash  = null!;
    private RadioButton _rbCard  = null!;
    private RadioButton _rbQr    = null!;

    // Cash fields
    private Panel       _pnlCash     = null!;
    private NumericUpDown _nudReceived = null!;
    private Label       _lblChange   = null!;

    // Split
    private TrackBar    _trkSplit   = null!;
    private Label       _lblPerPerson = null!;

    // Invoice
    private CheckBox    _chkInvoice = null!;
    private Panel       _pnlInvoice = null!;
    private TextBox     _txtCif     = null!;
    private TextBox     _txtBizName = null!;

    // Confirm
    private Button      _btnConfirm = null!;
    private Button      _btnPrint   = null!;

    public PaymentRequest? Result { get; private set; }

    public PaymentDialog(OrderDto order, ThemeManager theme)
    {
        _order = order;
        _theme = theme;

        Text            = $"Cobrar — #{order.OrderNumber}";
        Size            = new Size(500, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false; MinimizeBox = false;
        BackColor       = theme.Colors.Background;

        Build();
    }

    private void Build()
    {
        int y = 0;

        // ── Header ────────────────────────────────────────────────────────
        var pnlTop = new Panel { Size = new Size(500, 72), Location = new Point(0, 0), BackColor = _theme.Colors.SidebarBg };
        pnlTop.Controls.Add(new Label
        {
            Text = $"#{_order.OrderNumber}  —  {_order.TableName}  ·  {_order.GuestCount} pax",
            Font = _theme.Fonts.Bold, ForeColor = Color.White,
            Location = new Point(16, 12), AutoSize = true,
        });
        pnlTop.Controls.Add(new Label
        {
            Text = $"Camarero: {_order.WaiterName ?? "—"}  ·  Pedido a las {_order.PlacedAt.LocalDateTime:HH:mm}",
            Font = _theme.Fonts.Small, ForeColor = Color.FromArgb(180, 180, 180),
            Location = new Point(16, 42), AutoSize = true,
        });
        Controls.Add(pnlTop);
        y = 72;

        // ── Items summary ─────────────────────────────────────────────────
        var pnlItems = MakeCard("ARTÍCULOS", y, 140);
        y += 148;
        var lv = new ListView
        {
            Size = new Size(pnlItems.Width - 24, 96),
            Location = new Point(12, 30),
            View = View.Details, FullRowSelect = true, GridLines = false,
            BackColor = _theme.Colors.Surface, ForeColor = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.None,
        };
        lv.Columns.Add("Artículo", 240); lv.Columns.Add("Cant.", 50); lv.Columns.Add("Total", 80);
        foreach (var item in _order.Items)
        {
            var row = new ListViewItem(item.ProductName);
            row.SubItems.Add(item.Quantity.ToString());
            row.SubItems.Add($"€ {item.LineTotal:N2}");
            lv.Items.Add(row);
        }
        var lblSubtotal = MkLabel($"Subtotal: € {_order.Subtotal:N2}  |  IVA: € {_order.Tax:N2}", new Point(12, 128), false);
        var lblTotalBig = new Label
        {
            Text = $"TOTAL  € {_order.Total:N2}", Font = _theme.Fonts.Bold,
            ForeColor = _theme.Colors.Primary, AutoSize = true,
        };
        pnlItems.Controls.AddRange([lv, lblSubtotal, lblTotalBig]);
        pnlItems.Resize += (_, _) => lblTotalBig.Location = new Point(pnlItems.Width - lblTotalBig.Width - 12, 126);
        Controls.Add(pnlItems);

        // ── Payment method ────────────────────────────────────────────────
        var pnlMethod = MakeCard("MÉTODO DE PAGO", y, 54);
        y += 62;
        _rbCash = MakeRadio("💵 Efectivo", new Point(12, 24)); _rbCash.Checked = true;
        _rbCard = MakeRadio("💳 Tarjeta",  new Point(148, 24));
        _rbQr   = MakeRadio("📱 QR",       new Point(260, 24));
        _rbCash.CheckedChanged += (_, _) => { _pnlCash.Visible = _rbCash.Checked; RecalcChange(); };
        pnlMethod.Controls.AddRange([_rbCash, _rbCard, _rbQr]);
        Controls.Add(pnlMethod);

        // ── Cash fields ───────────────────────────────────────────────────
        _pnlCash = MakeCard("EFECTIVO", y, 62);
        y += 70;
        var lblRec = MkLabel("Importe recibido:", new Point(12, 28), false);
        _nudReceived = new NumericUpDown
        {
            Location    = new Point(160, 24),
            Size        = new Size(100, 28),
            Minimum     = 0, Maximum = 9999,
            DecimalPlaces = 2,
            Increment   = 0.5m,
            Value       = Math.Ceiling(_order.Total),
            Font        = _theme.Fonts.Regular,
        };
        _nudReceived.ValueChanged += (_, _) => RecalcChange();
        _lblChange = new Label
        {
            Text      = "Cambio: € 0,00",
            Font      = _theme.Fonts.SemiBold,
            ForeColor = _theme.Colors.Success,
            Location  = new Point(272, 28),
            AutoSize  = true,
        };
        _pnlCash.Controls.AddRange([lblRec, _nudReceived, _lblChange]);
        Controls.Add(_pnlCash);
        RecalcChange();

        // ── Split ─────────────────────────────────────────────────────────
        var pnlSplit = MakeCard("DIVISIÓN DE CUENTA", y, 64);
        y += 72;
        var lblSplitHdr = MkLabel("Número de partes:", new Point(12, 28), false);
        _trkSplit = new TrackBar
        {
            Location  = new Point(160, 20),
            Size      = new Size(200, 36),
            Minimum   = 1, Maximum = 10, Value = 1,
            TickStyle = TickStyle.None,
        };
        _lblPerPerson = new Label
        {
            Text      = $"€ {_order.Total:N2} / persona",
            Font      = _theme.Fonts.SemiBold,
            ForeColor = _theme.Colors.Primary,
            Location  = new Point(368, 28),
            AutoSize  = true,
        };
        _trkSplit.ValueChanged += (_, _) =>
            _lblPerPerson.Text = _trkSplit.Value == 1
                ? "Sin división"
                : $"€ {_order.Total / _trkSplit.Value:N2} / persona  ({_trkSplit.Value} partes)";
        pnlSplit.Controls.AddRange([lblSplitHdr, _trkSplit, _lblPerPerson]);
        Controls.Add(pnlSplit);

        // ── Invoice ───────────────────────────────────────────────────────
        _chkInvoice = new CheckBox
        {
            Text     = "El cliente solicita factura",
            Location = new Point(16, y + 4),
            AutoSize = true,
            Font     = _theme.Fonts.Regular,
            ForeColor = _theme.Colors.TextPrimary,
        };
        _chkInvoice.CheckedChanged += (_, _) => { _pnlInvoice.Visible = _chkInvoice.Checked; };
        Controls.Add(_chkInvoice);
        y += 28;

        _pnlInvoice = MakeCard("DATOS DE FACTURA", y, 72);
        _pnlInvoice.Visible = false;
        y += 80;
        var lblCif  = MkLabel("CIF / NIF:", new Point(12, 26), false);
        _txtCif  = new TextBox { Location = new Point(90, 22), Width = 120, PlaceholderText = "B12345678", Font = _theme.Fonts.Regular };
        var lblBiz = MkLabel("Razón social:", new Point(224, 26), false);
        _txtBizName = new TextBox { Location = new Point(310, 22), Width = 136, PlaceholderText = "Empresa S.L.", Font = _theme.Fonts.Regular };
        _pnlInvoice.Controls.AddRange([lblCif, _txtCif, lblBiz, _txtBizName]);
        Controls.Add(_pnlInvoice);

        // ── Buttons ───────────────────────────────────────────────────────
        var pnlBtns = new Panel
        {
            Size      = new Size(Width, 60),
            Location  = new Point(0, Height - 90),
            BackColor = _theme.Colors.Surface,
            Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        pnlBtns.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0);
        };

        _btnPrint = new Button
        {
            Text      = "🖨 Ticket",
            Size      = new Size(110, 38),
            Location  = new Point(16, 11),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            Font      = _theme.Fonts.Regular,
            Cursor    = Cursors.Hand,
        };
        _btnPrint.FlatAppearance.BorderColor = _theme.Colors.Border;
        _btnPrint.Click += OnPrint;

        var btnCancel = new Button
        {
            Text      = "Cancelar",
            Size      = new Size(90, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            Font      = _theme.Fonts.Regular,
            DialogResult = DialogResult.Cancel,
        };
        btnCancel.FlatAppearance.BorderColor = _theme.Colors.Border;

        _btnConfirm = new Button
        {
            Text      = "Confirmar cobro",
            Size      = new Size(150, 38),
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Bold,
            Cursor    = Cursors.Hand,
        };
        _btnConfirm.FlatAppearance.BorderSize = 0;
        _btnConfirm.Click += OnConfirm;

        pnlBtns.Controls.AddRange([_btnPrint, btnCancel, _btnConfirm]);
        pnlBtns.Resize += (_, _) =>
        {
            _btnConfirm.Location = new Point(pnlBtns.Width - 166, 11);
            btnCancel.Location   = new Point(pnlBtns.Width - 262, 11);
        };

        Controls.Add(pnlBtns);
        CancelButton = btnCancel;
    }

    private void RecalcChange()
    {
        var change = (decimal)_nudReceived.Value - _order.Total;
        _lblChange.Text      = $"Cambio: € {Math.Max(0, change):N2}";
        _lblChange.ForeColor = change >= 0 ? _theme.Colors.Success : _theme.Colors.Error;
    }

    private void OnPrint(object? sender, EventArgs e)
    {
        var method = _rbCash.Checked ? "Cash" : _rbCard.Checked ? "Card" : "Qr";
        var req    = BuildRequest(method);
        var bytes  = EscPosBuilder.BuildTicket(_order, req);
        var ok     = RawPrinterHelper.SendToDefaultPrinter(bytes, $"Ticket #{_order.OrderNumber}");
        if (!ok)
            MessageBox.Show("No se encontró impresora térmica. El ticket se ha generado en memoria.", "Impresión",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        var method = _rbCash.Checked ? "Cash" : _rbCard.Checked ? "Card" : "Qr";
        Result = BuildRequest(method);
        DialogResult = DialogResult.OK;
        Close();
    }

    private PaymentRequest BuildRequest(string method) => new(
        OrderId:        _order.Id,
        Method:         method,
        AmountTendered: _rbCash.Checked ? (decimal)_nudReceived.Value : _order.Total,
        SplitParts:     _trkSplit.Value,
        GenerateInvoice: _chkInvoice.Checked,
        CustomerCif:    _chkInvoice.Checked ? _txtCif.Text.Trim() : null);

    // ── Helpers ───────────────────────────────────────────────────────────

    private Panel MakeCard(string title, int y, int height)
    {
        var pnl = new Panel
        {
            Size      = new Size(Width - 32, height),
            Location  = new Point(16, y),
            BackColor = _theme.Colors.Surface,
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        pnl.Controls.Add(new Label
        {
            Text      = title,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            Location  = new Point(12, 8),
            AutoSize  = true,
        });
        pnl.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
        };
        return pnl;
    }

    private Label MkLabel(string text, Point loc, bool bold) => new()
    {
        Text      = text,
        Font      = bold ? _theme.Fonts.SemiBold : _theme.Fonts.Regular,
        ForeColor = _theme.Colors.TextSecondary,
        Location  = loc,
        AutoSize  = true,
    };

    private RadioButton MakeRadio(string text, Point location) => new()
    {
        Text     = text,
        Location = location,
        Font     = _theme.Fonts.Regular,
        ForeColor = _theme.Colors.TextPrimary,
        AutoSize = true,
    };
}
