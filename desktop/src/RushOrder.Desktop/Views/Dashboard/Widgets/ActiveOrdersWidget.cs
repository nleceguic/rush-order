using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class ActiveOrdersWidget : KpiWidget
{
    private Label _lblTotal    = null!;
    private Label _lblWaiting  = null!;
    private Label _lblPreparing = null!;
    private Label _lblReady    = null!;

    public ActiveOrdersWidget(ThemeManager theme) : base(theme, "Pedidos activos") { }

    protected override void BuildContent(Panel container)
    {
        _lblTotal = new Label
        {
            Text      = "0",
            Font      = new Font("Segoe UI", 36f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 60,
            TextAlign = ContentAlignment.BottomLeft,
            Padding   = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
        };

        var pnlPills = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 34,
            BackColor = Color.Transparent,
            Padding   = new Padding(8, 6, 8, 0),
        };

        _lblWaiting  = MakePill("0 En espera",    Color.FromArgb(255, 152, 0));
        _lblPreparing = MakePill("0 Preparando",  Color.FromArgb(33, 150, 243));
        _lblReady    = MakePill("0 Listos",        Color.FromArgb(76, 175, 80));

        _lblWaiting.Location   = new Point(0, 4);
        _lblPreparing.Location = new Point(_lblWaiting.Width + 4, 4);
        _lblReady.Location     = new Point(_lblWaiting.Width + _lblPreparing.Width + 8, 4);

        pnlPills.Controls.AddRange([_lblWaiting, _lblPreparing, _lblReady]);
        container.Controls.AddRange([_lblTotal, pnlPills]);
    }

    public void Update(int waiting, int preparing, int ready)
    {
        if (InvokeRequired) { Invoke(() => Update(waiting, preparing, ready)); return; }
        var total         = waiting + preparing + ready;
        _lblTotal.Text    = total.ToString();
        _lblWaiting.Text  = $"{waiting} En espera";
        _lblPreparing.Text = $"{preparing} Preparando";
        _lblReady.Text    = $"{ready} Listos";
    }

    private static Label MakePill(string text, Color color) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
        ForeColor = color,
        BackColor = Color.FromArgb(30, color.R, color.G, color.B),
        AutoSize  = false,
        Width     = 84,
        Height    = 20,
        TextAlign = ContentAlignment.MiddleCenter,
        Padding   = new Padding(4, 0, 4, 0),
    };
}
