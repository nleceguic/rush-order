using System.Drawing.Drawing2D;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.FloorPlan;

public sealed class TableFloorPlanControl : Control
{
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 2.0f;
    private const float HandleRadius = 6f;

    private readonly ThemeManager _theme;
    private readonly List<TableShape> _tables = [];

    private float   _zoom      = 1.0f;
    private PointF  _pan       = new(20, 20);
    private bool    _isPanning;
    private Point   _panStart;
    private PointF  _panOrigin;

    private TableShape? _dragging;
    private PointF      _dragOffset;
    private bool        _isDragging;

    private TableShape? _selected;
    private ResizeHandle _resizingHandle = ResizeHandle.None;
    private PointF       _resizeStart;
    private RectangleF   _resizeBounds;

    private bool _isEditMode;
    private float _pulsePhase;
    private readonly System.Windows.Forms.Timer _pulseTimer;

    // Events
    public event Action<TableShape>?        TableClicked;
    public event Action<TableShape>?        TableDoubleClicked;
    public event Action<TableShape, Point>? TableRightClicked;
    public event Action<TableShape>?        TableDragCompleted;
    public event Action?                    SelectionCleared;

    public bool IsEditMode
    {
        get => _isEditMode;
        set { _isEditMode = value; _selected = null; Invalidate(); }
    }

    public IReadOnlyList<TableShape> Tables => _tables;
    public TableShape? Selected => _selected;

    public TableFloorPlanControl(ThemeManager theme)
    {
        _theme         = theme;
        DoubleBuffered = true;
        BackColor      = theme.Colors.Background;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);

