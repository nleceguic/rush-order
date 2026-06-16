using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class AvgTicketWidget : KpiWidget
{
    private Label _lblValue = null!;
    private Label _lblDelta = null!;
    private Label _lblSub   = null!;

    public AvgTicketWidget(ThemeManager theme) : base(theme, "Ticket medio") { }

    protected override void BuildContent(Panel container)
    {
        _lblValue = new Label
        {
            Text      = "€ 0,00",
            Font      = new Font("Segoe UI", 26f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 52,
            TextAlign = ContentAlignment.BottomLeft,
            Padding   = new Padding(8, 0, 0, 0),
            BackColor = Color.Transparent,
        };
        _lblDelta = new Label
        {
            Text      = "",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 22,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
        };
        _lblSub = new Label
        {
            Text      = "por comensal",
            Font      = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock      = DockStyle.Top,
            Height    = 20,
            Padding   = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
        };
        container.Controls.AddRange([_lblValue, _lblSub, _lblDelta]);
    }

    public void Update(decimal today, decimal yesterday)
    {
        if (InvokeRequired) { Invoke(() => Update(today, yesterday)); return; }
        _lblValue.Text = $"€ {today:N2}";
        var pct  = GdiExtensions.PercentChange(today, yesterday);
        var up   = today >= yesterday;
        _lblDelta.ForeColor = up ? Theme.Colors.Success : Theme.Colors.Error;
        _lblDelta.Text = $"{(up ? "▲" : "▼")} {Math.Abs(pct):F1}% vs. ayer (€ {yesterday:N2})";
    }
}
