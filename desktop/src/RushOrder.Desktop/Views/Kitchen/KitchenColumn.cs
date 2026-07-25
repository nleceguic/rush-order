using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Kitchen;

// Same visual language as Pedidos' KanbanColumn (accent-colored header, count badge, workload
// strip under the header) so Cocina reads as the same board pattern — just hosting
// KitchenOrderCard instead of OrderCardControl, and no drag/drop: status changes here happen
// through the card's own action buttons, not by dragging between columns.
internal sealed class KitchenColumn : Panel
{
    private readonly Color _accentColor;

    private Panel            _pnlHeader   = null!;
    private Label            _lblTitle    = null!;
    private Label            _lblCount    = null!;
    private Panel            _pnlWorkload = null!;
    private FlowLayoutPanel  _flow        = null!;

    private static readonly Font TitleFont = PoppinsFont.New("Poppins", 9f,   FontStyle.Bold);
    private static readonly Font CountFont = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold);

    public OrderStatus Status { get; }
    public int CardCount => _flow.Controls.Count;
    // ClientSize (not Width) so cards are sized to the space actually left once a vertical
    // scrollbar appears — otherwise a scrolled column's cards would overhang the header bar.
    public int ContentWidth => _flow.ClientSize.Width;
    public IEnumerable<KitchenOrderCard> Cards => _flow.Controls.OfType<KitchenOrderCard>();

    public KitchenColumn(OrderStatus status, string label, Color accent, Color bgColor)
    {
        Status       = status;
        _accentColor = accent;

        Dock      = DockStyle.Fill;
        BackColor = bgColor;
        Margin    = new Padding(4, 0, 4, 0);

        Build(label, bgColor);
    }

    private void Build(string label, Color bgColor)
    {
        _pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            BackColor = Color.Transparent,
            Padding   = new Padding(10, 0, 10, 0),
        };
        _pnlHeader.Paint += PaintHeader;

        _lblCount = new Label
        {
            Text      = "0",
            Font      = CountFont,
            ForeColor = Color.White,
            BackColor = _accentColor,
            Size      = new Size(20, 18),
            Location  = new Point(4, 10),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _lblTitle = new Label
        {
            Text      = label,
            Font      = TitleFont,
            ForeColor = _accentColor,
            Location  = new Point(_lblCount.Right + 6, 10),
            AutoSize  = true,
        };
        _pnlHeader.Controls.AddRange([_lblCount, _lblTitle]);

        _pnlWorkload = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
        _pnlWorkload.Paint += PaintWorkload;

        // Zero horizontal padding — cards are given zero horizontal margin too (see
        // KitchenDisplayView.CreateCard), so their left/right edges land exactly on the
        // column's own edges, flush with the header/workload bar above instead of inset from it.
        _flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = true,
            AutoScroll    = true,
            BackColor     = bgColor,
            Padding       = new Padding(0, 4, 0, 8),
        };

        // Same reasoning as OrdersKanbanControl/MainForm (see KitchenDisplayView.BuildLayout):
        // Fill must be added first so the Top-docked header/workload strip carve their bands
        // out of the full column bounds instead of the Fill panel claiming everything first.
        Controls.AddRange([_flow, _pnlHeader, _pnlWorkload]);

        // Keep existing cards flush with the header/workload bar as the column is resized
        // (window resize, sidebar toggle) instead of only getting the right width at creation.
        _flow.Resize += (_, _) => ResizeAllCards();
    }

    private void ResizeAllCards()
    {
        var width = _flow.ClientSize.Width;
        foreach (KitchenOrderCard card in _flow.Controls.OfType<KitchenOrderCard>())
            card.Width = width;
    }

    public void AddCard(KitchenOrderCard card) => _flow.Controls.Add(card);

    public void InsertCard(KitchenOrderCard card, int index)
    {
        _flow.Controls.Add(card);
        _flow.Controls.SetChildIndex(card, index);
    }

    public void ClearCards()
    {
        _flow.SuspendLayout();
        foreach (KitchenOrderCard c in _flow.Controls.OfType<KitchenOrderCard>().ToList())
        {
            _flow.Controls.Remove(c);
            c.Dispose();
        }
        _flow.ResumeLayout(true);
    }

    public void RemoveCard(KitchenOrderCard card)
    {
        _flow.Controls.Remove(card);
        card.Dispose();
    }

    public KitchenOrderCard? FindCard(Guid orderId) =>
        Cards.FirstOrDefault(c => c.Order.OrderId == orderId);

    // max = the busiest column's card count (matches OrdersKanbanControl.UpdateWorkloadBars) —
    // the strip shows this column's share relative to whichever column is currently fullest.
    public void RefreshCount(int max)
    {
        _lblCount.Text   = CardCount.ToString();
        _pnlWorkload.Tag = Math.Max(1, max);
        _pnlWorkload.Invalidate();
    }

    private void PaintHeader(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(40, _accentColor.R, _accentColor.G, _accentColor.B), 1f);
        e.Graphics.DrawLine(pen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
    }

    private void PaintWorkload(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(30, _accentColor.R, _accentColor.G, _accentColor.B));
        var max = _pnlWorkload.Tag is int m && m > 0 ? m : 1;
        var pct = Math.Min(1f, (float)CardCount / max);
        if (pct > 0)
        {
            using var b = new SolidBrush(_accentColor);
            g.FillRectangle(b, 0, 0, (int)(_pnlWorkload.Width * pct), _pnlWorkload.Height);
        }
    }
}
