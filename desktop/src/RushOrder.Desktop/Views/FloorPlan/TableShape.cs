using RushOrder.Desktop.Models;

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

    public static Color GetStateColor(TableState state) => state switch
    {
        TableState.Free      => Color.FromArgb(82,  183, 136),  // #52B788
        TableState.Occupied  => Color.FromArgb(231, 111, 81),   // #E76F51
        TableState.Reserved  => Color.FromArgb(69,  123, 157),  // #457B9D
        TableState.Cleaning  => Color.FromArgb(173, 181, 189),  // #ADB5BD
        _                    => Color.Gray,
    };

    public string OccupancyLabel()
    {
        if (OccupiedSince is null) return "";
        var elapsed = DateTime.Now - OccupiedSince.Value.LocalDateTime;
        return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}";
    }
}

public enum ResizeHandle { None, NW, NE, SE, SW }
