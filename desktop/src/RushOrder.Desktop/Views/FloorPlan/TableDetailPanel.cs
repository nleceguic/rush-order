using RushOrder.Desktop.Models;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.FloorPlan;

internal sealed class TableDetailPanel : Panel
{
    private readonly ThemeManager _theme;
    private readonly TableService _tables;
    private TableShape? _current;

    private Label  _lblTitle    = null!;
    private Label  _lblWaiter   = null!;
    private Label  _lblTime     = null!;
    private Panel  _pnlOrders   = null!;
    private Button _btnClose    = null!;

    public event Action? CloseRequested;

    public TableDetailPanel(ThemeManager theme, TableService tables)
    {
        _theme  = theme;
        _tables = tables;
        Width     = 260;
        BackColor = theme.Colors.Surface;
        Dock      = DockStyle.Right;
        Visible   = false;
        Padding   = new Padding(0);
        Build();
    }

    private void Build()
    {
        var pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = _theme.Colors.Primary,
        };
        _lblTitle = new Label
        {
            Text      = "Mesa —",
            Font      = _theme.Fonts.Bold,
            ForeColor = Color.White,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(12, 0, 0, 0),
        };
        _btnClose = new Button
        {
            Text      = "✕",
            Size      = new Size(36, 48),
            Dock      = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font      = _theme.Fonts.SemiBold,
            Cursor    = Cursors.Hand,
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Click += (_, _) => { Visible = false; CloseRequested?.Invoke(); };
        pnlHeader.Controls.AddRange([_lblTitle, _btnClose]);

        _lblWaiter = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 28,
            Padding   = new Padding(12, 6, 0, 0),
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            BackColor = Color.Transparent,
        };
        _lblTime = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 26,
            Padding   = new Padding(12, 0, 0, 0),
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            BackColor = Color.Transparent,
        };

        var lblOrdersHdr = new Label
        {
            Text      = "PEDIDOS ACTIVOS",
            Dock      = DockStyle.Top,
            Height    = 30,
            Padding   = new Padding(12, 10, 0, 0),
            Font      = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            BackColor = Color.Transparent,
        };

        _pnlOrders = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoScroll = true,
        };

        // Left border
        Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        };

        Controls.AddRange([pnlHeader, _lblWaiter, _lblTime, lblOrdersHdr, _pnlOrders]);
    }

    public async Task ShowForTableAsync(TableShape table)
    {
        _current = table;
        _lblTitle.Text  = $"Mesa {table.Number}";
        _lblWaiter.Text = table.CurrentWaiter is { } w ? $"Camarero: {w}" : "Sin camarero asignado";
        _lblTime.Text   = table.OccupiedSince.HasValue
            ? $"Ocupada desde: {table.OccupiedSince.Value.LocalDateTime:HH:mm}  ({table.OccupancyLabel()})"
            : "";
        Visible = true;

        var orders = await _tables.GetTableOrdersAsync(table.Id);
        ShowOrders(orders);
    }

    private void ShowOrders(IReadOnlyList<ActiveOrderSummary> orders)
    {
        if (InvokeRequired) { Invoke(() => ShowOrders(orders)); return; }
        _pnlOrders.Controls.Clear();
        int y = 4;
        foreach (var o in orders)
        {
            var card = MakeOrderCard(o, y);
            _pnlOrders.Controls.Add(card);
            y += card.Height + 4;
        }
        if (orders.Count == 0)
        {
            _pnlOrders.Controls.Add(new Label
            {
                Text      = "Sin pedidos activos",
                Font      = _theme.Fonts.Small,
                ForeColor = _theme.Colors.TextSecondary,
                Location  = new Point(12, 8),
                AutoSize  = true,
            });
        }
    }

    private Panel MakeOrderCard(ActiveOrderSummary order, int y)
    {
        var statusColor = order.Status switch
        {
            "Waiting"   => _theme.Colors.Warning,
            "Preparing" => _theme.Colors.Info,
            "Ready"     => _theme.Colors.Success,
            _           => _theme.Colors.TextSecondary,
        };

        var card = new Panel
        {
            Size      = new Size(Width - 16, 64),
            Location  = new Point(8, y),
            BackColor = _theme.Colors.Background,
        };

        var lblNum = new Label
        {
            Text      = $"#{order.OrderNumber}",
            Font      = _theme.Fonts.SemiBold,
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(10, 8),
            AutoSize  = true,
        };
        var lblStatus = new Label
        {
            Text      = order.Status,
            Font      = _theme.Fonts.Small,
            ForeColor = statusColor,
            Location  = new Point(10, 28),
            AutoSize  = true,
        };
        var lblTotal = new Label
        {
            Text      = $"€ {order.Total:N2}",
            Font      = _theme.Fonts.SemiBold,
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(card.Width - 80, 8),
            Size      = new Size(70, 20),
            TextAlign = ContentAlignment.MiddleRight,
        };
        var lblItems = new Label
        {
            Text      = $"{order.ItemCount} artículos",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextSecondary,
            Location  = new Point(card.Width - 80, 28),
            Size      = new Size(70, 18),
            TextAlign = ContentAlignment.MiddleRight,
        };

        card.Controls.AddRange([lblNum, lblStatus, lblTotal, lblItems]);
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            using var b = new SolidBrush(statusColor);
            e.Graphics.FillRectangle(b, 0, 0, 3, card.Height);
        };
        return card;
    }
}
