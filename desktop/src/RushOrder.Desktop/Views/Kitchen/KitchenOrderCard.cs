using RushOrder.Desktop.Forms.Controls;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.Theme;

namespace RushOrder.Desktop.Views.Kitchen;

// Same visual language as Pedidos' OrderCardControl — rounded corners, a thin status-colored
// stripe down the left edge instead of a solid-color header block, a subtle border that
// highlights on hover, and rounded pill action buttons — adapted to keep Cocina's own
// functional pieces (per-item checkboxes, the urgent flag/flash, swipe-to-archive) that
// Pedidos' cards don't need.
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
    private bool                     _hovered;

    // Controls
    private Panel      _pnlHeader  = null!;
    private Label      _lblNum     = null!;
    private Label      _lblTable   = null!;
    private Label      _lblTimer   = null!;
    private Label      _lblUrgent  = null!;
    private Panel      _pnlItems   = null!;
    private Panel      _pnlBtns    = null!;
    private PillButton _btnPrepare = null!;
    private PillButton _btnReady   = null!;

    // Per-item completion state
    private readonly Dictionary<Guid, bool> _itemChecked = [];

    // Timers
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _flashTimer;
    private readonly System.Windows.Forms.Timer _animTimer;

    private bool _flashOn     = false;
    private int  _animH       = 0;
    private int  _targetH     = 0;
    private bool _slideInDone = false;

    // Swipe
    private int _swipeStartX;
    private bool _swiping;

    public KitchenOrderDto Order => _order;
    public bool AllItemsChecked  => _itemChecked.Count > 0 && _itemChecked.Values.All(v => v);

    // ── Style ─────────────────────────────────────────────────────────────
    private const float CornerRadius      = 8f;
    private const int   AccentStripeWidth = 5;
    private const int   HeaderMinHeight   = 52;
    private const int   ButtonsPanelHeight = 42;

    // Card chrome stays explicitly dark regardless of the app's light/dark theme — same
    // reasoning as KitchenDisplayView's Kds* constants ("always dark, regardless of theme").
    private static readonly Color KdsCardBg    = Color.FromArgb(28,  28,  28);
    private static readonly Color KdsRowText   = Color.FromArgb(230, 230, 230);
    private static readonly Color KdsRowSub    = Color.FromArgb(150, 150, 150);
    private static readonly Color KdsRowBorder = Color.FromArgb(70,  70,  70);
    private static readonly Color BorderIdle   = Color.FromArgb(60,  60,  60);
    private static readonly Color BorderHover  = Color.FromArgb(100, 100, 100);

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

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Width          = 300;
        Cursor         = Cursors.Default;
        BackColor      = KdsCardBg;
        // A parent's Region clips its OWN rendering but not child controls' (each is a real
        // HWND painted independently) — so header/items/buttons must stay inset from the
        // rounded edge, or their square corners poke past the curve. AccentStripeWidth on the
        // left (the stripe itself is self-painted in OnPaint, not a child, so it doesn't need
        // this) and a small gutter on the other three sides for the border stroke to show in.
        Padding        = new Padding(AccentStripeWidth, 2, 2, 2);

        foreach (var item in order.Items)
            _itemChecked[item.ItemId] = false;

        BuildLayout();
        CalculateHeight();
        Resize += (_, _) => ApplyCardRegion();

        // Slide-in: start height at 1, animate to target
        _animH  = 1;
        Height  = 1;
        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();

        // Must exist before the first UpdateTimerLabel() call below, which can reach into
        // it (Stop/Start) immediately if this order is already old when the card is built.
        _flashTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _flashTimer.Tick += (_, _) => { _flashOn = !_flashOn; Invalidate(); };

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
            Height    = HeaderMinHeight,
            BackColor = KdsCardBg,
        };

        _lblNum = new Label
        {
            Text      = _order.OrderNumber,
            Font      = PoppinsFont.New("Poppins", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Location  = new Point(10, 7),
            AutoSize  = true,
        };

        _lblTable = new Label
        {
            Text      = $"{_order.TableName}  ·  {_order.GuestCount} pax",
            Font      = PoppinsFont.New("Poppins", 8.5f),
            ForeColor = KdsRowSub,
            AutoSize  = true,
        };

        _lblTimer = new Label
        {
            Text      = "0:00",
            Font      = PoppinsFont.New("Poppins", 11f, FontStyle.Bold),
            ForeColor = GreenTime,
            AutoSize  = true,
        };

        _lblUrgent = new Label
        {
            Text      = "⚡ URGENTE",
            Font      = PoppinsFont.New("Poppins", 7f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(239, 68, 68),
            Visible   = _order.IsUrgent,
            AutoSize  = true,
            Padding   = new Padding(3, 1, 3, 1),
        };

        _pnlHeader.Controls.AddRange([_lblNum, _lblTable, _lblTimer, _lblUrgent]);
        _pnlHeader.Resize += (_, _) => RepositionHeaderRight();

        // Double-click header to toggle urgent
        _pnlHeader.DoubleClick += (_, _) => ToggleUrgentClicked?.Invoke(this);
        _lblNum.DoubleClick    += (_, _) => ToggleUrgentClicked?.Invoke(this);

        RepositionHeaderRight();

        // ── Items ─────────────────────────────────────────────────────────
        _pnlItems = new Panel
        {
            Dock      = DockStyle.Top,
            AutoScroll = false,
            BackColor = KdsCardBg,
        };

        BuildItemRows();

        // ── Buttons ───────────────────────────────────────────────────────
        _pnlBtns = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = ButtonsPanelHeight,
            BackColor = KdsCardBg,
            Padding   = new Padding(6, 6, 6, 0),
        };
        _pnlBtns.Paint += (_, e) =>
        {
            using var pen = new Pen(KdsRowBorder, 1f);
            e.Graphics.DrawLine(pen, 0, 0, _pnlBtns.Width, 0);
        };

        _btnPrepare = new PillButton(_theme, "▶  En preparación",
            idleBackColor: Color.FromArgb(234, 179, 8), idleBorderColor: Color.Transparent, idleTextColor: Color.Black);
        _btnPrepare.Visible = _order.Status == OrderStatus.New;
        _btnPrepare.Click  += (_, _) => MarkPreparingClicked?.Invoke(this);

        _btnReady = new PillButton(_theme, "✓  Todo listo",
            idleBackColor: Color.FromArgb(34, 197, 94), idleBorderColor: Color.Transparent, idleTextColor: Color.White);
        _btnReady.Visible = _order.Status == OrderStatus.Preparing;
        _btnReady.Click  += (_, _) => MarkReadyClicked?.Invoke(this);

        _pnlBtns.Controls.AddRange([_btnPrepare, _btnReady]);
        _pnlBtns.Resize += (_, _) => LayoutButtons();
        LayoutButtons();
        UpdateButtonsPanelHeight();

        // Bottom border line (card separator)
        var pnlSep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = KdsCardBg };

        // Top-docked children are all inset from the left by Padding.Left (=AccentStripeWidth,
        // set in the constructor), which is what leaves room for the self-painted accent stripe
        // in OnPaint without needing a dedicated child panel for it.
        Controls.AddRange([pnlSep, _pnlBtns, _pnlItems, _pnlHeader]);
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
            var row = new ItemRow(item, _itemChecked[item.ItemId], KdsCardBg, KdsRowText, KdsRowSub, KdsRowBorder);
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
                ForeColor = KdsRowSub,
                Location  = new Point(10, 8),
                AutoSize  = true,
            });
            y += 24;
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
        int h = _pnlHeader.Height + _pnlItems.Height + _pnlBtns.Height + 3; // header + items + buttons + sep
        _targetH = h;
    }

    // No status has both buttons hidden AND the panel still taking up space — a "Listo" order
    // (past both New and Preparing) has neither _btnPrepare nor _btnReady visible, so the panel
    // that exists solely to host them collapses to 0 instead of leaving a dead gap above the
    // separator line.
    private void UpdateButtonsPanelHeight()
    {
        var wanted = _btnPrepare.Visible || _btnReady.Visible ? ButtonsPanelHeight : 0;
        if (_pnlBtns.Height == wanted) return;

        _pnlBtns.Height = wanted;
        CalculateHeight();
        if (_slideInDone) Height = _targetH;
    }

    private void ApplyCardRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = GdiExtensions.CreateRoundedRect(new RectangleF(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    // ── Painting ──────────────────────────────────────────────────────────

    // Suppress the default square-corner background fill — everything visible is painted
    // below, in the exact rounded shape, same reasoning as OrderCardControl.
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SetQuality();

        var full = new RectangleF(0, 0, Width, Height);
        var bg = _dimmed ? Color.FromArgb(20, 20, 20) : KdsCardBg;
        var stripeColor = _dimmed ? Color.FromArgb(50, 50, 50)
            : (_flashOn && _order.Age.TotalMinutes >= 12) ? RedFlash
            : AccentColor();

        // Accent stripe: fill the whole rounded shape with the stripe color, then cover all but
        // a thin left sliver with the card's own background — the sliver curves smoothly around
        // the same corner because both fills share the exact same rounded-rect path, offset only
        // in X. Same technique Pedidos' OrderCardControl uses for its own status stripe; a plain
        // rectangular child panel there instead left square corners poking past the card's
        // rounded shape, since a parent's Region does not clip child window rendering.
        using (var stripeBrush = new SolidBrush(stripeColor))
            g.FillRoundedRectangle(stripeBrush, full, CornerRadius);
        var inner = new RectangleF(AccentStripeWidth, 0, Width - AccentStripeWidth, Height);
        using (var bgBrush = new SolidBrush(bg))
            g.FillRoundedRectangle(bgBrush, inner, CornerRadius);

        // Inset 0.5px so the anti-aliased stroke has room to blend into the rounded region's
        // edge instead of getting clipped into a jagged stairstep (same reasoning as
        // OrderCardControl's own border).
        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        var borderColor = _order.IsUrgent ? UrgentBorder : (_hovered ? BorderHover : BorderIdle);
        using var pen = new Pen(borderColor, _order.IsUrgent ? 2f : 1f);
        g.DrawRoundedRectangle(pen, rect, CornerRadius);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

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
            if (_flashTimer.Enabled) { _flashTimer.Stop(); Invalidate(); }
        }
        else if (age.TotalMinutes < 12)
        {
            _lblTimer.ForeColor = YellowTime;
            if (_flashTimer.Enabled) { _flashTimer.Stop(); Invalidate(); }
        }
        else
        {
            _lblTimer.ForeColor = RedTime;
            if (!_flashTimer.Enabled) _flashTimer.Start();
        }

        RepositionHeaderRight();
    }

    // Right-aligned timer + urgent badge stack. Urgent's Y is derived from the timer's actual
    // Bottom (not a fixed constant) so it never gets clipped when the label renders taller than
    // expected — e.g. at higher Windows DPI scaling, where AutoSize height grows accordingly.
    // The header panel's own Height is grown to match (instead of staying at a fixed constant),
    // otherwise that same taller stack would get its bottom sliced off by the items panel docked
    // right below it.
    private void RepositionHeaderRight()
    {
        // Same reasoning as the timer/urgent stack below: _lblTable's Y used to be a fixed 32,
        // which _lblNum's actual rendered Bottom (13pt Bold — routinely taller than the 25px
        // gap that constant assumed) could reach into, clipping the top of "Mesa X" under it.
        _lblTable.Location  = new Point(10, _lblNum.Bottom + 2);

        _lblTimer.Location  = new Point(_pnlHeader.Width - _lblTimer.Width - 10, 10);
        _lblUrgent.Location = new Point(_pnlHeader.Width - _lblUrgent.Width - 10, _lblTimer.Bottom + 4);

        var rightBottom = _lblUrgent.Visible ? _lblUrgent.Bottom : _lblTimer.Bottom;
        var leftBottom  = _lblTable.Bottom;
        var wanted      = Math.Max(HeaderMinHeight, Math.Max(rightBottom, leftBottom) + 8);
        if (_pnlHeader.Height != wanted)
        {
            _pnlHeader.Height = wanted;
            // _pnlItems doesn't exist yet on the very first call (made mid-BuildLayout, before
            // the items panel is built) — BuildLayout's own trailing CalculateHeight() call
            // covers that initial sizing instead.
            if (_pnlItems != null)
            {
                CalculateHeight();
                // Once the slide-in animation has finished, nothing else re-applies _targetH to
                // Height, so a later header growth/shrink (urgent toggling, DPI-driven resize)
                // needs to push it through directly instead of only updating the target.
                if (_slideInDone) Height = _targetH;
            }
        }
    }

    // Size never used Location — both buttons defaulted to (0,0), which left them flush against
    // the panel's top-left instead of centered in the space Padding carved out for them.
    private void LayoutButtons()
    {
        const int btnHeight = 32;
        var size = new Size(_pnlBtns.Width - _pnlBtns.Padding.Horizontal, btnHeight);
        var loc  = new Point(_pnlBtns.Padding.Left, (_pnlBtns.Height - btnHeight) / 2);
        _btnPrepare.Size = _btnReady.Size = size;
        _btnPrepare.Location = _btnReady.Location = loc;
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
            _slideInDone = true;
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
        RepositionHeaderRight();
        Invalidate();

        _btnPrepare.Visible = updated.Status == OrderStatus.New;
        _btnReady.Visible   = updated.Status == OrderStatus.Preparing;
        UpdateButtonsPanelHeight();
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
            var bg = value ? Color.FromArgb(20, 20, 20) : KdsCardBg;
            _pnlHeader.BackColor = bg;
            _pnlItems.BackColor  = bg;
            _pnlBtns.BackColor   = bg;
            BackColor            = bg;
            Invalidate(true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Matches Pedidos' OrderCardControl.GetStatusColor palette — same status colors, just
    // carried on a thin stripe here instead of a full header fill. Urgent overrides it with red.
    private Color StatusColor() => _order.Status switch
    {
        OrderStatus.New       => Color.FromArgb(33,  150, 243),
        OrderStatus.Preparing => Color.FromArgb(255, 152,  0),
        OrderStatus.Ready     => Color.FromArgb(76,  175,  80),
        _                     => Color.FromArgb(120, 120, 120),
    };

    private Color AccentColor() => _order.IsUrgent ? UrgentBorder : StatusColor();

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

        private readonly Color _textPrimary;
        private readonly Color _textSecondary;
        private readonly Color _border;

        public ItemRow(
            KitchenItemDto item, bool initialChecked,
            Color bg, Color textPrimary, Color textSecondary, Color border)
        {
            _checked       = initialChecked;
            _textPrimary   = textPrimary;
            _textSecondary = textSecondary;
            _border        = border;
            BackColor      = bg;
            DoubleBuffered = true;

            int rowH = 36;
            if (item.Modifiers.Count > 0)  rowH += 14;
            if (!string.IsNullOrEmpty(item.Notes)) rowH += 14;
            Height = rowH;

            Paint += (_, e) => DrawRow(e.Graphics, item);

            // Entire row is the touch target
            Cursor = Cursors.Hand;
            Click += (_, _) =>
            {
                _checked = !_checked;
                Invalidate();
                CheckedChanged?.Invoke(_checked);
            };
        }

        private void DrawRow(Graphics g, KitchenItemDto item)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Checkbox (36x36 touch target)
            var cbRect = new Rectangle(6, (36 - 20) / 2, 20, 20);
            using (var cbBorder = new Pen(_checked ? Color.FromArgb(34, 197, 94) : _border, 2f))
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
            int textX = 36;
            int textY = 7;
            bool hasAllergens = item.Allergens.Count > 0;
            var nameForeColor = _checked ? _textSecondary : _textPrimary;
            using var nameBrush = new SolidBrush(nameForeColor);
            using var nameFont  = PoppinsFont.New("Poppins", 9.5f, FontStyle.Bold);
            var quantityText = $"{item.Quantity}×  {item.ProductName}";
            if (_checked)
            {
                // Strikethrough via DrawLine
                var sz = g.MeasureString(quantityText, nameFont);
                g.DrawString(quantityText, nameFont, nameBrush, textX, textY);
                using var strikeP = new Pen(_textSecondary, 1.5f);
                g.DrawLine(strikeP, textX, textY + sz.Height / 2 + 2, textX + sz.Width, textY + sz.Height / 2 + 2);
            }
            else
            {
                g.DrawString(quantityText, nameFont, nameBrush, textX, textY);
            }

            // Allergen badges
            if (hasAllergens && !_checked)
            {
                using var allergenFont  = PoppinsFont.New("Poppins", 6.5f, FontStyle.Bold);
                using var allergenBrush = new SolidBrush(AllergenFg);
                int ax = Width - 6;
                foreach (var allergen in item.Allergens.Reverse())
                {
                    var abbr = allergen.Length > 3 ? allergen[..3].ToUpper() : allergen.ToUpper();
                    var aSize = g.MeasureString(abbr, allergenFont);
                    ax -= (int)aSize.Width + 8;
                    var aBounds = new RectangleF(ax, textY + 1, aSize.Width + 6, 13);
                    using var aBorder = new Pen(AllergenFg, 1.5f);
                    g.DrawRectangle(aBorder, aBounds.X, aBounds.Y, aBounds.Width, aBounds.Height);
                    g.DrawString(abbr, allergenFont, allergenBrush, ax + 3, textY + 2);
                }
            }

            // Modifiers (italic, below name)
            int subY = textY + 18;
            if (item.Modifiers.Count > 0 && !_checked)
            {
                using var modFont  = PoppinsFont.New("Poppins", 8f, FontStyle.Italic);
                using var modBrush = new SolidBrush(_textSecondary);
                g.DrawString("  " + string.Join("  ·  ", item.Modifiers), modFont, modBrush, textX, subY);
                subY += 14;
            }

            // Notes
            if (!string.IsNullOrEmpty(item.Notes) && !_checked)
            {
                using var noteFont  = PoppinsFont.New("Poppins", 7.5f, FontStyle.Italic);
                using var noteBrush = new SolidBrush(Color.FromArgb(234, 179, 8));
                g.DrawString($"  ★ {item.Notes}", noteFont, noteBrush, textX, subY);
            }

            // Bottom divider
            using var divPen = new Pen(Color.FromArgb(40, 40, 40), 1f);
            g.DrawLine(divPen, 6, Height - 1, Width - 6, Height - 1);
        }
    }
}
