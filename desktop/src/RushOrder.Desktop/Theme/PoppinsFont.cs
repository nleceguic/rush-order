using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;

namespace RushOrder.Desktop.Theme;

/// <summary>
/// Loads the embedded Poppins weights (400/500/600/700, matching the PWA's
/// @fontsource/poppins usage) into a PrivateFontCollection and exposes them
/// as FontFamily/Font factories. AddFontMemResourceEx registers a font for
/// raw GDI but GDI+'s Font(string, ...) name lookup does not see it, so
/// PrivateFontCollection is the officially supported path for embedded fonts.
/// </summary>
internal static class PoppinsFont
{
    private static readonly PrivateFontCollection Collection = new();

    public static FontFamily Regular  { get; }
    public static FontFamily Medium   { get; }
    public static FontFamily SemiBold { get; }

    static PoppinsFont()
    {
        AddEmbedded("Poppins-Regular.ttf");
        AddEmbedded("Poppins-Medium.ttf");
        AddEmbedded("Poppins-SemiBold.ttf");
        AddEmbedded("Poppins-Bold.ttf");

        Regular  = Resolve("Poppins");
        Medium   = Resolve("Poppins Medium");
        SemiBold = Resolve("Poppins SemiBold");
    }

    /// <summary>Touches the static ctor and logs the resolved families; call once at startup after the logger is configured.</summary>
    public static void Initialize()
    {
        Log.Information(
            "PoppinsFont: loaded families = {Families}",
            string.Join(", ", Collection.Families.Select(f => f.Name)));
        Log.Information(
            "PoppinsFont: Regular={Regular} Medium={Medium} SemiBold={SemiBold}",
            Regular.Name, Medium.Name, SemiBold.Name);
    }

    public static Font New(string family, float size) => new(Resolve(family), size);
    public static Font New(string family, float size, FontStyle style) => new(Resolve(family), size, style);

    private static FontFamily Resolve(string family)
    {
        var match = Collection.Families.FirstOrDefault(f =>
            string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        Log.Warning("PoppinsFont: family {Family} not found in PrivateFontCollection, falling back to generic sans-serif", family);
        return FontFamily.GenericSansSerif;
    }

    private static void AddEmbedded(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"RushOrder.Desktop.Assets.Fonts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Log.Warning("PoppinsFont: embedded resource not found: {ResourceName}", resourceName);
            return;
        }

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);

        var handle = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, handle, bytes.Length);
            Collection.AddMemoryFont(handle, bytes.Length);
        }
        finally
        {
            Marshal.FreeCoTaskMem(handle);
        }
    }
}
