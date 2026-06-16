using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Menu;

public sealed class ProductEditDialog : Form
{
    private readonly ThemeManager              _theme;
    private readonly MenuProductDto?           _existing;
    private readonly IReadOnlyList<CategoryDto> _categories;

    // ── Tab: General ──────────────────────────────────────────────────────
    private PictureBox   _picImage     = null!;
    private string?      _pendingImagePath;
    private TextBox      _txtName      = null!;
    private Label        _lblCharCount = null!;
    private RichTextBox  _rtbDesc      = null!;
    private NumericUpDown _nudPrice    = null!;
    private ComboBox     _cmbCategory  = null!;
    private NumericUpDown _nudPrepTime = null!;
    private Panel        _pnlAvailable = null!;
    private bool         _isAvailable  = true;

    // ── Tab: Variants ─────────────────────────────────────────────────────
    private ListView     _lvGroups     = null!;
    private ListView     _lvOptions    = null!;
    private readonly List<ModifierGroup> _groups = [];

    // ── Tab: Allergens ────────────────────────────────────────────────────
    private CheckedListBox _clbAllergens = null!;
    private CheckedListBox _clbTags      = null!;

    // ── Tab: Stock ────────────────────────────────────────────────────────
    private CheckBox      _chkTrackStock = null!;
    private NumericUpDown _nudStockQty   = null!;
    private NumericUpDown _nudThreshold  = null!;
    private Panel         _pnlStockFields = null!;

    // ── Tab: Advanced ─────────────────────────────────────────────────────
    private NumericUpDown _nudSortOrder = null!;
    private TextBox       _txtRef       = null!;
    private RichTextBox   _rtbNotes     = null!;

    public SaveProductRequest? Result { get; private set; }

    public ProductEditDialog(ThemeManager theme, MenuProductDto? existing, IReadOnlyList<CategoryDto> categories)
    {
        _theme      = theme;
        _existing   = existing;
        _categories = categories;

        if (existing is not null)
        {
            _groups.AddRange(existing.ModifierGroups.Select(g => g with { }));
            _isAvailable = existing.IsAvailable;
        }

        Text            = existing?.Id == Guid.Empty || existing is null ? "Nuevo producto" : $"Editar: {existing.Name}";
        Size            = new Size(740, 640);
        MinimumSize     = new Size(680, 580);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = false;
        BackColor       = theme.Colors.Background;

        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private void Build()
    {
        var tabs = new TabControl
        {
            Dock      = DockStyle.Fill,
            Font      = _theme.Fonts.Regular,
        };
        StyleTabControl(tabs);

        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildVariantsTab());
        tabs.TabPages.Add(BuildAllergensTab());
        tabs.TabPages.Add(BuildStockTab());
        tabs.TabPages.Add(BuildAdvancedTab());

        var pnlButtons = BuildButtonBar();

        Controls.AddRange([tabs, pnlButtons]);
    }

    // ── Tab: General ─────────────────────────────────────────────────────

