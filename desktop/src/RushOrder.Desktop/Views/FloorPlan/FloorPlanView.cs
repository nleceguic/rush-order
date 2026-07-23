using RushOrder.Desktop.Models;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.FloorPlan;

public sealed class FloorPlanView : UserControl
{
    private readonly ThemeManager             _theme;
    private readonly TableService             _tables;
    private readonly RealTimeService          _realTime;
    private readonly ToastNotificationManager _toasts;

    private TableFloorPlanControl _canvas     = null!;
    private TableDetailPanel      _detailPanel = null!;
    private Panel                 _toolbar     = null!;
    private ToolButton             _btnEdit     = null!;
    private ToolButton             _btnAdd      = null!;
    private ToolButton             _btnDelete   = null!;
    private ToolButton             _btnSave     = null!;
    private ToolButton             _btnReset    = null!;

    public FloorPlanView(
        ThemeManager theme, TableService tables,
        RealTimeService realTime, ToastNotificationManager toasts)
    {
        _theme    = theme;
        _tables   = tables;
        _realTime = realTime;
        _toasts   = toasts;

        Dock           = DockStyle.Fill;
        BackColor      = theme.Colors.Background;
        DoubleBuffered = true;

        BuildLayout();
        WireRealTime();
        Load += async (_, _) => await LoadTablesAsync();
    }

    private void BuildLayout()
    {
        // ── Toolbar (top) ─────────────────────────────────────────────────
        _toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(16, 6, 8, 6),
        };

        _btnEdit = MakeToolButton("✏ Modo edición");
        _btnEdit.Click += (_, _) => ToggleEditMode();

        _btnAdd = MakeToolButton("+ Mesa");
        _btnAdd.Enabled = false;
        _btnAdd.Click   += (_, _) => AddTable();

        _btnDelete = MakeToolButton("✕ Eliminar");
        _btnDelete.Enabled = false;
        _btnDelete.Click += (_, _) => DeleteSelected();

        _btnSave = MakeToolButton("💾 Guardar");
        _btnSave.Enabled = false;
        _btnSave.Click   += async (_, _) => await SaveLayoutAsync();

        _btnReset = MakeToolButton("⟳ Resetear vista");
        _btnReset.Click += (_, _) => _canvas.ResetView();

        // _toolbar.Padding.Left only affects auto-laid-out children — these are positioned via
        // explicit Location, so it was silently ignored and the row sat flush against the
        // toolbar's left edge. Start x from the padding value instead.
        // Toolbar is 44 tall, buttons are 32 — (44-32)/2 = 6 centers them; 4 sat 2px high.
        int x = _toolbar.Padding.Left;
        foreach (var btn in new[] { _btnEdit, _btnAdd, _btnDelete, _btnSave, _btnReset })
        {
            btn.Location = new Point(x, 6);
            x += btn.Width + 6;
        }
        _toolbar.Controls.AddRange([_btnEdit, _btnAdd, _btnDelete, _btnSave, _btnReset]);

        // ── Detail panel (right) ──────────────────────────────────────────
        _detailPanel = new TableDetailPanel(_theme, _tables);
        _detailPanel.CloseRequested += () => _canvas.SelectionCleared -= null;

        // ── Canvas ────────────────────────────────────────────────────────
        _canvas = new TableFloorPlanControl(_theme) { Dock = DockStyle.Fill };
        _canvas.TableClicked       += OnTableClicked;
        _canvas.TableDoubleClicked += OnTableDoubleClicked;
        _canvas.TableRightClicked  += OnTableRightClicked;
        _canvas.TableDragCompleted += _ => UpdateDeleteButton();
        _canvas.SelectionCleared   += UpdateDeleteButton;

