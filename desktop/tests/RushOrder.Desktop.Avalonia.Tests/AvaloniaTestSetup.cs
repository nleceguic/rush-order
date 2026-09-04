using Avalonia;
using Avalonia.Headless;
using RushOrder.Desktop.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace RushOrder.Desktop.Avalonia.Tests;

/// <summary>
/// Avalonia-11.3.20-vs-brief gap: <c>KpiValueTransition.Animate</c> (Task 8) drives its
/// counter interpolation via <c>Avalonia.Threading.DispatcherTimer</c>, which only ticks
/// when a real Avalonia platform (with a registered <c>IDispatcherImpl</c>) is running. A
/// bare xunit host has none, so the timer never fires (verified empirically — even a raw
/// DispatcherTimer produces zero ticks after 300ms of real waiting under plain xunit), and
/// any widget-view-model test that asserts an animated value after <c>LoadAsync</c> fails
/// with the property stuck at its initial value.
///
/// A hand-rolled <c>[ModuleInitializer]</c> calling
/// <c>AppBuilder.Configure&lt;App&gt;().UseHeadless(...).SetupWithoutStarting()</c> was
/// tried first and hung the test host indefinitely (headless setup needs to run inside the
/// dispatcher loop it creates, not synchronously before any loop exists). The supported
/// fix is Avalonia's own <c>Avalonia.Headless.XUnit</c> package: it registers a headless
/// platform per test run via <c>[AvaloniaTestApplication]</c> below and pumps the
/// dispatcher for each <c>[AvaloniaFact]</c>-attributed test, so <c>Dispatcher.UIThread</c>
/// — and therefore <c>KpiValueTransition</c> — behaves the same as inside the real running
/// app. No widget view model or animation code is altered; only tests that assert an
/// animated value need <c>[AvaloniaFact]</c> instead of <c>[Fact]</c>.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
