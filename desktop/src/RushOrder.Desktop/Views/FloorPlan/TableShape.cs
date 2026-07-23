using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.FloorPlan;

public sealed class TableShape
{
    public Guid         Id           { get; set; }
    public int          Number       { get; set; }
    public int          Capacity     { get; set; }
    public TableShapeType ShapeType  { get; set; }
    public TableState   State        { get; set; }
    public float        X            { get; set; }
    public float        Y            { get; set; }
    public float        Width        { get; set; }
    public float        Height       { get; set; }
    public bool         HasPendingOrder { get; set; }
    public DateTimeOffset? OccupiedSince  { get; set; }
    public string?      CurrentWaiter { get; set; }

    // Runtime-only
    public bool IsSelected { get; set; }

    public RectangleF Bounds => new(X, Y, Width, Height);

    public bool HitTest(PointF pt)
    {
        if (ShapeType == TableShapeType.Circular)
        {
            var cx = X + Width  / 2f;
            var cy = Y + Height / 2f;
            var rx = Width  / 2f;
            var ry = Height / 2f;
            var dx = (pt.X - cx) / rx;
            var dy = (pt.Y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
        return Bounds.Contains(pt);
    }

    public static TableShape FromDto(TableDto dto) => new()
    {
        Id            = dto.Id,
        Number        = dto.Number,
        Capacity      = dto.Capacity,
        ShapeType     = dto.ShapeType,
        State         = dto.State,
        X             = dto.X,
        Y             = dto.Y,
        Width         = dto.Width,
        Height        = dto.Height,
        HasPendingOrder = dto.HasPendingOrder,
        OccupiedSince  = dto.OccupiedSince,
        CurrentWaiter  = dto.CurrentWaiter,
    };

    // Pulled from the same ThemeManager palette every other screen uses (KPI widget accents,
    // alert icons, status pills) instead of a separate hardcoded hex set that happened to
    // share the same general hues but didn't actually match anything else in the app.
    public static Color GetStateColor(ThemeManager theme, TableState state) => state switch
    {
        TableState.Free      => theme.Colors.Success,
        TableState.Occupied  => theme.Colors.Primary,
        TableState.Reserved  => theme.Colors.Info,
        TableState.Cleaning  => theme.Colors.TextSecondary,
        _                    => theme.Colors.TextSecondary,
    };

    public string OccupancyLabel()
    {
        if (OccupiedSince is null) return "";
        var elapsed = DateTime.Now - OccupiedSince.Value.LocalDateTime;
        return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}";
    }
}

public enum ResizeHandle { None, NW, NE, SE, SW }
