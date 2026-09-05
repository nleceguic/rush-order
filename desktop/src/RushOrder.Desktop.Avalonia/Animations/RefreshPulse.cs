using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace RushOrder.Desktop.Avalonia.Animations;

/// <summary>Brief scale pulse on a widget card's Visual, run entirely on the
/// compositor/render thread via Avalonia's Composition API — the only animation in this
/// module that legitimately claims that guarantee. Triggered by <c>KpiCardBase</c> whenever
/// a widget's <see cref="RushOrder.Desktop.Avalonia.ViewModels.WidgetLoadState"/> transitions
/// to <see cref="RushOrder.Desktop.Avalonia.ViewModels.WidgetLoadState.Loaded"/> — covering
/// the initial load and any full refresh that ends up <c>Loaded</c>. A targeted real-time
/// patch (e.g. <c>Orders.Waiting++</c>) never changes <c>State</c>, so it does not trigger
/// this pulse today — that's a known gap, not yet implemented.</summary>
public static class RefreshPulse
{
    public static void Play(Visual target)
    {
        var elementVisual = ElementComposition.GetElementVisual(target);
        if (elementVisual is null) return; // not yet attached to the visual tree

        var compositor = elementVisual.Compositor;

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Target = "Scale";
        scaleAnim.InsertKeyFrame(0f, new System.Numerics.Vector3(1f, 1f, 1f));
        scaleAnim.InsertKeyFrame(0.5f, new System.Numerics.Vector3(1.03f, 1.03f, 1f));
        scaleAnim.InsertKeyFrame(1f, new System.Numerics.Vector3(1f, 1f, 1f));
        scaleAnim.Duration = TimeSpan.FromMilliseconds(260);

        elementVisual.StartAnimation("Scale", scaleAnim);
    }
}
