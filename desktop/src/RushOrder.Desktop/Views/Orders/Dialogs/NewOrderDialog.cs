using RushOrder.Desktop.Models;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Orders.Dialogs;

public sealed class NewOrderDialog : Form
{
    private readonly ThemeManager          _theme;
    private readonly ProductSearchService  _products;
    private readonly TableService          _tables;
    private readonly AppState              _state;

    private ComboBox  _cmbTable     = null!;
    private NumericUpDown _nudGuests = null!;
    private TextBox   _txtSearch    = null!;
    private ListBox   _lbResults    = null!;
    private NumericUpDown _nudQty   = null!;
    private TextBox   _txtItemNotes = null!;
    private Panel     _pnlOrderItems = null!;
    private Label     _lblTotal     = null!;
    private TextBox   _txtNotes     = null!;
    private Button    _btnConfirm   = null!;

    private readonly List<(ProductDto Product, int Qty, string? Notes)> _items = [];
    private IReadOnlyList<TableDto> _tableList = [];
    private IReadOnlyList<ProductDto> _searchResults = [];

    public CreateOrderRequest? Result { get; private set; }

    public NewOrderDialog(ThemeManager theme, ProductSearchService products,
        TableService tables, AppState state)
    {
        _theme    = theme;
        _products = products;
        _tables   = tables;
        _state    = state;

        Text            = "Nuevo pedido";
        Size            = new Size(780, 620);
        MinimumSize     = new Size(700, 560);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor       = theme.Colors.Background;
        MaximizeBox     = false;

        Build();
        Load += async (_, _) => await OnLoadAsync();
    }

    private void Build()
    {
        // ── Left panel: table selection + product search ──────────────────
        var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 360, BackColor = _theme.Colors.Surface, Padding = new Padding(16) };

        // Table row
        var lblTable  = MakeSectionLabel("Mesa", new Point(16, 16));
        _cmbTable     = new ComboBox { Location = new Point(16, 38), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Font = _theme.Fonts.Regular };
        var lblGuests = MakeSectionLabel("Pax", new Point(224, 16));
        _nudGuests    = new NumericUpDown { Location = new Point(224, 38), Width = 60, Minimum = 1, Maximum = 20, Value = 2 };

        // Product search
        var lblSearch = MakeSectionLabel("Buscar producto", new Point(16, 76));
        _txtSearch    = new TextBox { Location = new Point(16, 96), Width = 328, Font = _theme.Fonts.Regular, PlaceholderText = "Escribe para buscar..." };
        _txtSearch.TextChanged += async (_, _) => await SearchProductsAsync();

        _lbResults = new ListBox
        {
            Location   = new Point(16, 124),
            Size       = new Size(328, 180),
            Font       = _theme.Fonts.Regular,
            BackColor  = _theme.Colors.Surface,
            ForeColor  = _theme.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _lbResults.DrawMode   = DrawMode.OwnerDrawFixed;
        _lbResults.ItemHeight = 28;
        _lbResults.DrawItem  += OnDrawSearchItem;

        var lblQty     = MakeSectionLabel("Cantidad", new Point(16, 318));
        _nudQty        = new NumericUpDown { Location = new Point(16, 338), Width = 70, Minimum = 1, Maximum = 50, Value = 1 };
        var lblNotes   = MakeSectionLabel("Nota al artículo", new Point(100, 318));
        _txtItemNotes  = new TextBox { Location = new Point(100, 338), Width = 244, Font = _theme.Fonts.Regular, PlaceholderText = "Sin guarnición, sin sal..." };

        var btnAdd = new Button
        {
            Text      = "Añadir artículo →",
            Location  = new Point(16, 370),
            Width     = 328, Height = 36,
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.SemiBold,
            Cursor    = Cursors.Hand,
        };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += OnAddItem;

        // Order notes
        var lblOrderNotes = MakeSectionLabel("Nota del pedido", new Point(16, 420));
        _txtNotes = new TextBox
        {
            Location    = new Point(16, 440),
            Size        = new Size(328, 50),
            Font        = _theme.Fonts.Regular,
            Multiline   = true,
            PlaceholderText = "Alergias, preferencias generales...",
        };

        pnlLeft.Controls.AddRange([
            lblTable, _cmbTable, lblGuests, _nudGuests,
            lblSearch, _txtSearch, _lbResults,
            lblQty, _nudQty, lblNotes, _txtItemNotes, btnAdd,
            lblOrderNotes, _txtNotes,
        ]);

        // ── Right panel: order summary ────────────────────────────────────
        var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Colors.Background, Padding = new Padding(12) };

