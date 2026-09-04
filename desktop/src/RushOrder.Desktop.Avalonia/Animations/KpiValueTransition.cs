using Avalonia.Threading;

namespace RushOrder.Desktop.Avalonia.Animations;

/// <summary>Standard (non-Composition) UI-thread value interpolator for KPI counters —
/// see Task 8's technical note for why this is the correct tool here, not Composition
/// API. Drives <paramref name="onTick"/> via the Avalonia UI-thread animation clock at
/// roughly 60 steps over <paramref name="duration"/>, using an ease-out curve so the
/// counter settles rather than ticking linearly.</summary>
public static class KpiValueTransition
{
    public static IDisposable Animate(double from, double to, TimeSpan duration, Action<double> onTick)
    {
        var start = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };

        timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - start;
            var t = Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3);
            onTick(from + (to - from) * eased);
            if (t >= 1) timer.Stop();
        };

        timer.Start();
        return new StopOnDispose(timer);
    }

    private sealed class StopOnDispose(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
