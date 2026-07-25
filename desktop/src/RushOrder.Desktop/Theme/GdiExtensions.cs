using System.Drawing.Drawing2D;

namespace RushOrder.Desktop.Theme;

internal static class GdiExtensions
{
    public static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        => CreateRoundedRect(r, radius, radius, radius, radius);

    /// <summary>Rounds only the right-hand corners — e.g. a full-height sidebar flush against
    /// the window's left edge, where rounding the left corners would just cut a notch out of
    /// its own straight edge for no visual benefit.</summary>
    public static GraphicsPath CreateRightRoundedRect(RectangleF r, float radius)
        => CreateRoundedRect(r, 0, radius, radius, 0);

    public static GraphicsPath CreateRoundedRect(
        RectangleF r, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        // Clamp so AddArc never gets a diameter bigger than the rectangle itself — with an
        // unclamped diameter, a widget painted before its layout pass has given it a real
        // Width/Height (e.g. 0 or a couple px) throws an ArgumentException here, which can
        // leave that widget's card background/border unpainted for the rest of its lifetime.
        var maxD = Math.Min(r.Width, r.Height);
        float DClamp(float radiusVal) => Math.Max(0, Math.Min(radiusVal * 2, maxD));
        var dTL = DClamp(topLeft);
        var dTR = DClamp(topRight);
        var dBR = DClamp(bottomRight);
        var dBL = DClamp(bottomLeft);

        // GDI+ throws on a zero-width/height AddArc — a corner asked to have radius 0 (e.g.
        // the left corners of a sidebar rounded only on the right) must skip the arc entirely
        // and just run a straight line through that corner instead of drawing a degenerate one.
        //
        // Each AddLine below must end at the RADIUS offset (d.../2), not the full diameter —
        // that's where the following AddArc actually starts (e.g. the top-right arc's start
        // point is (Right - dTR/2, Y), directly above its circle's center, not (Right - dTR, Y)).
        // Using the full diameter left a gap between the declared line endpoint and the arc's
        // real start point; GDI+ silently bridges that gap with an implicit connecting segment,
        // which is invisible when it just extends the line in the same direction (true for every
        // rounded rect with a modest radius, i.e. everywhere else this method is used) but
        // reverses direction and retraces the whole edge when the two corners on a short edge
        // share radii large enough to meet — exactly a pill (radius = height/2) — producing an
        // extra stray line down that edge instead of a clean rounded end.
        var path = new GraphicsPath();
        path.AddLine(r.X + dTL / 2f, r.Y, r.Right - dTR / 2f, r.Y);
        if (dTR > 0) path.AddArc(r.Right - dTR, r.Y, dTR, dTR, 270, 90);
        path.AddLine(r.Right, r.Y + dTR / 2f, r.Right, r.Bottom - dBR / 2f);
        if (dBR > 0) path.AddArc(r.Right - dBR, r.Bottom - dBR, dBR, dBR, 0, 90);
        path.AddLine(r.Right - dBR / 2f, r.Bottom, r.X + dBL / 2f, r.Bottom);
        if (dBL > 0) path.AddArc(r.X, r.Bottom - dBL, dBL, dBL, 90, 90);
        path.AddLine(r.X, r.Bottom - dBL / 2f, r.X, r.Y + dTL / 2f);
        if (dTL > 0) path.AddArc(r.X, r.Y, dTL, dTL, 180, 90);
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