        _pulseTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _pulseTimer.Tick += (_, _) =>
        {
            _pulsePhase = (_pulsePhase + 0.15f) % MathF.Tau;
            if (_tables.Any(t => t.HasPendingOrder)) Invalidate();
        };
        _pulseTimer.Start();
    }

    public void LoadTables(IEnumerable<TableDto> dtos)
    {
        _tables.Clear();
        foreach (var d in dtos) _tables.Add(TableShape.FromDto(d));
        _selected = null;
        Invalidate();
    }

    public void AddTable(TableShape table)
    {
        _tables.Add(table);
        _selected = table;
        Invalidate();
    }

    public void RemoveSelected()
    {
        if (_selected is null) return;
        _tables.Remove(_selected);
        _selected = null;
        Invalidate();
    }

    public void UpdateTableState(Guid id, TableState state)
    {
        var table = _tables.FirstOrDefault(t => t.Id == id);
        if (table is null) return;
        table.State = state;
        if (state != TableState.Occupied) table.OccupiedSince = null;
        else if (table.OccupiedSince is null) table.OccupiedSince = DateTimeOffset.Now;
        Invalidate();
    }

    public void SetTablePendingOrder(Guid tableId, bool hasPending)
    {
        var t = _tables.FirstOrDefault(x => x.Id == tableId);
        if (t is not null) { t.HasPendingOrder = hasPending; Invalidate(); }
    }

    // ── Paint ─────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        // Apply zoom + pan transform
        g.TranslateTransform(_pan.X, _pan.Y);
        g.ScaleTransform(_zoom, _zoom);

        DrawBackground(g);
        if (_isEditMode) DrawGrid(g);
        foreach (var t in _tables) DrawTable(g, t);
        if (_isEditMode && _selected is not null) DrawResizeHandles(g, _selected);
    }

    private void DrawBackground(Graphics g)
    {
        var canvasRect = new RectangleF(-_pan.X / _zoom, -_pan.Y / _zoom,
            Width / _zoom, Height / _zoom);
        using var bg = new SolidBrush(_theme.Colors.Background);
        g.FillRectangle(bg, canvasRect);
    }

    private void DrawGrid(Graphics g)
    {
        const float step = 40f;
        var r     = new RectangleF(-_pan.X / _zoom - step, -_pan.Y / _zoom - step,
                                    Width  / _zoom + step * 2, Height / _zoom + step * 2);
        using var pen = new Pen(Color.FromArgb(30, 128, 128, 128), 0.5f);
        for (float x = MathF.Floor(r.Left / step) * step; x < r.Right; x += step)
            g.DrawLine(pen, x, r.Top, x, r.Bottom);
        for (float y = MathF.Floor(r.Top / step) * step; y < r.Bottom; y += step)
            g.DrawLine(pen, r.Left, y, r.Right, y);
    }

    private void DrawTable(Graphics g, TableShape t)
    {
        // Every table is now the same dark card color — the same Surface tone as every KPI
        // widget/panel elsewhere in the app — instead of each state getting its own saturated
        // fill. State is instead an accent: the border and the table number below use it,
        // matching how the rest of the app treats color (a dark card with a colored accent),
        // not a screen full of solid colored blobs in a palette that didn't match anything else.
        var stateColor = TableShape.GetStateColor(_theme, t.State);

        if (_zoom > 0.6f)
            DrawSoftShadow(g, t);

        using var fillBrush = new SolidBrush(_theme.Colors.Surface);
        var borderColor = t.IsSelected ? Color.White : stateColor;
        using var border = new Pen(borderColor, t.IsSelected ? 2.5f : 2f);
        DrawShape(g, fillBrush, border, t.ShapeType, t.Bounds);

        // Selected highlight ring
        if (t.IsSelected)
        {
            using var hlPen = new Pen(Color.White, 2.5f) { DashStyle = DashStyle.Dash };
            var expand = new RectangleF(t.X - 3, t.Y - 3, t.Width + 6, t.Height + 6);
            DrawShape(g, null, hlPen, t.ShapeType, expand);
        }

        // Text (hide below zoom threshold)
        if (_zoom >= 0.55f)
            DrawTableText(g, t, stateColor);

        // Pending order pulse indicator
        if (t.HasPendingOrder)
            DrawPulsingDot(g, t);
    }

    private void DrawSoftShadow(Graphics g, TableShape t)
    {
        for (var i = 3; i >= 1; i--)
        {
            var alpha  = 10 * i;
            var grow   = i * 1.5f;
            var offset = i * 1.4f;
            var rect   = new RectangleF(
                t.X - grow / 2 + offset, t.Y - grow / 2 + offset, t.Width + grow, t.Height + grow);
            using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
            DrawShape(g, shadowBrush, null, t.ShapeType, rect);
        }
    }

    private void DrawShape(Graphics g, Brush? fill, Pen? pen, TableShapeType type, RectangleF r)
    {
        if (type == TableShapeType.Circular)
        {
            if (fill is not null) g.FillEllipse(fill, r);
            if (pen  is not null) g.DrawEllipse(pen,  r);
        }
        else
        {
            using var path = GdiExtensions.CreateRoundedRect(r, 8f);
            if (fill is not null) g.FillPath(fill, path);
            if (pen  is not null) g.DrawPath(pen,  path);
        }
    }

    private void DrawTableText(Graphics g, TableShape t, Color numberColor)
    {
        using var numberBrush = new SolidBrush(numberColor);
        using var timeBrush   = new SolidBrush(_theme.Colors.TextSecondary);
        var sf    = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var numSz = 11f;
        using var numFont = PoppinsFont.New("Poppins", numSz, FontStyle.Bold);

        if (t.OccupiedSince.HasValue)
        {
            var upper = new RectangleF(t.X, t.Y, t.Width, t.Height * 0.54f);
            var lower = new RectangleF(t.X, t.Y + t.Height * 0.52f, t.Width, t.Height * 0.48f);
            g.DrawString(t.Number.ToString(), numFont, numberBrush, upper, sf);
            using var timeFont = PoppinsFont.New("Poppins", 7.5f);
            g.DrawString(t.OccupancyLabel(), timeFont, timeBrush, lower, sf);
        }
        else
        {
            g.DrawString(t.Number.ToString(), numFont, numberBrush, t.Bounds, sf);
        }
    }

    private void DrawPulsingDot(Graphics g, TableShape t)
    {
        var pulse    = (MathF.Sin(_pulsePhase) + 1f) / 2f;
        var dotSize  = 9f + pulse * 4f;
        var alpha    = (int)(150 + pulse * 105);
        var dotX     = t.X + t.Width  - dotSize / 2 - 2;
        var dotY     = t.Y - dotSize / 2 + 2;

        // Glow
        using var glow = new SolidBrush(Color.FromArgb((int)(alpha * 0.35f), 230, 57, 70));
        g.FillEllipse(glow, dotX - 3, dotY - 3, dotSize + 6, dotSize + 6);

        // Core
        using var dot = new SolidBrush(Color.FromArgb(alpha, 230, 57, 70));
        g.FillEllipse(dot, dotX, dotY, dotSize, dotSize);
    }

    private void DrawResizeHandles(Graphics g, TableShape t)
    {
        var corners = GetHandlePositions(t);
        foreach (var pt in corners)
        {
            using var fill = new SolidBrush(Color.White);
            using var pen  = new Pen(_theme.Colors.Primary, 1.5f);
            g.FillEllipse(fill, pt.X - HandleRadius, pt.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
            g.DrawEllipse(pen,  pt.X - HandleRadius, pt.Y - HandleRadius, HandleRadius * 2, HandleRadius * 2);
        }
    }

    private static PointF[] GetHandlePositions(TableShape t) =>
    [
        new(t.X,            t.Y),
        new(t.X + t.Width,  t.Y),
        new(t.X + t.Width,  t.Y + t.Height),
        new(t.X,            t.Y + t.Height),
    ];

    // ── Mouse ─────────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var canvas = ScreenToCanvas(e.Location);

        if (e.Button == MouseButtons.Middle)
        {
            _isPanning  = true;
            _panStart   = e.Location;
            _panOrigin  = _pan;
            Cursor      = Cursors.SizeAll;
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            var hit = HitTest(canvas);
            if (hit is not null) TableRightClicked?.Invoke(hit, PointToScreen(e.Location));
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            // Resize handle check (edit mode + selected)
            if (_isEditMode && _selected is not null)
            {
                var handle = HitResizeHandle(canvas, _selected);
                if (handle != ResizeHandle.None)
                {
                    _resizingHandle = handle;
                    _resizeStart    = canvas;
                    _resizeBounds   = _selected.Bounds;
                    return;
                }
            }

            var table = HitTest(canvas);
            if (table is not null)
            {
                SelectTable(table);

                if (_isEditMode)
                {
                    _dragging   = table;
                    _dragOffset = new PointF(canvas.X - table.X, canvas.Y - table.Y);
                    _isDragging = false;
                }
            }
            else
            {
                if (_selected is not null)
                {
                    _selected.IsSelected = false;
                    _selected = null;
                    SelectionCleared?.Invoke();
                    Invalidate();
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var canvas = ScreenToCanvas(e.Location);

        if (_isPanning)
        {
            _pan = new PointF(
                _panOrigin.X + e.X - _panStart.X,
                _panOrigin.Y + e.Y - _panStart.Y);
            Invalidate();
            return;
        }

        if (_resizingHandle != ResizeHandle.None && _selected is not null)
        {
            ApplyResize(canvas);
            Invalidate();
            return;
        }

        if (_dragging is not null)
        {
            _isDragging    = true;
            _dragging.X    = canvas.X - _dragOffset.X;
            _dragging.Y    = canvas.Y - _dragOffset.Y;
            _dragging.X    = MathF.Max(0, _dragging.X);
            _dragging.Y    = MathF.Max(0, _dragging.Y);
            Invalidate();
            return;
        }

        // Cursor hint
        if (_isEditMode)
        {
            var hit    = HitTest(canvas);
            var handle = _selected is not null ? HitResizeHandle(canvas, _selected) : ResizeHandle.None;
            Cursor = handle != ResizeHandle.None ? GetResizeCursor(handle)
                   : hit is not null             ? Cursors.SizeAll
                                                 : Cursors.Default;
        }
        else
        {
            Cursor = HitTest(canvas) is not null ? Cursors.Hand : Cursors.Default;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isPanning = false;
        Cursor     = Cursors.Default;

        if (_resizingHandle != ResizeHandle.None)
        {
            _resizingHandle = ResizeHandle.None;
            if (_selected is not null) TableDragCompleted?.Invoke(_selected);
            Invalidate();
            return;
        }

        if (_dragging is not null)
        {
            if (_isDragging) TableDragCompleted?.Invoke(_dragging);
            else             TableClicked?.Invoke(_dragging);
            _dragging   = null;
            _isDragging = false;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var hit = HitTest(ScreenToCanvas(e.Location));
        if (hit is not null) TableDoubleClicked?.Invoke(hit);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var canvas   = ScreenToCanvas(e.Location);
        var factor   = e.Delta > 0 ? 1.1f : 0.9f;
        var newZoom  = Math.Clamp(_zoom * factor, ZoomMin, ZoomMax);
        var zoomDiff = newZoom - _zoom;
        _zoom = newZoom;

        // Zoom toward mouse position
        _pan = new PointF(
            _pan.X - canvas.X * zoomDiff,
            _pan.Y - canvas.Y * zoomDiff);

        Invalidate();
    }

    // ── Hit testing ───────────────────────────────────────────────────────

    private TableShape? HitTest(PointF canvas)
    {
        for (int i = _tables.Count - 1; i >= 0; i--)
            if (_tables[i].HitTest(canvas)) return _tables[i];
        return null;
    }

    private ResizeHandle HitResizeHandle(PointF canvas, TableShape t)
    {
        var positions = GetHandlePositions(t);
        ResizeHandle[] handles = [ResizeHandle.NW, ResizeHandle.NE, ResizeHandle.SE, ResizeHandle.SW];
        float threshold = HandleRadius * 1.5f / _zoom;
        for (int i = 0; i < positions.Length; i++)
        {
            var dx = canvas.X - positions[i].X;
            var dy = canvas.Y - positions[i].Y;
            if (MathF.Sqrt(dx * dx + dy * dy) <= threshold) return handles[i];
        }
        return ResizeHandle.None;
    }

    private void ApplyResize(PointF canvas)
    {
        if (_selected is null) return;
        var dx = canvas.X - _resizeStart.X;
        var dy = canvas.Y - _resizeStart.Y;
        const float minSize = 40f;

        switch (_resizingHandle)
        {
            case ResizeHandle.NW:
                _selected.X      = _resizeBounds.X + dx;
                _selected.Y      = _resizeBounds.Y + dy;
                _selected.Width  = Math.Max(minSize, _resizeBounds.Width  - dx);
                _selected.Height = Math.Max(minSize, _resizeBounds.Height - dy);
                break;
            case ResizeHandle.NE:
                _selected.Y      = _resizeBounds.Y + dy;
                _selected.Width  = Math.Max(minSize, _resizeBounds.Width  + dx);
                _selected.Height = Math.Max(minSize, _resizeBounds.Height - dy);
                break;
            case ResizeHandle.SE:
                _selected.Width  = Math.Max(minSize, _resizeBounds.Width  + dx);
                _selected.Height = Math.Max(minSize, _resizeBounds.Height + dy);
                break;
            case ResizeHandle.SW:
                _selected.X      = _resizeBounds.X + dx;
                _selected.Width  = Math.Max(minSize, _resizeBounds.Width  - dx);
                _selected.Height = Math.Max(minSize, _resizeBounds.Height + dy);
                break;
        }
    }

    private static Cursor GetResizeCursor(ResizeHandle h) => h switch
    {
        ResizeHandle.NW or ResizeHandle.SE => Cursors.SizeNWSE,
        ResizeHandle.NE or ResizeHandle.SW => Cursors.SizeNESW,
        _                                  => Cursors.Default,
    };

    private void SelectTable(TableShape table)
    {
        if (_selected is not null) _selected.IsSelected = false;
        _selected            = table;
        _selected.IsSelected = true;
        Invalidate();
    }

    // ── Coordinate transforms ─────────────────────────────────────────────

    private PointF ScreenToCanvas(Point screen) =>
        new((screen.X - _pan.X) / _zoom, (screen.Y - _pan.Y) / _zoom);

    public void ResetView()
    {
        _zoom = 1.0f;
        _pan  = new PointF(20, 20);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _pulseTimer.Dispose();
        base.Dispose(disposing);
    }
}
