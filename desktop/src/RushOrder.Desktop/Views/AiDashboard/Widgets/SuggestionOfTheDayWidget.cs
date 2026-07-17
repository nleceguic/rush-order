using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Dashboard.Widgets;

namespace RushOrder.Desktop.Views.AiDashboard.Widgets;

// The day's highest-demand product per today's forecast — the one worth
// making sure is stocked, prepped, and featured.
internal sealed class SuggestionOfTheDayWidget : KpiWidget
{
    private Label _lblProduct = null!;
    private Label _lblDetail = null!;
    private Label _lblEmoji = null!;

    public SuggestionOfTheDayWidget(ThemeManager theme) : base(theme, "Sugerencia del día") { }

    protected override void BuildContent(Panel container)
    {
        _lblEmoji = new Label
        {
            Text = "💡",
            Font = new Font("Segoe UI Emoji", 22f),
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        _lblProduct = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Theme.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        _lblDetail = new Label
        {
            Text = "",
            Font = Theme.Fonts.Small,
            ForeColor = Theme.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        container.Controls.AddRange([_lblDetail, _lblProduct, _lblEmoji]);
    }

    public void Update(string? productName, decimal predictedQuantity)
    {
        if (InvokeRequired) { Invoke(() => Update(productName, predictedQuantity)); return; }

        if (productName is null)
        {
            _lblProduct.Text = "Sin datos suficientes";
            _lblDetail.Text = "";
            return;
        }

        _lblProduct.Text = productName;
        _lblDetail.Text = $"~{Math.Round(predictedQuantity)} unidades previstas hoy — destácalo";
    }
}
