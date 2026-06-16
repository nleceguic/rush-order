namespace RushOrder.Desktop.Notifications;

public enum ToastType { Success, Warning, Error, Info }

public sealed class ToastNotificationManager
{
    private const int MaxVisible  = 3;
    private const int ToastWidth  = 320;
    private const int ToastHeight = 68;
    private const int Margin      = 12;

    private readonly Queue<(string Message, ToastType Type, int DurationMs)> _queue = new();
    private readonly List<ToastForm> _visible = [];
    private Form? _owner;

    public void SetOwner(Form owner) => _owner = owner;

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3500)
    {
        if (_owner is null || !_owner.IsHandleCreated) return;

        if (_owner.InvokeRequired)
        {
            _owner.Invoke(() => Show(message, type, durationMs));
            return;
        }

        if (_visible.Count >= MaxVisible)
        {
            _queue.Enqueue((message, type, durationMs));
            return;
        }

        ShowToast(message, type, durationMs);
    }

    private void ShowToast(string message, ToastType type, int durationMs)
    {
        var index  = _visible.Count;
        var screen = Screen.FromControl(_owner!);

        var toast = new ToastForm(message, type, () =>
        {
            // Error toasts only close via the X button (onClose callback)
        });

        PositionToast(toast, index, screen);
        _visible.Add(toast);
        toast.FormClosed += (_, _) => OnToastClosed(toast, screen);
        toast.Show(_owner);

        if (type != ToastType.Error)
        {
            var timer = new System.Windows.Forms.Timer { Interval = durationMs };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!toast.IsDisposed)
                    toast.FadeOut(() => { });
            };
            timer.Start();
        }
    }

    private void OnToastClosed(ToastForm toast, Screen screen)
    {
        _visible.Remove(toast);
        RearrangeToasts(screen);

        if (_queue.Count > 0)
        {
            var (msg, t, dur) = _queue.Dequeue();
            ShowToast(msg, t, dur);
        }
    }

    private void PositionToast(Form toast, int index, Screen screen)
    {
        var wa = screen.WorkingArea;
        toast.Left = wa.Right  - ToastWidth  - Margin;
        toast.Top  = wa.Bottom - (ToastHeight + Margin) * (index + 1);
    }

    private void RearrangeToasts(Screen screen)
    {
        for (int i = 0; i < _visible.Count; i++)
            PositionToast(_visible[i], i, screen);
    }
}
