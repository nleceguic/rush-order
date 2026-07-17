using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Dashboard.Widgets;

namespace RushOrder.Desktop.Views.AiDashboard.Widgets;

internal sealed class KitchenEtaWidget : KpiWidget
{
    private Label _lblValue = null!;
    private Label _lblCaption = null!;

    public KitchenEtaWidget(ThemeManager theme) : base(theme, "ETA medio de cocina") { }

    protected override void BuildContent(Panel container)
    {
        _lblValue = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 26f, FontStyle.Bold),
            ForeColor = Theme.Colors.Primary,
            Dock = DockStyle.Top,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        _lblCaption = new Label
        {
            Text = "sin datos todavía",
            Font = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        container.Controls.AddRange([_lblCaption, _lblValue]);
    }

    public void Update(decimal? averageMinutes, int sampleSize)
    {
        if (InvokeRequired) { Invoke(() => Update(averageMinutes, sampleSize)); return; }

        if (averageMinutes is null)
        {
            _lblValue.Text = "—";
            _lblCaption.Text = "sin datos todavía";
            return;
        }

        _lblValue.Text = $"{Math.Round(averageMinutes.Value)} min";
        _lblCaption.Text = $"últimos {sampleSize} pedidos";
    }
}
