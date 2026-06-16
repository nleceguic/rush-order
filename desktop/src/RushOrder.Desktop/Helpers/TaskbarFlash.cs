using System.Runtime.InteropServices;

namespace RushOrder.Desktop.Helpers;

internal static class TaskbarFlash
{
    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint   cbSize;
        public IntPtr hwnd;
        public uint   dwFlags;
        public uint   uCount;
        public uint   dwTimeout;
    }

    private const uint FLASHW_STOP      = 0;
    private const uint FLASHW_CAPTION   = 1;
    private const uint FLASHW_TRAY      = 2;
    private const uint FLASHW_ALL       = 3;
    private const uint FLASHW_TIMERNOFG = 12;  // flash until foreground

    public static void Flash(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return;
        var info = new FLASHWINFO
        {
            hwnd    = form.Handle,
            dwFlags = FLASHW_TIMERNOFG,
            uCount  = 0,
            dwTimeout = 0,
        };
        info.cbSize = (uint)Marshal.SizeOf(info);
        FlashWindowEx(ref info);
    }

    public static void Stop(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated) return;
        var info = new FLASHWINFO
        {
            hwnd    = form.Handle,
            dwFlags = FLASHW_STOP,
            uCount  = 0,
            dwTimeout = 0,
        };
        info.cbSize = (uint)Marshal.SizeOf(info);
        FlashWindowEx(ref info);
    }
}