        Controls.AddRange([_toolbar, _detailPanel, _canvas]);
    }

    private void WireRealTime()
    {
        _realTime.TableStatusChanged += async (tableId, status) =>
        {
            if (!Guid.TryParse(tableId, out var id)) return;
            var state = Enum.TryParse<TableState>(status, out var s) ? s : TableState.Free;
            _canvas.UpdateTableState(id, state);
            await Task.CompletedTask;
        };

        _realTime.OrderReceived += payload =>
        {
            _canvas.SetTablePendingOrder(payload.TableId, true);
            return Task.CompletedTask;
        };

        _realTime.OrderStatusUpdated += (_, status, _) =>
        {
            if (status is "Delivered" or "Cancelled")
            {
                // Re-load to get accurate pending state
                _ = LoadTablesAsync();
            }
            return Task.CompletedTask;
        };
    }

    private async Task LoadTablesAsync()
    {
        var dtos = await _tables.GetTablesAsync();
        if (InvokeRequired) Invoke(() => _canvas.LoadTables(dtos));
        else _canvas.LoadTables(dtos);
    }

    // ── Edit mode ─────────────────────────────────────────────────────────

    private void ToggleEditMode()
    {
        var entering      = !_canvas.IsEditMode;
        _canvas.IsEditMode = entering;
        _btnEdit.Text     = entering ? "✓ Salir edición" : "✏ Modo edición";
        _btnAdd.Enabled    = entering;
        _btnSave.Enabled   = entering;
        UpdateDeleteButton();
    }

    private void AddTable()
    {
        using var dlg = new AddTableDialog(_theme);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var centerX = (_canvas.Width  / 2f - 20) / 1.0f;
        var centerY = (_canvas.Height / 2f - 20) / 1.0f;

        var table = new TableShape
        {
            Id        = Guid.NewGuid(),
            Number    = dlg.TableNumber,
            Capacity  = dlg.Capacity,
            ShapeType = dlg.Capacity <= 4 ? TableShapeType.Circular : TableShapeType.Rectangular,
            State     = TableState.Free,
            X         = centerX - 50,
            Y         = centerY - 50,
            Width     = dlg.Capacity <= 4 ? 90f : 130f,
            Height    = dlg.Capacity <= 4 ? 90f : 80f,
        };
        _canvas.AddTable(table);
        UpdateDeleteButton();
    }

    private void DeleteSelected()
    {
        if (_canvas.Selected is null) return;
        var msg = $"¿Eliminar Mesa {_canvas.Selected.Number}?";
        if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            == DialogResult.Yes)
        {
            _canvas.RemoveSelected();
            UpdateDeleteButton();
        }
    }

    private async Task SaveLayoutAsync()
    {
        _btnSave.Enabled = false;
        _btnSave.Text    = "Guardando…";
        try
        {
            var positions = _canvas.Tables.Select(t => new SavePositionRequest(t.Id, t.X, t.Y));
            await _tables.SavePositionsAsync(positions);
            _toasts.Show("Disposición guardada correctamente", ToastType.Success);
        }
        catch
        {
            _toasts.Show("Error al guardar la disposición", ToastType.Error);
        }
        finally
        {
            _btnSave.Enabled = true;
            _btnSave.Text    = "💾 Guardar";
        }
    }

    private void UpdateDeleteButton() =>
        _btnDelete.Enabled = _canvas.IsEditMode && _canvas.Selected is not null;

    // ── Table interactions ────────────────────────────────────────────────

    private async void OnTableClicked(TableShape table)
    {
        _detailPanel.Visible = false;
        UpdateDeleteButton();

        if (!_canvas.IsEditMode)
        {
            if (table.State == TableState.Occupied)
            {
                await _detailPanel.ShowForTableAsync(table);
            }
            else if (table.State == TableState.Free)
            {
                ShowOpenTableMenu(table);
            }
        }
    }

    private void OnTableDoubleClicked(TableShape table)
    {
        if (_canvas.IsEditMode) return;
        // Navigate to table orders — raise event for MainForm to handle
        MessageBox.Show($"Navegando a pedidos de Mesa {table.Number}", "Rush Order",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnTableRightClicked(TableShape table, Point screenPoint)
    {
        var menu = new ContextMenuStrip { Font = _theme.Fonts.Regular };

        var stateMenu = new ToolStripMenuItem($"Mesa {table.Number}  [{table.State}]");
        stateMenu.Enabled = false;
        menu.Items.Add(stateMenu);
        menu.Items.Add(new ToolStripSeparator());

        var subState = new ToolStripMenuItem("Cambiar estado");
        subState.DropDownItems.Add("🟢 Libre",        null, (_, _) => SetState(table, TableState.Free));
        subState.DropDownItems.Add("🟠 Ocupada",       null, (_, _) => SetState(table, TableState.Occupied));
        subState.DropDownItems.Add("🔵 Reservada",    null, (_, _) => SetState(table, TableState.Reserved));
        subState.DropDownItems.Add("⬜ En limpieza",  null, (_, _) => SetState(table, TableState.Cleaning));
        menu.Items.Add(subState);

        menu.Items.Add("Ver pedidos",      null, async (_, _) => await _detailPanel.ShowForTableAsync(table));
        menu.Items.Add("Cambiar camarero", null, (_, _) => ChangeWaiter(table));

        if (_canvas.IsEditMode)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Eliminar mesa", null, (_, _) => { _canvas.RemoveSelected(); UpdateDeleteButton(); });
        }

        menu.Show(screenPoint);
    }

    private void ShowOpenTableMenu(TableShape table)
    {
        var menu = new ContextMenuStrip { Font = _theme.Fonts.Regular };
        menu.Items.Add($"Mesa {table.Number} — Libre", null, null).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir mesa (Ocupada)",  null, (_, _) => SetState(table, TableState.Occupied));
        menu.Items.Add("Ver reserva",           null, (_, _) => { });
        menu.Show(Cursor.Position);
    }

    private void ChangeWaiter(TableShape table)
    {
        using var dlg = new WaiterInputDialog(_theme, table.CurrentWaiter);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            table.CurrentWaiter = dlg.WaiterName;
            _canvas.Invalidate();
        }
    }

    private void SetState(TableShape table, TableState state)
    {
        table.State = state;
        if (state == TableState.Occupied) table.OccupiedSince = DateTimeOffset.Now;
        else table.OccupiedSince = null;
        _canvas.Invalidate();
        _ = _tables.UpdateTableStateAsync(table.Id, state);
    }

    // Red (the app's usual accent) while clickable, transparent while disabled, no border,
    // white text always.
    private ToolButton MakeToolButton(string text) => new(_theme, text);
}

