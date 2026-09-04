using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.Controls;

public sealed class SparklineControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<decimal>> DataProperty =
        AvaloniaProperty.Register<SparklineControl, IReadOnlyList<decimal>>(nameof(Data), []);
    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush>(nameof(LineBrush), Brushes.Gray);

    public IReadOnlyList<decimal> Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public IBrush LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(DataProperty, LineBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var data = Data;
        if (data.Count < 2) return;

        const double padH = 3, padTop = 8, padBottom = 20; // bottom reserves room for hour labels
        var w = Bounds.Width - padH * 2;
        var h = Bounds.Height - padTop - padBottom;
        if (w <= 0 || h <= 0) return;

        var min = (double)data.Min();
        var max = (double)data.Max();
        var range = Math.Max(max - min, 1);

        var points = new Point[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var nx = (double)i / (data.Count - 1);
            var ny = 1 - (((double)data[i] - min) / range);
            points[i] = new Point(padH + nx * w, padTop + ny * h * 0.9);
        }

        // Gradient fill under the line
        var fillGeometry = new StreamGeometry();
        using (var ctx = fillGeometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, padTop + h), true);
            foreach (var p in points) ctx.LineTo(p);
            ctx.LineTo(new Point(points[^1].X, padTop + h));
            ctx.EndFigure(true);
        }
        var baseColor = ((SolidColorBrush)LineBrush).Color;
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(60, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Colors.Transparent, 1),
            ],
        };
        context.DrawGeometry(fillBrush, null, fillGeometry);

        // Line
        var lineGeometry = new StreamGeometry();
        using (var ctx = lineGeometry.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++) ctx.LineTo(points[i]);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(LineBrush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), lineGeometry);

        // Dot markers
        foreach (var p in points)
            context.DrawEllipse(LineBrush, null, p, 2.5, 2.5);

        // Hour labels
        var now = DateTime.Now.Hour;
        var typeface = new Typeface(new FontFamily("avares://RushOrder.Desktop.Avalonia/Assets/Fonts#Poppins"));
        for (var i = 0; i < points.Length; i++)
        {
            var hour = ((now - 7 + i + 24) % 24).ToString("D2") + "h";
            var text = new FormattedText(hour, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, LineBrush);
            var x = Math.Clamp(points[i].X - text.Width / 2, 4, Bounds.Width - 4 - text.Width);
            context.DrawText(text, new Point(x, Bounds.Height - text.Height - 2));
        }
    }
}