        var lblSummary = new Label
        {
            Text    = "RESUMEN DEL PEDIDO",
            Font    = PoppinsFont.New("Poppins", 8f, FontStyle.Bold),
            ForeColor = _theme.Colors.TextSecondary,
            Dock    = DockStyle.Top,
            Height  = 28,
            Padding = new Padding(0, 8, 0, 0),
        };

        _pnlOrderItems = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };

        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 90, BackColor = _theme.Colors.Surface };
        _lblTotal = new Label
        {
            Text      = "Total: € 0,00",
            Font      = _theme.Fonts.Bold,
            ForeColor = _theme.Colors.TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 36,
            TextAlign = ContentAlignment.MiddleRight,
            Padding   = new Padding(0, 0, 12, 0),
        };
        _btnConfirm = new Button
        {
            Text      = "Confirmar pedido",
            Dock      = DockStyle.Bottom,
            Height    = 42,
            BackColor = _theme.Colors.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = _theme.Fonts.Bold,
            Cursor    = Cursors.Hand,
            Enabled   = false,
        };
        _btnConfirm.FlatAppearance.BorderSize = 0;
        _btnConfirm.Click += OnConfirm;

        pnlBottom.Controls.AddRange([_lblTotal, _btnConfirm]);

        pnlRight.Controls.AddRange([lblSummary, _pnlOrderItems, pnlBottom]);

        // Splitter
        var splitter = new Splitter { Dock = DockStyle.Left, Width = 4, BackColor = _theme.Colors.Border };

        Controls.AddRange([pnlLeft, splitter, pnlRight]);
    }

    private async Task OnLoadAsync()
    {
        _tableList = await _tables.GetTablesAsync();
        _cmbTable.Items.Clear();
        _cmbTable.Items.Add("Sin mesa (barra)");
        foreach (var t in _tableList)
            _cmbTable.Items.Add($"Mesa {t.Number}  ({t.State})");
        _cmbTable.SelectedIndex = 0;

        var initial = await _products.SearchAsync("");
        UpdateSearchResults(initial);
    }

    private async Task SearchProductsAsync()
    {
        var query = _txtSearch.Text;
        await Task.Delay(150); // debounce
        if (_txtSearch.Text != query) return;
        var results = await _products.SearchAsync(query);
        UpdateSearchResults(results);
    }

    private void UpdateSearchResults(IReadOnlyList<ProductDto> results)
    {
        if (InvokeRequired) { Invoke(() => UpdateSearchResults(results)); return; }
        _searchResults = results;
        _lbResults.BeginUpdate();
        _lbResults.Items.Clear();
        foreach (var p in results) _lbResults.Items.Add(p);
        _lbResults.EndUpdate();
    }

    private void OnDrawSearchItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _searchResults.Count) return;
        var p = _searchResults[e.Index];

        e.DrawBackground();
        var bg = (e.State & DrawItemState.Selected) != 0
            ? _theme.Colors.Primary
            : (e.Index % 2 == 0 ? _theme.Colors.Surface : _theme.Colors.Background);
        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        var textColor = (e.State & DrawItemState.Selected) != 0 ? Color.White : _theme.Colors.TextPrimary;
        using var fgBrush = new SolidBrush(textColor);
        using var prBrush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(200, 255, 255, 255) : _theme.Colors.TextSecondary);

        e.Graphics.DrawString(p.Name, _theme.Fonts.Regular, fgBrush,
            new RectangleF(e.Bounds.X + 6, e.Bounds.Y + 4, e.Bounds.Width - 80, 20));
        e.Graphics.DrawString($"€ {p.Price:N2}", _theme.Fonts.SemiBold, prBrush,
            new RectangleF(e.Bounds.Right - 74, e.Bounds.Y + 4, 68, 20),
            new StringFormat { Alignment = StringAlignment.Far });
    }

    private void OnAddItem(object? sender, EventArgs e)
    {
        if (_lbResults.SelectedIndex < 0) return;
        var product = _searchResults[_lbResults.SelectedIndex];
        var qty     = (int)_nudQty.Value;
        var notes   = _txtItemNotes.Text.Trim().NullIfEmpty();

        _items.Add((product, qty, notes));
        _nudQty.Value         = 1;
        _txtItemNotes.Text    = "";
        _txtSearch.Text       = "";

        RebuildOrderSummary();
    }

    private void RebuildOrderSummary()
    {
        _pnlOrderItems.Controls.Clear();
        int y = 0;
        foreach (var (product, qty, notes) in _items)
        {
            var row = BuildItemRow(product, qty, notes, y);
            _pnlOrderItems.Controls.Add(row);
            y += row.Height + 2;
        }

        var total = _items.Sum(x => x.Product.Price * x.Qty);
        _lblTotal.Text  = $"Total: € {total:N2}";
        _btnConfirm.Enabled = _items.Count > 0;
    }

    private Panel BuildItemRow(ProductDto product, int qty, string? notes, int y)
    {
        var row = new Panel
        {
            Size      = new Size(_pnlOrderItems.Width - 4, notes is not null ? 44 : 28),
            Location  = new Point(0, y),
            BackColor = _theme.Colors.Surface,
        };
        var lblName  = new Label { Text = $"{qty}× {product.Name}", Font = _theme.Fonts.Regular, ForeColor = _theme.Colors.TextPrimary, Location = new Point(8, 4), AutoSize = true };
        var lblPrice = new Label { Text = $"€ {product.Price * qty:N2}", Font = _theme.Fonts.SemiBold, ForeColor = _theme.Colors.TextPrimary, AutoSize = true };
        var btnDel   = new Button { Text = "✕", Size = new Size(22, 22), Location = new Point(row.Width - 26, 3), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = _theme.Colors.Error, Font = _theme.Fonts.Small, Cursor = Cursors.Hand };
        btnDel.FlatAppearance.BorderSize = 0;

        var capturedItem = (product, qty, notes);
        btnDel.Click += (_, _) => { _items.Remove(capturedItem); RebuildOrderSummary(); };
        row.Controls.AddRange([lblName, lblPrice, btnDel]);
        row.Resize += (_, _) => { lblPrice.Location = new Point(row.Width - lblPrice.Width - 30, 4); btnDel.Left = row.Width - 26; };

        if (notes is not null)
        {
            row.Controls.Add(new Label
            {
                Text      = $"  ↳ {notes}",
                Font      = PoppinsFont.New("Poppins", 7f, FontStyle.Italic),
                ForeColor = _theme.Colors.TextSecondary,
                Location  = new Point(8, 24), AutoSize = true,
            });
        }
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };
        return row;
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        if (_items.Count == 0) return;

        Guid? tableId     = null;
        string tableName  = "Barra";
        if (_cmbTable.SelectedIndex > 0 && _cmbTable.SelectedIndex - 1 < _tableList.Count)
        {
            var t    = _tableList[_cmbTable.SelectedIndex - 1];
            tableId  = t.Id;
            tableName = $"Mesa {t.Number}";
        }

        Result = new CreateOrderRequest(
            RestaurantId: _state.CurrentRestaurant?.Id ?? Guid.Empty,
            TableId:      tableId,
            TableName:    tableName,
            GuestCount:   (int)_nudGuests.Value,
            Source:       "Manual",
            Items:        _items.Select(x => new CreateOrderItemRequest(x.Product.Id, x.Product.Name, x.Product.Price, x.Qty, x.Notes)).ToList(),
            Notes:        _txtNotes.Text.Trim().NullIfEmpty());

        DialogResult = DialogResult.OK;
        Close();
    }

    private Label MakeSectionLabel(string text, Point location) => new()
    {
        Text      = text,
        Font      = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold),
        ForeColor = _theme.Colors.TextSecondary,
        Location  = location,
        AutoSize  = true,
    };
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