    private TabPage BuildGeneralTab()
    {
        var page = MakePage("General");

        // ── Left: image + toggle ──────────────────────────────────────────
        var pnlLeft = new Panel { Width = 160, Location = new Point(16, 16), BackColor = Color.Transparent };

        _picImage = new PictureBox
        {
            Size        = new Size(130, 130),
            Location    = new Point(0, 0),
            SizeMode    = PictureBoxSizeMode.Zoom,
            BackColor   = _theme.Colors.Border,
            Cursor      = Cursors.Hand,
        };
        _picImage.Paint += OnImagePlaceholderPaint;
        _picImage.Click += OnImageClick;

        var lblImgHint = new Label
        {
            Text      = "Click para cambiar",
            Font      = new Font("Segoe UI", 7.5f),
            ForeColor = _theme.Colors.TextSecondary,
            Location  = new Point(0, 136),
            AutoSize  = true,
        };

        // Available toggle
        _pnlAvailable = new Panel
        {
            Size      = new Size(130, 38),
            Location  = new Point(0, 158),
            BackColor = Color.Transparent,
            Cursor    = Cursors.Hand,
        };
        _pnlAvailable.Paint  += OnAvailableTogglePaint;
        _pnlAvailable.Click  += (_, _) => { _isAvailable = !_isAvailable; _pnlAvailable.Invalidate(); };

        pnlLeft.Size = new Size(160, 210);
        pnlLeft.Controls.AddRange([_picImage, lblImgHint, _pnlAvailable]);

        // ── Right: form fields ─────────────────────────────────────────────
        var pnlRight = new Panel { Location = new Point(188, 12), BackColor = Color.Transparent };
        pnlRight.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        int y = 0;

        // Name
        AddFieldLabel(pnlRight, "Nombre del producto *", ref y);
        _txtName = new TextBox
        {
            Location = new Point(0, y), Width = 480, MaxLength = 200,
            Font = _theme.Fonts.Regular, Text = _existing?.Name ?? "",
        };
        _txtName.TextChanged += (_, _) => _lblCharCount.Text = $"{_txtName.Text.Length}/200";
        _lblCharCount = new Label
        {
            Text = $"{(_existing?.Name?.Length ?? 0)}/200",
            Font = new Font("Segoe UI", 7.5f), ForeColor = _theme.Colors.TextSecondary,
            Location = new Point(486, y + 4), AutoSize = true,
        };
        pnlRight.Controls.AddRange([_txtName, _lblCharCount]);
        y += 32;

        // Price + Category
        AddFieldLabel(pnlRight, "Precio (€)", ref y);
        _nudPrice = new NumericUpDown
        {
            Location = new Point(0, y), Width = 110,
            Minimum = 0, Maximum = 9999, DecimalPlaces = 2, Increment = 0.50m,
            Value = (decimal)(_existing?.Price ?? 0), Font = _theme.Fonts.Regular,
        };
        var lblCat = new Label
        {
            Text = "Categoría *", Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary, Location = new Point(126, y - 16), AutoSize = true,
        };
        _cmbCategory = new ComboBox
        {
            Location = new Point(126, y), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList,
            Font = _theme.Fonts.Regular,
        };
        foreach (var c in _categories) _cmbCategory.Items.Add(c);
        _cmbCategory.DisplayMember = "Name";
        if (_existing?.CategoryId != null)
            _cmbCategory.SelectedItem = _categories.FirstOrDefault(c => c.Id == _existing.CategoryId);
        if (_cmbCategory.SelectedIndex < 0 && _cmbCategory.Items.Count > 0)
            _cmbCategory.SelectedIndex = 0;

        var lblPrep = new Label
        {
            Text = "Preparación (min)", Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary, Location = new Point(370, y - 16), AutoSize = true,
        };
        _nudPrepTime = new NumericUpDown
        {
            Location = new Point(370, y), Width = 70,
            Minimum = 0, Maximum = 120, Value = _existing?.PrepTimeMinutes ?? 10,
            Font = _theme.Fonts.Regular,
        };
        pnlRight.Controls.AddRange([_nudPrice, lblCat, _cmbCategory, lblPrep, _nudPrepTime]);
        y += 34;

        // Description
        AddFieldLabel(pnlRight, "Descripción", ref y);
        _rtbDesc = new RichTextBox
        {
            Location = new Point(0, y), Size = new Size(480, 90),
            Font = _theme.Fonts.Regular, BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary, BorderStyle = BorderStyle.FixedSingle,
            Text = _existing?.Description ?? "",
        };
        pnlRight.Controls.Add(_rtbDesc);
        y += 98;

        pnlRight.Size = new Size(480, y + 20);

        page.Controls.AddRange([pnlLeft, pnlRight]);
        page.Resize += (_, _) => pnlRight.Width = page.Width - 220;
        return page;
    }

