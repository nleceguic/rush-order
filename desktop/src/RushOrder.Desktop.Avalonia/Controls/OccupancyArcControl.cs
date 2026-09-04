using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.Controls;

public sealed class OccupancyArcControl : Control
{
    public static readonly StyledProperty<int> OccupiedProperty =
        AvaloniaProperty.Register<OccupancyArcControl, int>(nameof(Occupied));
    public static readonly StyledProperty<int> TotalProperty =
        AvaloniaProperty.Register<OccupancyArcControl, int>(nameof(Total));

    public int Occupied { get => GetValue(OccupiedProperty); set => SetValue(OccupiedProperty, value); }
    public int Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    static OccupancyArcControl()
    {
        AffectsRender<OccupancyArcControl>(OccupiedProperty, TotalProperty);
    }

    public override void Render(DrawingContext context)
    {
        const double legendBand = 42;
        var ringArea = Math.Max(0, Bounds.Height - legendBand);
        var size = Math.Min(Bounds.Width, ringArea) - 16;
        if (size <= 0) return;

        var x = (Bounds.Width - size) / 2;
        var y = (ringArea - size) / 2;
        var rect = new Rect(x, y, size, size);
        var thickness = size * 0.13;

        var pct = Total > 0 ? (double)Occupied / Total : 0;
        var fillColor = pct > 0.8 ? Color.Parse("#F44336") : pct > 0.6 ? Color.Parse("#FF9800") : Color.Parse("#4CAF50");

        var inflated = rect.Deflate(thickness / 2);
        DrawArc(context, inflated, new Pen(new SolidColorBrush(Color.Parse("#28808080")), thickness,
            lineCap: PenLineCap.Round), 135, 270);
        if (Total > 0)
            DrawArc(context, inflated, new Pen(new SolidColorBrush(fillColor), thickness, lineCap: PenLineCap.Round),
                135, pct * 270);

        var typeface = new Typeface(new FontFamily("avares://RushOrder.Desktop.Avalonia/Assets/Fonts#Poppins"),
            FontStyle.Normal, FontWeight.Bold);
        var countText = new FormattedText($"{Occupied}/{Total}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, size * 0.16, Brushes.Black)
        { TextAlignment = TextAlignment.Center };
        context.DrawText(countText, new Point(x + size / 2 - countText.Width / 2, y + size * 0.3));

        var free = Math.Max(0, Total - Occupied);
        var legendY = ringArea + (legendBand - 30) / 2;
        var colWidth = Bounds.Width / 2;
        DrawLegendColumn(context, 0, colWidth, legendY, "Ocupadas", Occupied, fillColor, typeface);
        DrawLegendColumn(context, colWidth, colWidth, legendY, "Libres", free, Color.Parse("#787878"), typeface);
    }

    private static void DrawArc(DrawingContext context, Rect rect, Pen pen, double startDeg, double sweepDeg)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var center = rect.Center;
        var radiusX = rect.Width / 2;
        var radiusY = rect.Height / 2;
        var startRad = startDeg * Math.PI / 180;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180;
        var start = new Point(center.X + radiusX * Math.Cos(startRad), center.Y + radiusY * Math.Sin(startRad));
        var end = new Point(center.X + radiusX * Math.Cos(endRad), center.Y + radiusY * Math.Sin(endRad));
        ctx.BeginFigure(start, false);
        ctx.ArcTo(end, new Size(radiusX, radiusY), 0, sweepDeg > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawLegendColumn(DrawingContext context, double colX, double colWidth, double y,
        string label, int value, Color valueColor, Typeface typeface)
    {
        var valueText = new FormattedText(value.ToString(), System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(valueColor)) { TextAlignment = TextAlignment.Center };
        context.DrawText(valueText, new Point(colX + colWidth / 2 - valueText.Width / 2, y));

        var labelText = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 8, new SolidColorBrush(Color.Parse("#787878"))) { TextAlignment = TextAlignment.Center };
        context.DrawText(labelText, new Point(colX + colWidth / 2 - labelText.Width / 2, y + valueText.Height));
    }
}
