using System.Drawing.Drawing2D;

namespace RushOrder.Desktop.Theme;

internal static class GdiExtensions
{
    public static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
    {
        float d   = radius * 2;
        var path  = new GraphicsPath();
        path.AddArc(r.X,          r.Y,           d, d, 180, 90);
        path.AddArc(r.Right - d,  r.Y,           d, d, 270, 90);
        path.AddArc(r.Right - d,  r.Bottom - d,  d, d, 0,   90);
        path.AddArc(r.X,          r.Bottom - d,  d, d, 90,  90);
        path.CloseFigure();
        return path;
    }

    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF r, float radius)
    {
        using var path = CreateRoundedRect(r, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF r, float radius)
    {
        using var path = CreateRoundedRect(r, radius);
        g.DrawPath(pen, path);
    }

    public static void SetQuality(this Graphics g)
    {
        g.SmoothingMode         = SmoothingMode.AntiAlias;
        g.TextRenderingHint     = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode     = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode       = PixelOffsetMode.HighQuality;
    }

    public static double PercentChange(decimal current, decimal previous)
        => previous == 0 ? 0 : (double)((current - previous) / previous * 100);
}