    private void OnImagePlaceholderPaint(object? sender, PaintEventArgs e)
    {
        if (_picImage.Image is not null) return;
        var g  = e.Graphics;
        var r  = _picImage.ClientRectangle;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var b = new SolidBrush(_theme.Colors.Border);
        g.FillRectangle(b, r);
        using var tb = new SolidBrush(_theme.Colors.TextSecondary);
        using var f  = new Font("Segoe UI Symbol", 28f);
        var txt = "🖼";
        var sz  = g.MeasureString(txt, f);
        g.DrawString(txt, f, tb, (r.Width - sz.Width) / 2, (r.Height - sz.Height) / 2);
    }

    private void OnImageClick(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title  = "Seleccionar imagen del producto",
            Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        _pendingImagePath = ofd.FileName;
        try
        {
            _picImage.Image?.Dispose();
            _picImage.Image   = Image.FromFile(ofd.FileName);
            _picImage.SizeMode = PictureBoxSizeMode.Zoom;
        }
        catch { MessageBox.Show("No se pudo cargar la imagen.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void OnAvailableTogglePaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = _pnlAvailable.ClientRectangle;
        var w = 100; var h = 28; var x = (r.Width - w) / 2; var y = (r.Height - h) / 2;

        using var bgBrush = new SolidBrush(_isAvailable ? Color.FromArgb(34, 197, 94) : Color.FromArgb(150, 150, 150));
        using var path    = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, h, h, 90, 180);
        path.AddArc(x + w - h, y, h, h, 270, 180);
        path.CloseFigure();
        g.FillPath(bgBrush, path);

        var cx = _isAvailable ? (x + w - h + 2) : (x + 2);
        using var cb = new SolidBrush(Color.White);
        g.FillEllipse(cb, cx, y + 2, h - 4, h - 4);

        using var tf = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var tb = new SolidBrush(Color.White);
        var label = _isAvailable ? "DISPONIBLE" : "OCULTO";
        var lx = _isAvailable ? x + 6 : x + h + 2;
        g.DrawString(label, tf, tb, lx, y + (h - tf.Height) / 2);
    }

    // ── Tab: Variants ─────────────────────────────────────────────────────

    private TabPage BuildVariantsTab()
    {
        var page = MakePage("Variantes");

        // Left: modifier groups
        var pnlGroupHeader = MakeSubHeader(page, "Grupos de modificadores", 0);

        _lvGroups = new ListView
        {
            Location    = new Point(16, 54),
            Size        = new Size(320, 200),
            View        = View.Details,
            FullRowSelect = true,
            GridLines   = true,
            BackColor   = _theme.Colors.Surface,
            ForeColor   = _theme.Colors.TextPrimary,
        };
        _lvGroups.Columns.Add("Nombre",   180);
        _lvGroups.Columns.Add("Req.",       40);
        _lvGroups.Columns.Add("Min/Max",    80);
        _lvGroups.SelectedIndexChanged += (_, _) => RefreshOptionsList();
        RefreshGroupsList();

        var btnAddGroup    = MakeSmallButton("+ Grupo",    _theme.Colors.Primary, Color.White);
        var btnRemoveGroup = MakeSmallButton("− Eliminar", _theme.Colors.Surface, _theme.Colors.Error);
        btnAddGroup.Location    = new Point(16,  262);
        btnRemoveGroup.Location = new Point(116, 262);
        btnAddGroup.Click    += OnAddModifierGroupClicked;
        btnRemoveGroup.Click += OnRemoveModifierGroupClicked;

        // Right: options
        var pnlOptionHeader = MakeSubHeader(page, "Opciones del grupo seleccionado", 300);

        _lvOptions = new ListView
        {
            Location   = new Point(360, 54),
            Size       = new Size(320, 200),
            View       = View.Details,
            FullRowSelect = true,
            GridLines  = true,
            BackColor  = _theme.Colors.Surface,
            ForeColor  = _theme.Colors.TextPrimary,
        };
        _lvOptions.Columns.Add("Nombre",       160);
        _lvOptions.Columns.Add("Precio +",      80);
        _lvOptions.Columns.Add("Activo",         60);

        var btnAddOpt    = MakeSmallButton("+ Opción",    _theme.Colors.Primary, Color.White);
        var btnRemoveOpt = MakeSmallButton("− Opción",    _theme.Colors.Surface, _theme.Colors.Error);
        btnAddOpt.Location    = new Point(360, 262);
        btnRemoveOpt.Location = new Point(460, 262);
        btnAddOpt.Click    += OnAddModifierOptionClicked;
        btnRemoveOpt.Click += OnRemoveModifierOptionClicked;

        page.Controls.AddRange([_lvGroups, btnAddGroup, btnRemoveGroup, _lvOptions, btnAddOpt, btnRemoveOpt]);

        // Simple variants section below
        var lblVariants = new Label
        {
            Text = "Variantes simples (ej: tallas, sabores con precio diferente):",
            Font = _theme.Fonts.Small, ForeColor = _theme.Colors.TextSecondary,
            Location = new Point(16, 310), AutoSize = true,
        };
        page.Controls.Add(lblVariants);
        BuildSimpleVariants(page, 330);

        return page;
    }

    private void BuildSimpleVariants(TabPage page, int startY)
    {
        var lvVariants = new ListView
        {
            Location  = new Point(16, startY),
            Size      = new Size(330, 130),
            View      = View.Details,
            FullRowSelect = true,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            Tag       = new List<ProductVariant>(),
        };
        lvVariants.Columns.Add("Nombre",     160);
        lvVariants.Columns.Add("+ Precio",    80);
        lvVariants.Columns.Add("Activo",      60);

        if (_existing?.Variants is not null)
            foreach (var v in _existing.Variants)
                lvVariants.Items.Add(new ListViewItem([v.Name, $"€ {v.ExtraPrice:N2}", v.IsActive ? "✓" : "✕"]) { Tag = v });

        var btnAdd = MakeSmallButton("+ Variante", _theme.Colors.Primary, Color.White);
        btnAdd.Location = new Point(16, startY + 138);
        btnAdd.Click += (_, _) =>
        {
            using var dlg = new VariantInputDialog(_theme);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var v = new ProductVariant(Guid.NewGuid(), dlg.VariantName, dlg.ExtraPrice, true);
            var li = new ListViewItem([v.Name, $"€ {v.ExtraPrice:N2}", "✓"]) { Tag = v };
            lvVariants.Items.Add(li);
            if (lvVariants.Tag is List<ProductVariant> list) list.Add(v);
        };

        lvVariants.Tag = _existing?.Variants?.ToList() ?? new List<ProductVariant>();
        page.Controls.AddRange([lvVariants, btnAdd]);
        page.Tag = lvVariants; // store reference to read variants on save
    }

    private void RefreshGroupsList()
    {
        _lvGroups.BeginUpdate();
        _lvGroups.Items.Clear();
        foreach (var g in _groups)
            _lvGroups.Items.Add(new ListViewItem([g.Name, g.Required ? "✓" : "", $"{g.MinSelections}-{g.MaxSelections}"]) { Tag = g });
        _lvGroups.EndUpdate();
    }

    private void RefreshOptionsList()
    {
        _lvOptions.BeginUpdate();
        _lvOptions.Items.Clear();
        if (_lvGroups.SelectedItems.Count == 0) { _lvOptions.EndUpdate(); return; }
        if (_lvGroups.SelectedItems[0].Tag is not ModifierGroup grp) { _lvOptions.EndUpdate(); return; }
        foreach (var o in grp.Options)
            _lvOptions.Items.Add(new ListViewItem([o.Name, $"€ {o.ExtraPrice:N2}", o.IsActive ? "✓" : "✕"]) { Tag = o });
        _lvOptions.EndUpdate();
    }

    private void OnAddModifierGroupClicked(object? sender, EventArgs e)
    {
        using var dlg = new GroupInputDialog(_theme);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _groups.Add(new ModifierGroup(Guid.NewGuid(), dlg.GroupName, dlg.Required, dlg.Min, dlg.Max, []));
        RefreshGroupsList();
    }

    private void OnRemoveModifierGroupClicked(object? sender, EventArgs e)
    {
        if (_lvGroups.SelectedItems.Count == 0) return;
        if (_lvGroups.SelectedItems[0].Tag is ModifierGroup g) _groups.Remove(g);
        RefreshGroupsList();
    }

    private void OnAddModifierOptionClicked(object? sender, EventArgs e)
    {
        if (_lvGroups.SelectedItems.Count == 0) return;
        if (_lvGroups.SelectedItems[0].Tag is not ModifierGroup g) return;
        using var dlg = new VariantInputDialog(_theme);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var opt   = new ModifierOption(Guid.NewGuid(), dlg.VariantName, dlg.ExtraPrice, true);
        var idx   = _groups.FindIndex(x => x.Id == g.Id);
        if (idx < 0) return;
        var opts  = g.Options.ToList(); opts.Add(opt);
        _groups[idx] = g with { Options = opts };
        RefreshOptionsList();
    }

    private void OnRemoveModifierOptionClicked(object? sender, EventArgs e)
    {
        if (_lvGroups.SelectedItems.Count == 0 || _lvOptions.SelectedItems.Count == 0) return;
        if (_lvGroups.SelectedItems[0].Tag is not ModifierGroup g) return;
        if (_lvOptions.SelectedItems[0].Tag is not ModifierOption o) return;
        var idx = _groups.FindIndex(x => x.Id == g.Id);
        if (idx < 0) return;
        var opts = g.Options.Where(x => x.Id != o.Id).ToList();
        _groups[idx] = g with { Options = opts };
        RefreshOptionsList();
    }

    // ── Tab: Allergens ────────────────────────────────────────────────────

    private TabPage BuildAllergensTab()
    {
        var page = MakePage("Alérgenos y Etiquetas");

        // 14 EU Allergens
        AddFieldLabel(page, "Alérgenos (Reglamento UE 1169/2011)", 16, 16);
        _clbAllergens = new CheckedListBox
        {
            Location       = new Point(16, 40),
            Size           = new Size(300, 280),
            Font           = _theme.Fonts.Regular,
            BackColor      = _theme.Colors.Surface,
            ForeColor      = _theme.Colors.TextPrimary,
            CheckOnClick   = true,
            BorderStyle    = BorderStyle.FixedSingle,
        };
        foreach (var a in MenuConstants.Allergens14)
        {
            bool check = _existing?.Allergens.Contains(a) ?? false;
            _clbAllergens.Items.Add(a, check);
        }

        // Dietary tags
        AddFieldLabel(page, "Etiquetas / Restricciones dietéticas", 340, 16);
        _clbTags = new CheckedListBox
        {
            Location       = new Point(340, 40),
            Size           = new Size(240, 280),
            Font           = _theme.Fonts.Regular,
            BackColor      = _theme.Colors.Surface,
            ForeColor      = _theme.Colors.TextPrimary,
            CheckOnClick   = true,
            BorderStyle    = BorderStyle.FixedSingle,
        };
        foreach (var t in MenuConstants.DietaryTags)
        {
            bool check = _existing?.Tags.Contains(t) ?? false;
            _clbTags.Items.Add(t, check);
        }

        page.Controls.AddRange([_clbAllergens, _clbTags]);
        return page;
    }

    // ── Tab: Stock ────────────────────────────────────────────────────────

    private TabPage BuildStockTab()
    {
        var page = MakePage("Stock");

        _chkTrackStock = new CheckBox
        {
            Text      = "Controlar el stock de este producto",
            Font      = _theme.Fonts.SemiBold,
            ForeColor = _theme.Colors.TextPrimary,
            Location  = new Point(20, 20),
            AutoSize  = true,
            Checked   = _existing?.TrackStock ?? false,
        };
        _chkTrackStock.CheckedChanged += (_, _) => _pnlStockFields.Visible = _chkTrackStock.Checked;

        _pnlStockFields = new Panel
        {
            Location  = new Point(20, 60),
            Size      = new Size(500, 140),
            BackColor = Color.Transparent,
            Visible   = _existing?.TrackStock ?? false,
        };

        AddFieldLabel(_pnlStockFields, "Cantidad actual en stock", 0, 0);
        _nudStockQty = new NumericUpDown
        {
            Location = new Point(0, 22), Width = 100,
            Minimum = 0, Maximum = 9999,
            Value = _existing?.StockQuantity ?? 0,
            Font = _theme.Fonts.Regular,
        };

        AddFieldLabel(_pnlStockFields, "Umbral de alerta (stock bajo)", 120, 0);
        _nudThreshold = new NumericUpDown
        {
            Location = new Point(120, 22), Width = 100,
            Minimum = 0, Maximum = 9999,
            Value = _existing?.StockAlertThreshold ?? 5,
            Font = _theme.Fonts.Regular,
        };

        var lblHint = new Label
        {
            Text      = "Cuando el stock sea ≤ al umbral, aparecerá la alerta de stock bajo en el KDS y el dashboard.",
            Font      = new Font("Segoe UI", 8f, FontStyle.Italic),
            ForeColor = _theme.Colors.TextSecondary,
            Location  = new Point(0, 60),
            Size      = new Size(480, 36),
        };

        _pnlStockFields.Controls.AddRange([_nudStockQty, _nudThreshold, lblHint]);
        page.Controls.AddRange([_chkTrackStock, _pnlStockFields]);
        return page;
    }

    // ── Tab: Advanced ─────────────────────────────────────────────────────

    private TabPage BuildAdvancedTab()
    {
        var page = MakePage("Avanzado");

        AddFieldLabel(page, "Orden dentro de la categoría", 20, 20);
        _nudSortOrder = new NumericUpDown
        {
            Location = new Point(20, 42), Width = 80,
            Minimum = 0, Maximum = 999,
            Value = _existing?.SortOrder ?? 0,
            Font = _theme.Fonts.Regular,
        };

        AddFieldLabel(page, "Referencia interna", 120, 20);
        _txtRef = new TextBox
        {
            Location = new Point(120, 42), Width = 200,
            Font = _theme.Fonts.Regular,
            PlaceholderText = "ENT-001",
            Text = _existing?.InternalRef ?? "",
        };

        AddFieldLabel(page, "Notas internas (no visibles al cliente)", 20, 86);
        _rtbNotes = new RichTextBox
        {
            Location    = new Point(20, 108),
            Size        = new Size(640, 100),
            Font        = _theme.Fonts.Regular,
            BackColor   = _theme.Colors.Surface,
            ForeColor   = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text        = _existing?.InternalNotes ?? "",
        };
        _rtbNotes.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        page.Controls.AddRange([_nudSortOrder, _txtRef, _rtbNotes]);
        return page;
    }

    // ── Button bar ────────────────────────────────────────────────────────

    private Panel BuildButtonBar()
    {
        var pnl = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 56,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(12, 10, 12, 10),
        };
        pnl.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnl.Width, 0);
        };

        var btnCancel = new Button
        {
            Text      = "Cancelar",
            Size      = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Colors.Surface,
            ForeColor = _theme.Colors.TextPrimary,
            DialogResult = DialogResult.Cancel,
        };
        btnCancel.FlatAppearance.BorderColor = _theme.Colors.Border;

        var btnSave = new Button
        {
            Text      = "Guardar cambios",
            Size      = new Size(150, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            Font      = _theme.Fonts.Bold,
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += OnSaveClicked;

        pnl.Controls.AddRange([btnCancel, btnSave]);
        pnl.Resize += (_, _) =>
        {
            btnSave.Location   = new Point(pnl.Width - 166, 10);
            btnCancel.Location = new Point(pnl.Width - 272, 10);
        };

        CancelButton = btnCancel;
        return pnl;
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("El nombre del producto es obligatorio.", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_cmbCategory.SelectedItem is not CategoryDto cat)
        {
            MessageBox.Show("Selecciona una categoría.", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var allergens = Enumerable.Range(0, _clbAllergens.Items.Count)
            .Where(i => _clbAllergens.GetItemChecked(i))
            .Select(i => (string)_clbAllergens.Items[i]!)
            .ToList();

        var tags = Enumerable.Range(0, _clbTags.Items.Count)
            .Where(i => _clbTags.GetItemChecked(i))
            .Select(i => (string)_clbTags.Items[i]!)
            .ToList();

        Result = new SaveProductRequest(
            Id:                   _existing?.Id == Guid.Empty ? null : _existing?.Id,
            Name:                 _txtName.Text.Trim(),
            CategoryId:           cat.Id,
            Price:                _nudPrice.Value,
            IsAvailable:          _isAvailable,
            Description:          string.IsNullOrWhiteSpace(_rtbDesc.Text) ? null : _rtbDesc.Text.Trim(),
            PrepTimeMinutes:      (int)_nudPrepTime.Value,
            Allergens:            allergens,
            Tags:                 tags,
            TrackStock:           _chkTrackStock.Checked,
            StockQuantity:        _chkTrackStock.Checked ? (int?)_nudStockQty.Value  : null,
            StockAlertThreshold:  _chkTrackStock.Checked ? (int?)_nudThreshold.Value : null,
            Variants:             _existing?.Variants ?? [],
            ModifierGroups:       _groups,
            SortOrder:            (int)_nudSortOrder.Value,
            InternalRef:          string.IsNullOrWhiteSpace(_txtRef.Text)   ? null : _txtRef.Text.Trim(),
            InternalNotes:        string.IsNullOrWhiteSpace(_rtbNotes.Text) ? null : _rtbNotes.Text.Trim());

        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private TabPage MakePage(string title) => new(title)
    {
        BackColor = _theme.Colors.Background,
        Padding   = new Padding(12),
    };

    private void StyleTabControl(TabControl tc)
    {
        tc.DrawMode   = TabDrawMode.OwnerDrawFixed;
        tc.DrawItem  += (_, e) =>
        {
            var g    = e.Graphics;
            var tab  = tc.TabPages[e.Index];
            var r    = tc.GetTabRect(e.Index);
            bool sel = e.Index == tc.SelectedIndex;
            using var bg = new SolidBrush(sel ? _theme.Colors.Primary : _theme.Colors.Surface);
            g.FillRectangle(bg, r);
            using var tb = new SolidBrush(sel ? Color.White : _theme.Colors.TextSecondary);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(tab.Text, _theme.Fonts.Small, tb, r, sf);
        };
    }

    private static void AddFieldLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 100, 100),
            Location  = new Point(x, y),
            AutoSize  = true,
        });
    }

    private static void AddFieldLabel(Control parent, string text, ref int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 100, 100),
            Location  = new Point(0, y),
            AutoSize  = true,
        });
        y += 18;
    }

    private static Panel MakeSubHeader(Control parent, string text, int x)
    {
        var lbl = new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(120, 120, 120),
            Location  = new Point(x + 16, 30),
            AutoSize  = true,
        };
        parent.Controls.Add(lbl);
        return new Panel();
    }

    private Button MakeSmallButton(string text, Color bg, Color fg) => new()
    {
        Text      = text,
        Size      = new Size(90, 28),
        BackColor = bg,
        ForeColor = fg,
        FlatStyle = FlatStyle.Flat,
        Font      = _theme.Fonts.Small,
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderColor = bg == _theme.Colors.Surface ? _theme.Colors.Border : bg },
    };

    // ── Inner dialogs ─────────────────────────────────────────────────────

    internal sealed class VariantInputDialog : Form
    {
        private readonly TextBox      _txtName;
        private readonly NumericUpDown _nudPrice;
        public string VariantName => _txtName.Text.Trim();
        public decimal ExtraPrice => _nudPrice.Value;

        public VariantInputDialog(ThemeManager theme)
        {
            Text = "Nueva variante / opción"; Size = new Size(360, 160);
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; BackColor = theme.Colors.Background;

            Controls.Add(new Label { Text = "Nombre:",    Font = theme.Fonts.Regular, Location = new Point(12, 16), AutoSize = true, ForeColor = theme.Colors.TextPrimary });
            _txtName = new TextBox { Location = new Point(70,  12), Width = 240, Font = theme.Fonts.Regular };
            Controls.Add(new Label { Text = "Precio +:", Font = theme.Fonts.Regular, Location = new Point(12, 50), AutoSize = true, ForeColor = theme.Colors.TextPrimary });
            _nudPrice = new NumericUpDown { Location = new Point(70, 46), Width = 100, Minimum = -99, Maximum = 999, DecimalPlaces = 2, Font = theme.Fonts.Regular };
            Controls.Add(_txtName); Controls.Add(_nudPrice);

            var ok  = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(230, 84), Size = new Size(80, 30), BackColor = theme.Colors.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_txtName.Text)) { DialogResult = DialogResult.None; return; } Close(); };
            Controls.Add(ok);
            AcceptButton = ok;
        }
    }

    internal sealed class GroupInputDialog : Form
    {
        private readonly TextBox       _txtName;
        private readonly CheckBox      _chkRequired;
        private readonly NumericUpDown _nudMin, _nudMax;
        public string GroupName  => _txtName.Text.Trim();
        public bool   Required   => _chkRequired.Checked;
        public int    Min        => (int)_nudMin.Value;
        public int    Max        => (int)_nudMax.Value;

        public GroupInputDialog(ThemeManager theme)
        {
            Text = "Nuevo grupo de modificadores"; Size = new Size(400, 220);
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; BackColor = theme.Colors.Background;

            Controls.Add(new Label { Text = "Nombre del grupo:", Location = new Point(12, 16), AutoSize = true, Font = theme.Fonts.Regular, ForeColor = theme.Colors.TextPrimary });
            _txtName = new TextBox { Location = new Point(12, 36), Width = 350, Font = theme.Fonts.Regular };
            _chkRequired = new CheckBox { Text = "Selección obligatoria", Location = new Point(12, 72), AutoSize = true, Font = theme.Fonts.Regular, ForeColor = theme.Colors.TextPrimary };
            Controls.Add(new Label { Text = "Min selecciones:", Location = new Point(12,  100), AutoSize = true, Font = theme.Fonts.Regular, ForeColor = theme.Colors.TextPrimary });
            _nudMin = new NumericUpDown { Location = new Point(130, 96), Width = 70, Minimum = 0, Maximum = 10, Font = theme.Fonts.Regular };
            Controls.Add(new Label { Text = "Max:",               Location = new Point(215, 100), AutoSize = true, Font = theme.Fonts.Regular, ForeColor = theme.Colors.TextPrimary });
            _nudMax = new NumericUpDown { Location = new Point(240, 96), Width = 70, Minimum = 1, Maximum = 10, Value = 1, Font = theme.Fonts.Regular };
            Controls.AddRange([_txtName, _chkRequired, _nudMin, _nudMax]);

            var ok = new Button { Text = "Crear grupo", DialogResult = DialogResult.OK, Location = new Point(250, 140), Size = new Size(110, 32), BackColor = theme.Colors.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_txtName.Text)) { DialogResult = DialogResult.None; return; } Close(); };
            Controls.Add(ok);
            AcceptButton = ok;
        }
    }
}
