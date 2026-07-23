using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Dashboard.Widgets;

internal sealed class ReservationsWidget : KpiWidget
{
    private FlowLayoutPanel _flow = null!;

    public ReservationsWidget(ThemeManager theme) : base(theme, "Próximas reservas") { }

    protected override void BuildContent(Panel container)
    {
        _flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = Theme.Colors.Surface,
            Padding       = new Padding(4, 4, 4, 4),
        };
        container.Controls.Add(_flow);
    }

    public void Update(IReadOnlyList<ReservationDto> reservations)
    {
        if (InvokeRequired) { Invoke(() => Update(reservations)); return; }
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        foreach (var res in reservations.Take(3))
            _flow.Controls.Add(MakeRow(res));
        _flow.ResumeLayout();
    }

    private Panel MakeRow(ReservationDto res)
    {
        var minutesUntil = (int)(res.ReservationTime - DateTimeOffset.Now).TotalMinutes;
        var urgentColor  = minutesUntil <= 30 ? Theme.Colors.Warning : Theme.Colors.Info;

        var row = new Panel
        {
            Width     = _flow.ClientSize.Width - 8,
            Height    = 50,
            BackColor = Theme.Colors.Surface,
            Margin    = new Padding(0, 0, 0, 4),
        };

        // Time badge
        var timeBadge = new Label
        {
            Text      = res.ReservationTime.LocalDateTime.ToString("HH:mm"),
            Font      = PoppinsFont.New("Poppins", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = urgentColor,
            Size      = new Size(52, 44),
            Location  = new Point(0, 3),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblName = new Label
        {
            Text      = res.CustomerName,
            Font      = Theme.Fonts.SemiBold,
            ForeColor = Theme.Colors.TextPrimary,
            Size      = new Size(row.Width - 70, 22),
            Location  = new Point(58, 4),
            AutoEllipsis = true,
        };

        var lblPax = new Label
        {
            Text      = $"♟ {res.PartySize} personas  ·  {FormatUntil(minutesUntil)}",
            Font      = Theme.Fonts.Small,
            ForeColor = minutesUntil <= 30 ? Theme.Colors.Warning : Theme.Colors.TextSecondary,
            Size      = new Size(row.Width - 70, 18),
            Location  = new Point(58, 26),
        };

        if (!string.IsNullOrEmpty(res.Notes))
            lblName.Text = $"{res.CustomerName}  —  {res.Notes}";

        row.Controls.AddRange([timeBadge, lblName, lblPax]);

        // Separator line
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        return row;
    }

    private static string FormatUntil(int minutes)
    {
        if (minutes <= 0)    return "Ahora";
        if (minutes < 60)    return $"en {minutes} min";
        return $"en {minutes / 60}h {minutes % 60:D2}m";
    }
}
