using RushOrder.Desktop.Models;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Menu;

public sealed class MenuManagementControl : UserControl
{
    private readonly ThemeManager             _theme;
    private readonly MenuService              _menu;
    private readonly AppState                 _state;
    private readonly ToastNotificationManager _toasts;

    // Layout
    private SplitContainer     _split     = null!;
    private TreeView           _tree      = null!;
    private DataGridView       _grid      = null!;
    private TextBox            _txtSearch = null!;
    private CheckBox           _chkAvail  = null!;
    private CheckBox           _chkLow    = null!;
    private ComboBox           _cmbAllergen = null!;

    // State
    private IReadOnlyList<CategoryDto>    _categories = [];
    private IReadOnlyList<MenuProductDto> _products   = [];
    private Guid?  _selectedCategory;
    private string _searchText   = "";
    private bool   _filterAvail  = false;
    private bool   _filterLow    = false;
    private string? _filterAllergen = null;

    // Image cache (URL → Bitmap)
    private readonly Dictionary<string, Image> _imgCache = [];

    // Timer for search debounce
    private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 300 };

    // Column indices
    private const int ColThumb    = 0;
    private const int ColName     = 1;
    private const int ColCategory = 2;
    private const int ColPrice    = 3;
    private const int ColStatus   = 4;
    private const int ColStock    = 5;
    private const int ColEdit     = 6;
    private const int ColDup      = 7;
    private const int ColDel      = 8;

    // Category node colors
    private static readonly Color[] CatColors =
    [
        Color.FromArgb(59,  130, 246),
        Color.FromArgb(16,  185, 129),
        Color.FromArgb(245, 158,  11),
        Color.FromArgb(239,  68,  68),
        Color.FromArgb(139,  92, 246),
        Color.FromArgb(236,  72, 153),
    ];

    public MenuManagementControl(ThemeManager theme, MenuService menu,
        AppState state, ToastNotificationManager toasts)
    {
        _theme  = theme;
        _menu   = menu;
        _state  = state;
        _toasts = toasts;

        Dock  = DockStyle.Fill;
        DoubleBuffered = true;

        BuildLayout();
        _searchDebounce.Tick += async (_, _) =>
        {
            _searchDebounce.Stop();
            await RefreshGridAsync();
        };

        Load += async (_, _) => await LoadAllAsync();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        _split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            SplitterWidth    = 4,
            SplitterDistance = 260,
            Panel1MinSize    = 180,
            Panel2MinSize    = 400,
            BackColor        = _theme.Colors.Border,
        };

        BuildLeftPanel();
        BuildRightPanel();
        Controls.Add(_split);
    }

    private void BuildLeftPanel()
    {
        _split.Panel1.BackColor = _theme.Colors.Surface;

        // ── Left toolbar ───────────────────────────────────────────────────
        var pnlToolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(6, 6, 6, 0),
        };
        pnlToolbar.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.Colors.Border);
            e.Graphics.DrawLine(p, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
        };

        var btnAddCat = MakeIconButton("+ Categoría", _theme.Colors.Primary, Color.White, 110);
        btnAddCat.Click += OnAddCategoryClicked;

        var btnDel = MakeIconButton("✕ Eliminar", _theme.Colors.Surface, _theme.Colors.Error, 80);
        btnDel.FlatAppearance.BorderColor = _theme.Colors.Error;
        btnDel.Click += OnDeleteNodeClicked;

        int bx = 6;
        foreach (var b in new[] { btnAddCat, btnDel })
        {
            b.Location = new Point(bx, 6);
            b.Height   = 30;
            pnlToolbar.Controls.Add(b);
            bx += b.Width + 4;
        }

        // ── Tree ───────────────────────────────────────────────────────────
        _tree = new TreeView
        {
            Dock             = DockStyle.Fill,
            BackColor        = _theme.Colors.Surface,
            ForeColor        = _theme.Colors.TextPrimary,
            Font             = _theme.Fonts.Regular,
            ShowLines        = false,
            ShowRootLines    = false,
            FullRowSelect    = true,
            HideSelection    = false,
            DrawMode         = TreeViewDrawMode.OwnerDrawAll,
            AllowDrop        = true,
            BorderStyle      = BorderStyle.None,
            ItemHeight       = 38,
            Indent           = 16,
        };
        _tree.DrawNode          += OnDrawTreeNode;
        _tree.AfterSelect       += async (_, _) => await OnTreeSelectionChanged();
        _tree.ItemDrag          += OnTreeItemDrag;
        _tree.DragEnter         += (_, e) => e.Effect = DragDropEffects.Move;
        _tree.DragOver          += OnTreeDragOver;
        _tree.DragDrop          += async (_, e) => await OnTreeDragDrop(e);
        _tree.NodeMouseClick    += OnTreeNodeRightClick;

        _split.Panel1.Controls.AddRange([pnlToolbar, _tree]);
    }

    private void BuildRightPanel()
    {
        _split.Panel2.BackColor = _theme.Colors.Background;

        // ── Right toolbar ──────────────────────────────────────────────────
        var pnlFilter = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(8, 8, 8, 0),
        };
        pnlFilter.Paint += (_, e) =>
        {
            using var p = new Pen(_theme.Colors.Border);
            e.Graphics.DrawLine(p, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _txtSearch = new TextBox
        {
            Width           = 200,
            Font            = _theme.Fonts.Regular,
            PlaceholderText = "Buscar producto…",
        };
        _txtSearch.TextChanged += (_, _) => { _searchText = _txtSearch.Text; _searchDebounce.Stop(); _searchDebounce.Start(); };

        _chkAvail = new CheckBox
        {
            Text      = "Solo disponibles",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.TextPrimary,
            AutoSize  = true,
        };
        _chkAvail.CheckedChanged += async (_, _) => { _filterAvail = _chkAvail.Checked; await RefreshGridAsync(); };

        _chkLow = new CheckBox
        {
            Text      = "Stock bajo",
            Font      = _theme.Fonts.Small,
            ForeColor = _theme.Colors.Error,
            AutoSize  = true,
        };
        _chkLow.CheckedChanged += async (_, _) => { _filterLow = _chkLow.Checked; await RefreshGridAsync(); };

        _cmbAllergen = new ComboBox
        {
            Width         = 140,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = _theme.Fonts.Small,
        };
        _cmbAllergen.Items.Add("Todos los alérgenos");
        foreach (var a in MenuConstants.Allergens14) _cmbAllergen.Items.Add(a);
        _cmbAllergen.SelectedIndex = 0;
        _cmbAllergen.SelectedIndexChanged += async (_, _) =>
        {
            _filterAllergen = _cmbAllergen.SelectedIndex == 0 ? null : _cmbAllergen.SelectedItem?.ToString();
            await RefreshGridAsync();
        };

        var btnAddProd = MakeIconButton("+ Producto", _theme.Colors.Primary, Color.White, 100);
        btnAddProd.Click += OnAddProductClicked;

        var btnPreview = MakeIconButton("👁 Vista previa", _theme.Colors.Surface, _theme.Colors.TextPrimary, 120);
        btnPreview.Click += OnPreviewClicked;

        int fx = 0;
        foreach (Control c in new Control[] { _txtSearch, _chkAvail, _chkLow, _cmbAllergen, btnPreview, btnAddProd })
        {
            c.Location = new Point(fx, 9);
            if (c is Button b) { b.Height = 30; }
            if (c is CheckBox cb) { c.Location = new Point(fx, 14); }
            pnlFilter.Controls.Add(c);
            fx += c.Width + 8;
        }

        // ── DataGridView ───────────────────────────────────────────────────
        _grid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            BackgroundColor             = _theme.Colors.Background,
            GridColor                   = _theme.Colors.Border,
            BorderStyle                 = BorderStyle.None,
            CellBorderStyle             = DataGridViewCellBorderStyle.SingleHorizontal,
            RowHeadersVisible           = false,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            AllowUserToResizeRows       = false,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly                    = true,
            AutoSizeRowsMode            = DataGridViewAutoSizeRowsMode.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 36,
            RowTemplate                 = { Height = 46 },
        };
        StyleGrid();
        AddGridColumns();

        _grid.CellPainting   += OnGridCellPainting;
        _grid.CellClick      += async (_, e) => await OnGridCellClick(e);
        _grid.CellDoubleClick += async (_, e) => await OnGridCellDoubleClick(e);

        _split.Panel2.Controls.AddRange([pnlFilter, _grid]);
    }

    private void StyleGrid()
    {
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor      = _theme.Colors.Surface,
            ForeColor      = _theme.Colors.TextPrimary,
            SelectionBackColor = _theme.Colors.Primary,
            SelectionForeColor = Color.White,
            Font           = _theme.Fonts.Regular,
            Padding        = new Padding(4, 0, 4, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor  = _theme.Colors.Background,
            ForeColor  = _theme.Colors.TextPrimary,
            SelectionBackColor = _theme.Colors.Primary,
            SelectionForeColor = Color.White,
        };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextSecondary,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            SelectionBackColor = _theme.Colors.Surface,
        };
    }

    private void AddGridColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Image",     HeaderText = "",            Width = 52,  ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Name",      HeaderText = "Nombre",      Width = 200, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Category",  HeaderText = "Categoría",   Width = 120, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Price",     HeaderText = "Precio",      Width = 80,  ReadOnly = true,
              DefaultCellStyle    = { Alignment = DataGridViewContentAlignment.MiddleRight }});
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Available", HeaderText = "Estado",      Width = 110, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Stock",     HeaderText = "Stock",       Width = 70,  ReadOnly = true,
              DefaultCellStyle    = { Alignment = DataGridViewContentAlignment.MiddleCenter }});
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Edit",      HeaderText = "",            Width = 38,  ReadOnly = true,
              DefaultCellStyle    = { Alignment = DataGridViewContentAlignment.MiddleCenter }});
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Duplicate", HeaderText = "",            Width = 38,  ReadOnly = true,
              DefaultCellStyle    = { Alignment = DataGridViewContentAlignment.MiddleCenter }});
        _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Delete",    HeaderText = "",            Width = 38,  ReadOnly = true,
              DefaultCellStyle    = { Alignment = DataGridViewContentAlignment.MiddleCenter }});

        _grid.Columns["Name"]!.AutoSizeMode      = DataGridViewAutoSizeColumnMode.Fill;
    }

    // ── Data loading ──────────────────────────────────────────────────────

    private async Task LoadAllAsync()
    {
        _categories = await _menu.GetCategoriesAsync();
        RebuildTree();
        await RefreshGridAsync();
    }

    private async Task RefreshGridAsync()
    {
        _products = await _menu.GetProductsAsync(
            _selectedCategory, _searchText.Trim(),
            _filterAvail ? true : null,
            _filterLow   ? true : null);

        if (_filterAllergen is not null)
            _products = _products.Where(p => p.Allergens.Contains(_filterAllergen)).ToList();

        PopulateGrid();
    }

    // ── TreeView ──────────────────────────────────────────────────────────

    private void RebuildTree()
    {
        if (InvokeRequired) { Invoke(RebuildTree); return; }
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        // "All" node
        var allNode = new TreeNode("Todos los productos")
        {
            Tag         = (Guid?)null,
            ForeColor   = _theme.Colors.TextPrimary,
        };
        _tree.Nodes.Add(allNode);

        // Category nodes
        int colorIdx = 0;
        foreach (var cat in _categories.OrderBy(c => c.SortOrder))
        {
            var node = new TreeNode(cat.Name)
            {
                Tag       = cat,
                ForeColor = CatColors[colorIdx % CatColors.Length],
            };
            _tree.Nodes.Add(node);
            colorIdx++;
        }

        _tree.SelectedNode = _tree.Nodes.Count > 0 ? _tree.Nodes[0] : null;
        _tree.EndUpdate();
    }

    private void OnDrawTreeNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var g    = e.Graphics;
        var node = e.Node;
        var r    = e.Bounds;
        if (r.Width == 0) return;

        // Background
        var isSelected = (e.State & TreeNodeStates.Selected) != 0;
        using var bgBrush = new SolidBrush(isSelected ? _theme.Colors.Primary : _theme.Colors.Surface);
        g.FillRectangle(bgBrush, r);

        // Color dot
        var dotColor = (node?.ForeColor ?? Color.Empty) != Color.Empty ? node!.ForeColor : _theme.Colors.TextSecondary;
        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, r.X + 10, r.Y + (r.Height - 8) / 2, 8, 8);

        // Label
        var textColor = isSelected ? Color.White : _theme.Colors.TextPrimary;
        using var textBrush = new SolidBrush(textColor);
        using var font = _theme.Fonts.Regular;
        g.DrawString(node.Text, font, textBrush, r.X + 26, r.Y + (r.Height - font.Height) / 2);

        // Product count badge for category nodes
        if (node.Tag is CategoryDto cat)
        {
            var badge = cat.UnavailableCount > 0
                ? $"{cat.ProductCount} ({cat.UnavailableCount}✕)"
                : cat.ProductCount.ToString();
            using var badgeFg = new SolidBrush(isSelected ? Color.FromArgb(200, 255, 255, 255) : _theme.Colors.TextSecondary);
            using var sf = new Font("Segoe UI", 8f);
            var bSz = g.MeasureString(badge, sf);
            g.DrawString(badge, sf, badgeFg, r.Right - bSz.Width - 8, r.Y + (r.Height - sf.Height) / 2);
        }

        // Bottom divider
        using var divPen = new Pen(_theme.Colors.Border, 1f);
        g.DrawLine(divPen, r.X, r.Bottom - 1, r.Right, r.Bottom - 1);
    }

    private async Task OnTreeSelectionChanged()
    {
        if (_tree.SelectedNode?.Tag is CategoryDto cat)
            _selectedCategory = cat.Id;
        else
            _selectedCategory = null;

        await RefreshGridAsync();
    }

    // ── Tree DnD reorder ──────────────────────────────────────────────────

    private void OnTreeItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is TreeNode node && node.Tag is CategoryDto)
            DoDragDrop(node, DragDropEffects.Move);
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        var pt   = _tree.PointToClient(new Point(e.X, e.Y));
        var node = _tree.GetNodeAt(pt);
        if (node?.Tag is CategoryDto) { _tree.SelectedNode = node; e.Effect = DragDropEffects.Move; }
        else e.Effect = DragDropEffects.None;
    }

    private async Task OnTreeDragDrop(DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode dragNode) return;
        var pt      = _tree.PointToClient(new Point(e.X, e.Y));
        var dropNode = _tree.GetNodeAt(pt);
        if (dropNode is null || dropNode == dragNode) return;

        // Move node in tree
        _tree.BeginUpdate();
        _tree.Nodes.Remove(dragNode);
        int insertAt = dropNode.Index;
        _tree.Nodes.Insert(insertAt, dragNode);
        _tree.SelectedNode = dragNode;
        _tree.EndUpdate();

        // Persist order (skip the "all" node at index 0)
        var orderedIds = _tree.Nodes.OfType<TreeNode>()
            .Where(n => n.Tag is CategoryDto)
            .Select(n => ((CategoryDto)n.Tag!).Id)
            .ToList();

        await _menu.ReorderCategoriesAsync(orderedIds);
        _toasts.Show("Orden de categorías actualizado", ToastType.Success, 2000);
    }

    private void OnTreeNodeRightClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node.Tag is null) return;
        _tree.SelectedNode = e.Node;

        var cms = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer() };
        if (e.Node.Tag is CategoryDto cat)
        {
            cms.Items.Add("✏  Renombrar categoría", null, (_, _) => OnRenameCategoryClicked(cat));
            cms.Items.Add("+ Añadir producto", null, async (_, _) => await OnAddProductInCategoryAsync(cat.Id));
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("✕  Eliminar categoría", null, async (_, _) => await DeleteCategoryAsync(cat));
        }
        cms.Show(_tree, e.Location);
    }

    // ── Grid population ───────────────────────────────────────────────────

    private void PopulateGrid()
    {
        if (InvokeRequired) { Invoke(PopulateGrid); return; }
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var p in _products)
        {
            int ri = _grid.Rows.Add(
                "",                                // image (custom painted)
                p.Name,
                p.CategoryName,
                $"€ {p.Price:N2}",
                p.IsAvailable ? "Disponible" : "No disponible",
                p.TrackStock ? (p.StockQuantity?.ToString() ?? "?") : "—",
                "✏", "⧉", "✕");
            _grid.Rows[ri].Tag = p;
        }
        _grid.ResumeLayout();
    }

    // ── Grid custom painting ──────────────────────────────────────────────

    private void OnGridCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row  = _grid.Rows[e.RowIndex];
        var prod = row.Tag as MenuProductDto;
        if (prod is null) return;

        var g    = e.Graphics!;
        var r    = e.CellBounds;
        bool sel = (e.State & DataGridViewElementStates.Selected) != 0;

        // Paint background
        using var bgBrush = new SolidBrush(sel ? e.CellStyle!.SelectionBackColor : e.CellStyle!.BackColor);
        g.FillRectangle(bgBrush, r);

        if (e.ColumnIndex == ColThumb)
        {
            PaintThumbnail(g, r, prod);
            e.Handled = true;
            return;
        }

        if (e.ColumnIndex == ColStatus)
        {
            PaintStatusToggle(g, r, prod.IsAvailable, sel);
            e.Handled = true;
            return;
        }

        if (e.ColumnIndex is ColEdit or ColDup or ColDel)
        {
            PaintActionButton(g, r, e.ColumnIndex, sel);
            e.Handled = true;
            return;
        }

        // Name column: bold
        if (e.ColumnIndex == ColName && !sel)
        {
            e.Paint(r, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            using var f  = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var tb = new SolidBrush(_theme.Colors.TextPrimary);
            g.DrawString(prod.Name, f, tb, r.X + 6, r.Y + (r.Height - f.Height) / 2);
            e.Handled = true;
            return;
        }
    }

    private void PaintThumbnail(Graphics g, Rectangle r, MenuProductDto p)
    {
        var pad  = 8;
        var sqR  = new Rectangle(r.X + pad, r.Y + (r.Height - 30) / 2, 30, 30);

        // Try cache
        if (!string.IsNullOrEmpty(p.ImageUrl) && _imgCache.TryGetValue(p.ImageUrl, out var cached))
        {
            g.DrawImage(cached, sqR);
            return;
        }

        // Letter placeholder
        int ci      = Math.Abs(p.Name.GetHashCode()) % CatColors.Length;
        using var b = new SolidBrush(CatColors[ci]);
        g.FillRectangle(b, sqR);
        using var tf = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        var letter   = p.Name.Length > 0 ? p.Name[0].ToString().ToUpper() : "?";
        var sz       = g.MeasureString(letter, tf);
        g.DrawString(letter, tf, tb, sqR.X + (sqR.Width - sz.Width) / 2, sqR.Y + (sqR.Height - sz.Height) / 2);

        // Low stock badge
        if (p.IsLowStock)
        {
            using var lb = new SolidBrush(Color.FromArgb(239, 68, 68));
            g.FillEllipse(lb, sqR.Right - 9, sqR.Y - 1, 10, 10);
        }
    }

    private void PaintStatusToggle(Graphics g, Rectangle r, bool available, bool rowSelected)
    {
        var w    = 70; var h = 24;
        var x    = r.X + (r.Width  - w) / 2;
        var y    = r.Y + (r.Height - h) / 2;
        var pill = new Rectangle(x, y, w, h);

        using var bg = new SolidBrush(available
            ? Color.FromArgb(34, 197, 94)
            : Color.FromArgb(75, 75, 75));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(pill.X, pill.Y, pill.Height, pill.Height, 90, 180);
        path.AddArc(pill.Right - pill.Height, pill.Y, pill.Height, pill.Height, 270, 180);
        path.CloseFigure();
        g.FillPath(bg, path);

        // Circle thumb
        var cx = available ? (pill.Right - pill.Height + 2) : (pill.X + 2);
        using var cb = new SolidBrush(Color.White);
        g.FillEllipse(cb, cx, pill.Y + 2, pill.Height - 4, pill.Height - 4);

        // Label
        using var tf = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        var label = available ? "ACTIVO" : "OCULTO";
        var lx    = available ? pill.X + 6 : pill.X + pill.Height + 2;
        g.DrawString(label, tf, tb, lx, pill.Y + (pill.Height - tf.Height) / 2 + 1);
    }

    private void PaintActionButton(Graphics g, Rectangle r, int col, bool rowSelected)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var (icon, color) = col switch
        {
            ColEdit => ("✏", _theme.Colors.Primary),
            ColDup  => ("⧉", Color.FromArgb(100, 100, 100)),
            _       => ("✕", _theme.Colors.Error),
        };

        var bR = new Rectangle(r.X + 4, r.Y + (r.Height - 26) / 2, r.Width - 8, 26);
        using var bgBrush = new SolidBrush(Color.FromArgb(30, color.R, color.G, color.B));
        g.FillRectangle(bgBrush, bR);
        using var pen    = new Pen(color, 1f);
        g.DrawRectangle(pen, bR);
        using var iconF  = new Font("Segoe UI Symbol", 10f);
        using var iconB  = new SolidBrush(color);
        var isz = g.MeasureString(icon, iconF);
        g.DrawString(icon, iconF, iconB,
            bR.X + (bR.Width - isz.Width) / 2, bR.Y + (bR.Height - isz.Height) / 2);
    }

    // ── Grid interactions ─────────────────────────────────────────────────

    private async Task OnGridCellClick(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not MenuProductDto prod) return;

        switch (e.ColumnIndex)
        {
            case ColStatus:
                await ToggleAvailabilityAsync(prod);
                break;
            case ColEdit:
                await OpenEditDialogAsync(prod);
                break;
            case ColDup:
                await DuplicateProductAsync(prod);
                break;
            case ColDel:
                await DeleteProductAsync(prod);
                break;
        }
    }

    private async Task OnGridCellDoubleClick(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex >= ColEdit) return;
        if (_grid.Rows[e.RowIndex].Tag is MenuProductDto prod)
            await OpenEditDialogAsync(prod);
    }

    // ── Actions ───────────────────────────────────────────────────────────

    private void OnAddCategoryClicked(object? sender, EventArgs e)
    {
        using var dlg = new CategoryNameDialog(_theme, null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _ = Task.Run(async () =>
        {
            var req  = new SaveCategoryRequest(null, dlg.CategoryName, _categories.Count, true);
            var cat  = await _menu.SaveCategoryAsync(req);
            _categories = await _menu.GetCategoriesAsync();
            Invoke(() => { RebuildTree(); _toasts.Show($"Categoría \"{cat.Name}\" creada", ToastType.Success); });
        });
    }

    private void OnRenameCategoryClicked(CategoryDto cat)
    {
        using var dlg = new CategoryNameDialog(_theme, cat.Name);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _ = Task.Run(async () =>
        {
            var req = new SaveCategoryRequest(cat.Id, dlg.CategoryName, cat.SortOrder, cat.IsActive);
            await _menu.SaveCategoryAsync(req);
            _categories = await _menu.GetCategoriesAsync();
            Invoke(() => { RebuildTree(); _toasts.Show("Categoría actualizada", ToastType.Success, 2000); });
        });
    }

    private void OnDeleteNodeClicked(object? sender, EventArgs e)
    {
        if (_tree.SelectedNode?.Tag is CategoryDto cat)
            _ = Task.Run(async () => await DeleteCategoryAsync(cat));
    }

    private async Task DeleteCategoryAsync(CategoryDto cat)
    {
        if (MessageBox.Show($"¿Eliminar categoría \"{cat.Name}\" y todos sus productos?",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await _menu.DeleteCategoryAsync(cat.Id);
        _categories = await _menu.GetCategoriesAsync();
        Invoke(() => { RebuildTree(); _toasts.Show("Categoría eliminada", ToastType.Warning); });
        await RefreshGridAsync();
    }

    private void OnAddProductClicked(object? sender, EventArgs e) =>
        _ = Task.Run(async () => await OnAddProductInCategoryAsync(_selectedCategory));

    private async Task OnAddProductInCategoryAsync(Guid? categoryId)
    {
        var cats = _categories;
        MenuProductDto? blank = categoryId.HasValue
            ? new MenuProductDto(Guid.Empty, "", categoryId.Value, "", 0, true, null, null, 10,
                [], [], false, null, null, [], [], 0, null, null)
            : null;

        await ShowEditDialogAsync(blank, cats);
    }

    private async Task OpenEditDialogAsync(MenuProductDto prod)
    {
        var cats = _categories;
        await ShowEditDialogAsync(prod, cats);
    }

    private async Task ShowEditDialogAsync(MenuProductDto? prod, IReadOnlyList<CategoryDto> cats)
    {
        MenuProductDto? product = prod;
        DialogResult result = DialogResult.Cancel;
        SaveProductRequest? req = null;

        Invoke(() =>
        {
            using var dlg = new ProductEditDialog(_theme, product, cats);
            result = dlg.ShowDialog(this);
            req = dlg.Result;
        });

        if (result != DialogResult.OK || req is null) return;

        var saved = await _menu.SaveProductAsync(req);
        _categories = await _menu.GetCategoriesAsync();
        Invoke(() =>
        {
            RebuildTree();
            _toasts.Show($"\"{saved.Name}\" guardado — cambios visibles en el menú QR ✓", ToastType.Success);
        });
        await RefreshGridAsync();
    }

    private async Task ToggleAvailabilityAsync(MenuProductDto prod)
    {
        try
        {
            await _menu.ToggleAvailabilityAsync(prod.Id, !prod.IsAvailable);
            _toasts.Show(
                prod.IsAvailable ? $"\"{prod.Name}\" ocultado del menú" : $"\"{prod.Name}\" disponible en el menú",
                ToastType.Info, 2000);
            await RefreshGridAsync();
        }
        catch (Exception ex)
        {
            _toasts.Show($"Error: {ex.Message}", ToastType.Error);
        }
    }

    private async Task DuplicateProductAsync(MenuProductDto prod)
    {
        var copy = await _menu.DuplicateProductAsync(prod.Id);
        _toasts.Show($"\"{copy.Name}\" duplicado", ToastType.Success, 2000);
        await RefreshGridAsync();
    }

    private async Task DeleteProductAsync(MenuProductDto prod)
    {
        if (MessageBox.Show($"¿Eliminar \"{prod.Name}\"?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await _menu.DeleteProductAsync(prod.Id);
        _toasts.Show($"\"{prod.Name}\" eliminado", ToastType.Warning, 2000);
        _categories = await _menu.GetCategoriesAsync();
        Invoke(RebuildTree);
        await RefreshGridAsync();
    }

    // NOTE: the PWA route is /menu/:qrToken (a table's QR code), not /menu/:restaurantId
    // — this was pointing at the API's port (5000) with the wrong kind of id entirely.
    // Still opens using the restaurant id as a placeholder since there's no table
    // picker here; the PWA will 404 ("table not found") until this is wired to an
    // actual table's QrCode.
    private void OnPreviewClicked(object? sender, EventArgs e)
    {
        // Open PWA menu preview in system browser
        var restaurantId = _state.CurrentRestaurant?.Id.ToString() ?? "demo";
        var url = $"http://localhost:5173/menu/{restaurantId}?preview=true";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { _toasts.Show("No se pudo abrir la vista previa en el navegador", ToastType.Warning); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Button MakeIconButton(string text, Color bg, Color fg, int width) => new()
    {
        Text      = text,
        Width     = width,
        BackColor = bg,
        ForeColor = fg,
        FlatStyle = FlatStyle.Flat,
        Font      = _theme.Fonts.Small,
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderColor = bg == _theme.Colors.Surface ? _theme.Colors.Border : bg, BorderSize = 1 },
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchDebounce.Dispose();
            foreach (var img in _imgCache.Values) img.Dispose();
            _imgCache.Clear();
        }
        base.Dispose(disposing);
    }

    // ── Inner dialogs ─────────────────────────────────────────────────────

    internal sealed class CategoryNameDialog : Form
    {
        private readonly TextBox _txt;
        public string CategoryName => _txt.Text.Trim();

        public CategoryNameDialog(ThemeManager theme, string? existing)
        {
            Text            = existing is null ? "Nueva categoría" : "Renombrar categoría";
            Size            = new Size(380, 140);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false; MinimizeBox = false;
            BackColor       = theme.Colors.Background;

            _txt = new TextBox { Text = existing ?? "", Width = 320, Location = new Point(16, 20), Font = theme.Fonts.Regular };

            var btnOk = new Button
            {
                Text = "Guardar", DialogResult = DialogResult.OK,
                Location = new Point(214, 58), Size = new Size(90, 32),
                BackColor = theme.Colors.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_txt.Text)) { DialogResult = DialogResult.None; return; } Close(); };

            var btnCancel = new Button
            {
                Text = "Cancelar", DialogResult = DialogResult.Cancel,
                Location = new Point(116, 58), Size = new Size(90, 32),
                FlatStyle = FlatStyle.Flat, BackColor = theme.Colors.Surface, ForeColor = theme.Colors.TextPrimary,
            };
            btnCancel.FlatAppearance.BorderColor = theme.Colors.Border;

            Controls.AddRange([_txt, btnOk, btnCancel]);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
