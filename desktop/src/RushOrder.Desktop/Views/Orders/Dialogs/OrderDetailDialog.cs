using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders.Dialogs;

public sealed class OrderDetailDialog : Form
{
    private readonly ThemeManager _theme;
    private OrderDto _order;

    public OrderDetailDialog(OrderDto order, ThemeManager theme)
    {
        _order = order;
        _theme = theme;

        Text            = $"Pedido #{order.OrderNumber}";
        Size            = new Size(520, 600);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false; MinimizeBox = false;
        BackColor       = theme.Colors.Background;

        Build();
    }

    private void Build()
    {
        // Header
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = _theme.Colors.SidebarBg };
        var lblNum  = new Label
        {
            Text      = $"#{_order.OrderNumber}",
            Font      = _theme.Fonts.Title,
            ForeColor = Color.White,
            Location  = new Point(16, 12),
            AutoSize  = true,
        };
        var lblMeta = new Label
        {
            Text      = $"{_order.TableName}  ·  {_order.GuestCount} pax  ·  {_order.PlacedAt.LocalDateTime:HH:mm}  ·  Camarero: {_order.WaiterName ?? "—"}",
            Font      = _theme.Fonts.Small,
            ForeColor = Color.FromArgb(180, 180, 180),
            Location  = new Point(16, 46),
            AutoSize  = true,
        };
        var lblStatus = new Label
        {
            Text      = StatusLabel(_order.Status),
            Font      = _theme.Fonts.SemiBold,
            ForeColor = Color.White,
            BackColor = StatusColor(_order.Status),
            AutoSize  = false,
            Size      = new Size(110, 26),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        pnlTop.Controls.AddRange([lblNum, lblMeta, lblStatus]);
        pnlTop.Resize += (_, _) => lblStatus.Location = new Point(pnlTop.Width - 122, 22);

        // Items list
        var lblItemsHdr = new Label
        {
            Text    = "ARTÍCULOS",
            Dock    = DockStyle.Top,
            Height  = 30,
            Font    = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            Padding = new Padding(16, 10, 0, 0),
            BackColor = Color.Transparent,
        };

        var listView = new ListView
        {
            Dock      = DockStyle.Fill,
            View      = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.None,
        };
        listView.Columns.Add("Artículo",  240);
        listView.Columns.Add("Cant.",  50);
        listView.Columns.Add("Precio", 70);
        listView.Columns.Add("Total",  70);
        listView.Columns.Add("Notas",  0);

        foreach (var item in _order.Items)
        {
            var row = new ListViewItem(item.ProductName);
            row.SubItems.Add(item.Quantity.ToString());
            row.SubItems.Add($"€ {item.UnitPrice:N2}");
            row.SubItems.Add($"€ {item.LineTotal:N2}");
            row.SubItems.Add(item.Notes ?? "");
            if (item.Allergens.Count > 0)
                row.ForeColor = _theme.Colors.Warning;
            listView.Items.Add(row);
        }

        // Totals panel
        var pnlTotals = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = _theme.Colors.Surface };
        pnlTotals.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, pnlTotals.Width, 0);
        };

        void AddTotalRow(string label, string value, int y, bool bold = false)
        {
            var lbl = new Label { Text = label, Font = bold ? _theme.Fonts.Bold : _theme.Fonts.Regular, ForeColor = _theme.Colors.TextSecondary, Location = new Point(16, y), AutoSize = true };
            var val = new Label { Text = value, Font = bold ? _theme.Fonts.Bold : _theme.Fonts.Regular, ForeColor = _theme.Colors.TextPrimary, AutoSize = true };
            pnlTotals.Controls.AddRange([lbl, val]);
            pnlTotals.Resize += (_, _) => val.Location = new Point(pnlTotals.Width - val.Width - 16, y);
        }

        AddTotalRow("Subtotal:", $"€ {_order.Subtotal:N2}", 10);
        AddTotalRow("IVA (10%):", $"€ {_order.Tax:N2}", 34);
        AddTotalRow("TOTAL:", $"€ {_order.Total:N2}", 62, bold: true);

        // Notes
        Panel? pnlNotes = null;
        if (!string.IsNullOrEmpty(_order.Notes))
        {
            pnlNotes = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(254, 249, 195), Padding = new Padding(12, 8, 12, 0) };
            pnlNotes.Controls.Add(new Label { Text = $"📝 {_order.Notes}", Font = _theme.Fonts.Small, ForeColor = Color.FromArgb(120, 80, 0), Dock = DockStyle.Fill });
        }

        // Close button
        var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = _theme.Colors.Background, Padding = new Padding(12, 9, 12, 9) };
        var btnClose   = new Button
        {
            Text      = "Cerrar",
            Width     = 100,
            Dock      = DockStyle.Right,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Regular,
            DialogResult = DialogResult.Cancel,
        };
        btnClose.FlatAppearance.BorderColor = _theme.Colors.Border;
        pnlButtons.Controls.Add(btnClose);

        if (pnlNotes is not null) Controls.Add(pnlNotes);
        Controls.AddRange([pnlTop, lblItemsHdr, listView, pnlTotals, pnlButtons]);
        CancelButton = btnClose;
    }

    private static string StatusLabel(OrderStatus s) => s switch
    {
        OrderStatus.New       => "NUEVO",
        OrderStatus.Preparing => "EN PREPARACIÓN",
        OrderStatus.Ready     => "LISTO",
        OrderStatus.Served    => "SERVIDO",
        OrderStatus.Paid      => "COBRADO",
        _                     => s.ToString(),
    };

    private static Color StatusColor(OrderStatus s) => s switch
    {
        OrderStatus.New       => Color.FromArgb(59, 130, 246),
        OrderStatus.Preparing => Color.FromArgb(245, 158, 11),
        OrderStatus.Ready     => Color.FromArgb(34, 197, 94),
        OrderStatus.Served    => Color.FromArgb(148, 163, 184),
        OrderStatus.Paid      => Color.FromArgb(21, 128, 61),
        _                     => Color.Gray,
    };
}
