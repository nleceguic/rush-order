using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Kitchen;

public sealed class KitchenOrderCard : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────
    public event Action<KitchenOrderCard>? MarkPreparingClicked;
    public event Action<KitchenOrderCard>? MarkReadyClicked;
    public event Action<KitchenOrderCard>? ToggleUrgentClicked;
    public event Action<KitchenOrderCard>? SwipeArchived;

    private KitchenOrderDto _order;
    private readonly ThemeManager    _theme;
    private KitchenStation           _stationFilter;

    // Controls
    private Panel  _pnlHeader  = null!;
    private Label  _lblNum     = null!;
    private Label  _lblTable   = null!;
    private Label  _lblTimer   = null!;
    private Label  _lblUrgent  = null!;
    private Panel  _pnlItems   = null!;
    private Button _btnPrepare = null!;
    private Button _btnReady   = null!;

    // Per-item completion state
    private readonly Dictionary<Guid, bool> _itemChecked = [];

    // Timers
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _flashTimer;
    private readonly System.Windows.Forms.Timer _animTimer;

    private bool _flashOn   = false;
    private int  _animH     = 0;
    private int  _targetH   = 0;

    // Swipe
    private int _swipeStartX;
    private bool _swiping;

    public KitchenOrderDto Order => _order;
    public bool AllItemsChecked  => _itemChecked.Count > 0 && _itemChecked.Values.All(v => v);

    // ── Colors ────────────────────────────────────────────────────────────
    private static readonly Color GreenTime  = Color.FromArgb(34,  197, 94);
    private static readonly Color YellowTime = Color.FromArgb(234, 179,  8);
    private static readonly Color RedTime    = Color.FromArgb(239, 68,  68);
    private static readonly Color RedFlash   = Color.FromArgb(180, 30,  30);
    private static readonly Color AllergenFg = Color.FromArgb(255, 149,  0);
    private static readonly Color UrgentBorder = Color.FromArgb(239, 68, 68);

    public KitchenOrderCard(KitchenOrderDto order, ThemeManager theme, KitchenStation stationFilter)
    {
        _order         = order;
        _theme         = theme;
        _stationFilter = stationFilter;

        DoubleBuffered = true;
        Width          = 420;
        Cursor         = Cursors.Default;

        foreach (var item in order.Items)
            _itemChecked[item.ItemId] = false;

        BuildLayout();
        CalculateHeight();

        // Slide-in: start height at 1, animate to target
        _animH  = 1;
        Height  = 1;
        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();

        // Must exist before the first UpdateTimerLabel() call below, which can reach into
        // it (Stop/Start) immediately if this order is already old when the card is built.
        _flashTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _flashTimer.Tick += (_, _) => { _flashOn = !_flashOn; _pnlHeader.Invalidate(); };

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateTimerLabel();
        _clockTimer.Start();
        UpdateTimerLabel();

        // Swipe detection on the whole card
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _swipeStartX = e.X; _swiping = true; } };
        MouseUp   += (_, e) =>
        {
            if (!_swiping) return;
            _swiping = false;
            if (e.X - _swipeStartX < -90)   // left swipe ≥ 90px
                SwipeArchived?.Invoke(this);
        };
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // ── Header ────────────────────────────────────────────────────────
        _pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 72,
            BackColor = HeaderBg(),
        };
        _pnlHeader.Paint += OnHeaderPaint;

        _lblNum = new Label
        {
            Text      = _order.OrderNumber,
            Font      = PoppinsFont.New("Poppins", 18f, FontStyle.Bold),
            ForeColor = Color.White,
            Location  = new Point(12, 10),
            AutoSize  = true,
        };

        _lblTable = new Label
        {
            Text      = $"{_order.TableName}  ·  {_order.GuestCount} pax",
            Font      = PoppinsFont.New("Poppins", 11f),
            ForeColor = Color.FromArgb(210, 210, 210),
            Location  = new Point(12, 46),
            AutoSize  = true,
        };

        _lblTimer = new Label
        {
            Text      = "0:00",
            Font      = PoppinsFont.New("Poppins", 15f, FontStyle.Bold),
            ForeColor = GreenTime,
            AutoSize  = true,
        };

        _lblUrgent = new Label
        {
            Text      = "⚡ URGENTE",
            Font      = PoppinsFont.New("Poppins", 9f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(239, 68, 68),
            Visible   = _order.IsUrgent,
            AutoSize  = true,
            Padding   = new Padding(4, 2, 4, 2),
        };

        _pnlHeader.Controls.AddRange([_lblNum, _lblTable, _lblTimer, _lblUrgent]);
        _pnlHeader.Resize += (_, _) =>
        {
            _lblTimer.Location  = new Point(_pnlHeader.Width - _lblTimer.Width - 12, 14);
            _lblUrgent.Location = new Point(_pnlHeader.Width - _lblUrgent.Width - 12, 46);
        };

        // Double-click header to toggle urgent
        _pnlHeader.DoubleClick += (_, _) => ToggleUrgentClicked?.Invoke(this);
        _lblNum.DoubleClick    += (_, _) => ToggleUrgentClicked?.Invoke(this);

        // ── Items ─────────────────────────────────────────────────────────
        _pnlItems = new Panel
        {
            Dock      = DockStyle.Top,
            AutoScroll = false,
            BackColor = _theme.Colors.Surface,
        };

        BuildItemRows();

        // ── Buttons ───────────────────────────────────────────────────────
        var pnlBtns = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 56,
            BackColor = _theme.Colors.Surface,
            Padding   = new Padding(8, 8, 8, 0),
        };
        pnlBtns.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Colors.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0);
        };

        _btnPrepare = MakeActionButton("▶  EN PREPARACIÓN", Color.FromArgb(234, 179, 8), Color.Black);
        _btnPrepare.Visible = _order.Status == OrderStatus.New;
        _btnPrepare.Click  += (_, _) => MarkPreparingClicked?.Invoke(this);

        _btnReady = MakeActionButton("✓  TODO LISTO", Color.FromArgb(34, 197, 94), Color.White);
        _btnReady.Visible = _order.Status == OrderStatus.Preparing;
        _btnReady.Click  += (_, _) => MarkReadyClicked?.Invoke(this);

        pnlBtns.Controls.AddRange([_btnPrepare, _btnReady]);
        pnlBtns.Resize += (_, _) =>
        {
            _btnPrepare.Size = _btnReady.Size = new Size(pnlBtns.Width - 16, 40);
        };

        // Bottom border line (card separator)
        var pnlSep = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = _theme.Colors.Background };

        Controls.AddRange([pnlSep, pnlBtns, _pnlItems, _pnlHeader]);
    }

    private void BuildItemRows()
    {
        _pnlItems.Controls.Clear();
        int y = 6;

        var visibleItems = _stationFilter == KitchenStation.All
            ? _order.Items
            : _order.Items.Where(i => i.Station == _stationFilter).ToList();

        foreach (var item in visibleItems)
        {
            var row = new ItemRow(item, _itemChecked[item.ItemId], _theme);
            row.Location = new Point(0, y);
            row.Width    = _pnlItems.Width > 0 ? _pnlItems.Width : 420;
            row.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.CheckedChanged += (isChecked) =>
            {
                _itemChecked[item.ItemId] = isChecked;
                // Auto-mark all ready if every item is checked
                if (AllItemsChecked && _order.Status == OrderStatus.Preparing)
                    MarkReadyClicked?.Invoke(this);
            };
            _pnlItems.Controls.Add(row);
            y += row.Height + 2;
        }

        if (visibleItems.Count == 0 && _stationFilter != KitchenStation.All)
        {
            _pnlItems.Controls.Add(new Label
            {
                Text      = "Sin ítems en esta estación",
                Font      = _theme.Fonts.Small,
                ForeColor = _theme.Colors.TextSecondary,
                Location  = new Point(12, 10),
                AutoSize  = true,
            });
            y += 30;
        }

        _pnlItems.Height = y + 6;
        _pnlItems.Resize += (_, _) =>
        {
            foreach (Control c in _pnlItems.Controls)
                c.Width = _pnlItems.Width;
        };
    }

    private void CalculateHeight()
    {
        int h = _pnlHeader.Height + _pnlItems.Height + 56 + 4; // header + items + buttons + sep
        _targetH = h;
    }

    // ── Painting ──────────────────────────────────────────────────────────

    private void OnHeaderPaint(object? sender, PaintEventArgs e)
    {
        var g  = e.Graphics;
        var bg = (_flashOn && _order.Age.TotalMinutes >= 12)
            ? RedFlash
            : HeaderBg();
        using var b = new SolidBrush(bg);
        g.FillRectangle(b, 0, 0, _pnlHeader.Width, _pnlHeader.Height);

        // Urgent left stripe
        if (_order.IsUrgent)
        {
            using var stripe = new SolidBrush(UrgentBorder);
            g.FillRectangle(stripe, 0, 0, 5, _pnlHeader.Height);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Outer card border
        using var pen = new Pen(_order.IsUrgent ? UrgentBorder : _theme.Colors.Border, _order.IsUrgent ? 2f : 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    // ── Timer logic ───────────────────────────────────────────────────────

    private void UpdateTimerLabel()
    {
        var age = _order.Age;
        var min = (int)age.TotalMinutes;
        var sec = age.Seconds;

        _lblTimer.Text = $"{min}:{sec:D2}";

        if (age.TotalMinutes < 8)
        {
            _lblTimer.ForeColor = GreenTime;
            _flashTimer.Stop();
            _pnlHeader.BackColor = HeaderBg();
        }
        else if (age.TotalMinutes < 12)
        {
            _lblTimer.ForeColor = YellowTime;
            _flashTimer.Stop();
        }
        else
        {
            _lblTimer.ForeColor = RedTime;
            if (!_flashTimer.Enabled) _flashTimer.Start();
        }

        // Reposition timer label
        _lblTimer.Location = new Point(_pnlHeader.Width - _lblTimer.Width - 12, 14);
    }

    // ── Slide-in animation ────────────────────────────────────────────────

    private void OnAnimTick(object? sender, EventArgs e)
    {
        var diff = _targetH - _animH;
        if (Math.Abs(diff) <= 2)
        {
            Height = _targetH;
            _animTimer.Stop();
            _animTimer.Dispose();
            return;
        }
        _animH += (int)Math.Max(1, diff * 0.35);
        Height  = _animH;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void UpdateOrder(KitchenOrderDto updated)
    {
        _order = updated;
        _lblNum.Text   = updated.OrderNumber;
        _lblTable.Text = $"{updated.TableName}  ·  {updated.GuestCount} pax";
        _lblUrgent.Visible = updated.IsUrgent;
        _pnlHeader.BackColor = HeaderBg();
        _pnlHeader.Invalidate();

        _btnPrepare.Visible = updated.Status == OrderStatus.New;
        _btnReady.Visible   = updated.Status == OrderStatus.Preparing;
    }

    public void ApplyStationFilter(KitchenStation station)
    {
        _stationFilter = station;
        BuildItemRows();
        CalculateHeight();
        Height = _targetH;
    }

    public bool IsReadyAndOld => _order.Status == OrderStatus.Ready && _order.Age.TotalMinutes > 3;

    private bool _dimmed;
    public bool Dimmed
    {
        get => _dimmed;
        set
        {
            if (_dimmed == value) return;
            _dimmed = value;
            _pnlHeader.BackColor = value ? Color.FromArgb(20, 20, 20) : HeaderBg();
            BackColor            = value ? Color.FromArgb(20, 20, 20) : _theme.Colors.Surface;
            Invalidate(true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Color HeaderBg() => _order.Status switch
    {
        OrderStatus.New       => Color.FromArgb(30,  58, 138),  // deep blue
        OrderStatus.Preparing => Color.FromArgb(80,  50,  10),  // dark amber
        OrderStatus.Ready     => Color.FromArgb(20,  83,  45),  // dark green
        _                     => Color.FromArgb(40,  40,  40),
    };

    private static Button MakeActionButton(string text, Color bg, Color fg) => new()
    {
        Text      = text,
        Height    = 40,
        Dock      = DockStyle.None,
        BackColor = bg,
        ForeColor = fg,
        FlatStyle = FlatStyle.Flat,
        Font      = PoppinsFont.New("Poppins", 11f, FontStyle.Bold),
        Cursor    = Cursors.Hand,
        FlatAppearance = { BorderSize = 0 },
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer.Dispose();
            _flashTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── Inner: ItemRow ────────────────────────────────────────────────────

    private sealed class ItemRow : Panel
    {
        private bool _checked;
        public event Action<bool>? CheckedChanged;

        public ItemRow(KitchenItemDto item, bool initialChecked, ThemeManager theme)
        {
            _checked      = initialChecked;
            BackColor     = theme.Colors.Surface;
            DoubleBuffered = true;

            int rowH = 48;
            if (item.Modifiers.Count > 0)  rowH += 18;
            if (!string.IsNullOrEmpty(item.Notes)) rowH += 18;
            Height = rowH;

            Paint += (_, e) => DrawRow(e.Graphics, item, theme);

            // Entire row is the touch target
            Cursor = Cursors.Hand;
            Click += (_, _) =>
            {
                _checked = !_checked;
                Invalidate();
                CheckedChanged?.Invoke(_checked);
            };
        }

        private void DrawRow(Graphics g, KitchenItemDto item, ThemeManager theme)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Checkbox (48x48 touch target)
            var cbRect = new Rectangle(8, (48 - 28) / 2, 28, 28);
            using (var cbBorder = new Pen(_checked ? Color.FromArgb(34, 197, 94) : theme.Colors.Border, 2f))
                g.DrawRectangle(cbBorder, cbRect);

            if (_checked)
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(34, 197, 94));
                g.FillRectangle(fillBrush, cbRect.X + 2, cbRect.Y + 2, cbRect.Width - 4, cbRect.Height - 4);
                using var tick = new Pen(Color.White, 2.5f);
                g.DrawLines(tick, new PointF[]
                {
                    new(cbRect.X + 5,  cbRect.Y + cbRect.Height / 2f),
                    new(cbRect.X + cbRect.Width / 2f - 2, cbRect.Y + cbRect.Height - 6),
                    new(cbRect.X + cbRect.Width - 5, cbRect.Y + 6),
                });
            }

            // Product name
            int textX = 48;
            int textY = 10;
            bool hasAllergens = item.Allergens.Count > 0;
            var nameForeColor = _checked ? theme.Colors.TextSecondary : theme.Colors.TextPrimary;
            using var nameBrush = new SolidBrush(nameForeColor);
            using var nameFont  = PoppinsFont.New("Poppins", 12f, FontStyle.Bold);
            var quantityText = $"{item.Quantity}×  {item.ProductName}";
            if (_checked)
            {
                // Strikethrough via DrawLine
                var sz = g.MeasureString(quantityText, nameFont);
                g.DrawString(quantityText, nameFont, nameBrush, textX, textY);
                using var strikeP = new Pen(theme.Colors.TextSecondary, 1.5f);
                g.DrawLine(strikeP, textX, textY + sz.Height / 2 + 2, textX + sz.Width, textY + sz.Height / 2 + 2);
            }
            else
            {
                g.DrawString(quantityText, nameFont, nameBrush, textX, textY);
            }

            // Allergen badges
            if (hasAllergens && !_checked)
            {
                using var allergenFont  = PoppinsFont.New("Poppins", 7.5f, FontStyle.Bold);
                using var allergenBrush = new SolidBrush(AllergenFg);
                int ax = Width - 8;
                foreach (var allergen in item.Allergens.Reverse())
                {
                    var abbr = allergen.Length > 3 ? allergen[..3].ToUpper() : allergen.ToUpper();
                    var aSize = g.MeasureString(abbr, allergenFont);
                    ax -= (int)aSize.Width + 10;
                    var aBounds = new RectangleF(ax, textY + 2, aSize.Width + 6, 16);
                    using var aBorder = new Pen(AllergenFg, 1.5f);
                    g.DrawRectangle(aBorder, aBounds.X, aBounds.Y, aBounds.Width, aBounds.Height);
                    g.DrawString(abbr, allergenFont, allergenBrush, ax + 3, textY + 3);
                }
            }

            // Modifiers (italic, below name)
            int subY = textY + 24;
            if (item.Modifiers.Count > 0 && !_checked)
            {
                using var modFont  = PoppinsFont.New("Poppins", 9.5f, FontStyle.Italic);
                using var modBrush = new SolidBrush(theme.Colors.TextSecondary);
                g.DrawString("  " + string.Join("  ·  ", item.Modifiers), modFont, modBrush, textX, subY);
                subY += 18;
            }

            // Notes
            if (!string.IsNullOrEmpty(item.Notes) && !_checked)
            {
                using var noteFont  = PoppinsFont.New("Poppins", 9f, FontStyle.Italic);
                using var noteBrush = new SolidBrush(Color.FromArgb(234, 179, 8));
                g.DrawString($"  ★ {item.Notes}", noteFont, noteBrush, textX, subY);
            }

            // Bottom divider
            using var divPen = new Pen(Color.FromArgb(40, 40, 40), 1f);
            g.DrawLine(divPen, 8, Height - 1, Width - 8, Height - 1);
        }
    }
}