/// <summary>Custom-painted toolbar button — replaces a plain Button whose rounded corners were
/// done via Control.Region, which clips with a hard, non-anti-aliased pixel mask (no partial/
/// blended edge pixels at all). No radius fixed that; drawing the rounded fill directly with
/// Graphics.SmoothingMode.AntiAlias (the same technique every other rounded shape in this app
/// already uses) does.</summary>
internal sealed class ToolButton : Control
{
    private readonly ThemeManager _theme;

    public ToolButton(ThemeManager theme, string text)
    {
        _theme = theme;
        Text   = text;
        Height = 32;
        Font   = theme.Fonts.Small;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);

        var textSize = TextRenderer.MeasureText(text, Font);
        Width = textSize.Width + 28;

        EnabledChanged += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        // OnPaintBackground is suppressed below, so this must paint its own fill — matches the
        // toolbar's own background exactly, so a disabled (unfilled) button blends in seamlessly.
        using (var bg = new SolidBrush(_theme.Colors.Surface))
            g.FillRectangle(bg, ClientRectangle);

        if (Enabled)
        {
            // Radius = half the height → fully rounded (pill) ends, not just rounded corners.
            using var fillBrush = new SolidBrush(_theme.Colors.Primary);
            g.FillRoundedRectangle(fillBrush, new RectangleF(0, 0, Width, Height), Height / 2f);
        }

        using var textBrush = new SolidBrush(Color.White);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), format);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
}

// ── Auxiliary dialogs ─────────────────────────────────────────────────────────

internal sealed class AddTableDialog : Form
{
    private readonly NumericUpDown _nudNumber;
    private readonly NumericUpDown _nudCapacity;

    public int TableNumber => (int)_nudNumber.Value;
    public int Capacity    => (int)_nudCapacity.Value;

    public AddTableDialog(ThemeManager theme)
    {
        Text            = "Añadir mesa";
        Size            = new Size(300, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false; MinimizeBox = false;
        BackColor       = theme.Colors.Background;

        var lblNum = new Label { Text = "Número de mesa:", Location = new Point(20, 20), AutoSize = true };
        _nudNumber  = new NumericUpDown { Location = new Point(160, 16), Width = 80, Minimum = 1, Maximum = 99, Value = 1 };

        var lblCap = new Label { Text = "Capacidad (personas):", Location = new Point(20, 56), AutoSize = true };
        _nudCapacity = new NumericUpDown { Location = new Point(160, 52), Width = 80, Minimum = 1, Maximum = 20, Value = 4 };

        var btnOk     = new Button { Text = "Añadir", DialogResult = DialogResult.OK,  Location = new Point(120, 110), Width = 70 };
        var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(200, 110), Width = 70 };
        AcceptButton = btnOk; CancelButton = btnCancel;
        Controls.AddRange([lblNum, _nudNumber, lblCap, _nudCapacity, btnOk, btnCancel]);
    }
}

internal sealed class WaiterInputDialog : Form
{
    private readonly TextBox _txt;
    public string WaiterName => _txt.Text.Trim();

    public WaiterInputDialog(ThemeManager theme, string? current)
    {
        Text            = "Cambiar camarero";
        Size            = new Size(300, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false; MinimizeBox = false;
        BackColor       = theme.Colors.Background;

        var lbl = new Label { Text = "Nombre del camarero:", Location = new Point(20, 20), AutoSize = true };
        _txt = new TextBox { Location = new Point(20, 44), Width = 240, Text = current ?? "" };
        var btnOk     = new Button { Text = "Guardar",   DialogResult = DialogResult.OK,     Location = new Point(110, 80), Width = 70 };
        var btnCancel = new Button { Text = "Cancelar",  DialogResult = DialogResult.Cancel,  Location = new Point(190, 80), Width = 70 };
        AcceptButton = btnOk; CancelButton = btnCancel;
        Controls.AddRange([lbl, _txt, btnOk, btnCancel]);
    }
}
