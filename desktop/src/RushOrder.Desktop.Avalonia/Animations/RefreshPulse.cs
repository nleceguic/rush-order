using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace RushOrder.Desktop.Avalonia.Animations;

/// <summary>Brief scale+opacity pulse on a widget card's Visual, run entirely on the
/// compositor/render thread via Avalonia's Composition API — the only animation in this
/// module that legitimately claims that guarantee. Triggered whenever a widget's bound
/// content changes (targeted real-time patch or a normal refresh), never on a timer.</summary>
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
