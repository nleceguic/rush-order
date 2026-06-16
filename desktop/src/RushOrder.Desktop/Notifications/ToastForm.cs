using System.Drawing.Drawing2D;

namespace RushOrder.Desktop.Notifications;

internal sealed class ToastForm : Form
{
    private double _opacity = 0.0;
    private readonly System.Windows.Forms.Timer _fadeInTimer;

    public ToastForm(string message, ToastType type, Action onClose)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        Size            = new Size(320, 68);
        BackColor       = GetBackColor(type);
        Opacity         = 0;

        var pnlAccent = new Panel
        {
            Dock      = DockStyle.Left,
            Width     = 5,
            BackColor = GetAccentColor(type),
        };

        var lblIcon = new Label
        {
            Text      = GetIcon(type),
            Font      = new Font("Segoe UI Symbol", 16f),
            ForeColor = Color.White,
            Size      = new Size(44, 68),
            Location  = new Point(5, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var lblMessage = new Label
        {
            Text      = message,
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.White,
            Size      = new Size(230, 60),
            Location  = new Point(52, 4),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var btnClose = new Label
        {
            Text      = "✕",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(200, 255, 255, 255),
            Size      = new Size(24, 24),
            Location  = new Point(289, 4),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand,
        };
        btnClose.Click += (_, _) => { onClose(); Close(); };

        Controls.AddRange([pnlAccent, lblIcon, lblMessage, btnClose]);

        _fadeInTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeInTimer.Tick += (_, _) =>
        {
            _opacity = Math.Min(1.0, _opacity + 0.12);
            Opacity  = _opacity;
            if (_opacity >= 1.0) _fadeInTimer.Stop();
        };

        Shown += (_, _) => _fadeInTimer.Start();
    }

    public void FadeOut(Action onComplete)
    {
        _fadeInTimer.Stop();
        _opacity = Opacity;
        var fadeOut = new System.Windows.Forms.Timer { Interval = 16 };
        fadeOut.Tick += (_, _) =>
        {
            _opacity = Math.Max(0.0, _opacity - 0.12);
            Opacity  = _opacity;
            if (_opacity <= 0)
            {
                fadeOut.Stop();
                fadeOut.Dispose();
                onComplete();
                if (!IsDisposed) Close();
            }
        };
        fadeOut.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fadeInTimer.Dispose();
        base.Dispose(disposing);
    }

    private static Color GetBackColor(ToastType type) => type switch
    {
        ToastType.Success => Color.FromArgb(27, 94, 32),
        ToastType.Warning => Color.FromArgb(230, 81, 0),
        ToastType.Error   => Color.FromArgb(183, 28, 28),
        _                 => Color.FromArgb(13, 71, 161),
    };

    private static Color GetAccentColor(ToastType type) => type switch
    {
        ToastType.Success => Color.FromArgb(76, 175, 80),
        ToastType.Warning => Color.FromArgb(255, 152, 0),
        ToastType.Error   => Color.FromArgb(244, 67, 54),
        _                 => Color.FromArgb(33, 150, 243),
    };

    private static string GetIcon(ToastType type) => type switch
    {
        ToastType.Success => "✓",
        ToastType.Warning => "⚠",
        ToastType.Error   => "✕",
        _                 => "ℹ",
    };
}
