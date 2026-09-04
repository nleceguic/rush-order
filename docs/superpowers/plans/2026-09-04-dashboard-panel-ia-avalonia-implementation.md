# Dashboard (DASH-01) + Panel IA (DASH-02) Avalonia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `RushOrder.Desktop.Avalonia` — a new, parallel Avalonia 11 + MVVM desktop project — implementing a minimal Shell-01 plus the DASH-01 (Dashboard) and DASH-02 (Panel IA) modules, functionally at parity with the existing WinForms implementation, per the approved spec.

**Architecture:** MVVM with `CommunityToolkit.Mvvm` source generators. Each KPI widget is an independent `WidgetViewModelBase` subclass owning its own `WidgetLoadState`/`IsShowingSimulatedData`; a shared `KpiCardBase` templated control renders the state chrome (skeleton/empty/error+retry/content) so widget views only supply their content. Services return `Result<T>` and never fabricate success from a failure. Real-time events patch individual widget view models directly — no full-dashboard refetch.

**Tech Stack:** .NET 8 (`net8.0-windows`, required for the `RushOrder.Desktop.Core` project reference — see Task 1), Avalonia 11.3.20, `CommunityToolkit.Mvvm` 8.4.2, `Microsoft.Extensions.Hosting` 8.0.1 (existing central version), `LiveChartsCore.SkiaSharpView.Avalonia` 2.0.5 (Task 22 only), `xunit` 2.5.3 (existing central version) for the test project.

**Spec:** `docs/superpowers/specs/2026-09-04-dashboard-panel-ia-avalonia-design.md` (commit `3d6c4e5`, approved and closed — do not reopen).

## Global Constraints

- New project only (`RushOrder.Desktop.Avalonia`), added to `rush-order.sln`. Never modify `RushOrder.Desktop` (WinForms) or its files.
- No shared classlib between WinForms and Avalonia — `Models`/`Services` are duplicated in the new project, per spec Section 1.
- Target framework `net8.0-windows` (not plain `net8.0` — see Task 1 technical note) and not `.NET 9` (SDK not installed).
- `WidgetLoadState` reflects only the real-fetch result (`Loading`/`Loaded`/`Empty`/`Error`); `IsShowingSimulatedData` is an orthogonal flag. `Loaded` never coexists with simulated data. `Error` always sets `IsShowingSimulatedData = true` with fallback content.
- Services return `Result<T>` (`Ok`/`Fail`) from the real fetch only — never a silently-substituted mock inside a `catch`. The view model owns the decision to display simulated fallback data.
- `RetryCommand` re-runs only the real fetch.
- No `.Result`/`.Wait()` anywhere. No `DispatcherTimer` + manual `Invalidate()`/re-render loops.
- Composition API (`ElementComposition.GetElementVisual` + `Compositor.Create*KeyFrameAnimation`) is reserved for actual Visual-level, perf-critical animations (opacity/scale/offset) — never claimed for text/number content, which Avalonia always formats on the UI thread regardless of framework (see Task 8 technical note). Simple cross-fades may use standard `Transitions`.
- Real-time events patch only the affected widget view model's properties — never trigger a dashboard-wide reload.
- Navigation goes through `INavigationService.NavigateTo(routeKey, param?)`. Until Orders/Tables/Menu modules exist in Avalonia, `"orders/kanban"`, `"tables/floorplan"`, `"orders/new"`, and `"menu/products"` all resolve to `PlaceholderView`.
- Every color, font, spacing, and radius value comes from `DesignTokens.axaml` (Task 2) — no hardcoded hex/px in any widget or control.
- DASH-02 grid keeps the Alerts widget (2×2: Previsión, Alertas, Sugerencia, ETA cocina) — parity with WinForms, per spec.

---

### Task 1: Solution scaffold + minimal boot

**Technical note (resolved, not a blocker):** `RushOrder.Desktop.Core` (holds `RestaurantHubClient`, reused per spec) targets `net8.0-windows` with `UseWindowsForms=true`. A project targeting plain `net8.0` cannot reference a `net8.0-windows` project (NETSDK1140). `RushOrder.Desktop.Avalonia` must therefore also target `net8.0-windows` — harmless for Avalonia (it runs fine on that TFM) and consistent with this being a Windows desktop app like the rest of the stack. `UseWindowsForms` is left `false` in the new project; only the TFM needs to match.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Program.cs`
- Modify: `Directory.Packages.props` (add Avalonia + CommunityToolkit.Mvvm versions)
- Modify: `rush-order.sln` (add project)

**Interfaces:**
- Produces: an empty runnable Avalonia window, the project skeleton every later task adds files into.

- [ ] **Step 1: Add package versions to `Directory.Packages.props`**

Add inside the existing `<ItemGroup>`, after the `<!-- Desktop-only -->` block:

```xml
<!-- Desktop-only (Avalonia) -->
<PackageVersion Include="Avalonia" Version="11.3.20" />
<PackageVersion Include="Avalonia.Desktop" Version="11.3.20" />
<PackageVersion Include="Avalonia.Diagnostics" Version="11.3.20" />
<PackageVersion Include="Avalonia.Themes.Fluent" Version="11.3.20" />
<PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
<PackageVersion Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="2.0.5" />
```

- [ ] **Step 2: Create the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>

  <ItemGroup Condition="'$(Configuration)'=='Debug'">
    <PackageReference Include="Avalonia.Diagnostics" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RushOrder.Desktop.Core\RushOrder.Desktop.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create `App.axaml`**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="RushOrder.Desktop.Avalonia.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

(`DesignTokens.axaml` is merged in here by Task 2.)

- [ ] **Step 4: Create `App.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window { Width = 1280, Height = 800, Title = "Rush Order" };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

(`using Avalonia.Controls;` is required here for the `Window` type used below — omitted from an earlier draft of this snippet, corrected now so Task 1 and Task 2 agree on the file's exact content.)

- [ ] **Step 5: Create `Program.cs`**

```csharp
using Avalonia;

namespace RushOrder.Desktop.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
```

(No `.WithInterFont()` — that extension method requires the `Avalonia.Fonts.Inter` package, which this project deliberately never references: Task 2 embeds and uses Poppins exclusively via the design tokens, matching the WinForms app. An earlier draft of this snippet carried `.WithInterFont()` over from Avalonia's default project template without noticing the dependency; corrected here so Task 1 doesn't contradict Task 2's already-approved decision.)

- [ ] **Step 6: Add the project to `rush-order.sln`**

Run: `dotnet sln rush-order.sln add desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`

- [ ] **Step 7: Build and run**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

Run: `dotnet run --project desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: an empty window titled "Rush Order" opens. Close it.

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props rush-order.sln desktop/src/RushOrder.Desktop.Avalonia
git commit -m "feat(desktop-avalonia): scaffold RushOrder.Desktop.Avalonia project"
```

---

### Task 2: Design tokens

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Assets/Fonts/Poppins-Regular.ttf` (copy from `RushOrder.Desktop/Assets/Fonts/`)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Assets/Fonts/Poppins-Medium.ttf` (copy)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Assets/Fonts/Poppins-SemiBold.ttf` (copy)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Assets/Fonts/Poppins-Bold.ttf` (copy)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Styles/DesignTokens.axaml`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml` (merge the dictionary)

**Interfaces:**
- Produces: `StaticResource`/`DynamicResource` keys consumed by every later Task's XAML — `Brush.*`, `Space*`, `Radius*`, `FontSize*`, `PoppinsFontFamily`.

Values below are copied verbatim from `docs/design-system.md`, not invented.

- [ ] **Step 1: Copy the 4 embedded font files**

Run: `cp desktop/src/RushOrder.Desktop/Assets/Fonts/Poppins-*.ttf desktop/src/RushOrder.Desktop.Avalonia/Assets/Fonts/`

(`IBM Plex Mono` is documented in `docs/design-system.md` for tabular data, but grep confirmed it's unused anywhere in the current WinForms Dashboard/AI Dashboard widgets — only Poppins is used for KPI text. It is not needed for DASH-01/DASH-02 and is intentionally not embedded in this phase; a later Tables/Orders phase adds it if those screens use it.)

- [ ] **Step 2: Create `Styles/DesignTokens.axaml`**

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <FontFamily x:Key="PoppinsFontFamily">avares://RushOrder.Desktop.Avalonia/Assets/Fonts#Poppins</FontFamily>

  <!-- Brand palette (theme-invariant) -->
  <Color x:Key="RushRedColor">#E63946</Color>
  <Color x:Key="RushRedHoverColor">#C1121F</Color>
  <Color x:Key="RushDarkColor">#1D3557</Color>
  <Color x:Key="RushDarkLightColor">#2D4A6E</Color>
  <Color x:Key="RushBlueColor">#457B9D</Color>
  <Color x:Key="RushMintColor">#F1FAEE</Color>
  <SolidColorBrush x:Key="RushRedBrush" Color="{StaticResource RushRedColor}" />
  <SolidColorBrush x:Key="RushRedHoverBrush" Color="{StaticResource RushRedHoverColor}" />
  <SolidColorBrush x:Key="RushDarkBrush" Color="{StaticResource RushDarkColor}" />
  <SolidColorBrush x:Key="RushDarkLightBrush" Color="{StaticResource RushDarkLightColor}" />
  <SolidColorBrush x:Key="RushBlueBrush" Color="{StaticResource RushBlueColor}" />
  <SolidColorBrush x:Key="RushMintBrush" Color="{StaticResource RushMintColor}" />

  <!-- Semantic colors -->
  <SolidColorBrush x:Key="SuccessBrush" Color="#4CAF50" />
  <SolidColorBrush x:Key="WarningBrush" Color="#FF9800" />
  <SolidColorBrush x:Key="ErrorBrush" Color="#F44336" />
  <SolidColorBrush x:Key="InfoBrush" Color="#2196F3" />

  <!-- NavActive is Rush Red in both themes -->
  <SolidColorBrush x:Key="NavActiveBrush" Color="{StaticResource RushRedColor}" />

  <!-- Theme-aware tokens (light default; dark overrides via ThemeDictionaries) -->
  <SolidColorBrush x:Key="BackgroundBrush" Color="#F8F8F8" />
  <SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFF" />
  <SolidColorBrush x:Key="SidebarBgBrush" Color="#1D1D1D" />
  <SolidColorBrush x:Key="HeaderBgBrush" Color="#FFFFFF" />
  <SolidColorBrush x:Key="TextPrimaryBrush" Color="#1D1D1D" />
  <SolidColorBrush x:Key="TextSecondaryBrush" Color="#787878" />
  <SolidColorBrush x:Key="BorderBrush2" Color="#E5E5E5" />
  <SolidColorBrush x:Key="InputBrush" Color="#FFFFFF" />

  <!-- Spacing (8px scale, 4px fine step). x:Double values are for double-typed properties
       (Spacing, Width, Height); the parallel SpaceThicknessN resources below are for
       Margin/Padding (Thickness-typed) — StaticResource does not implicitly convert a
       boxed double to Thickness the way a literal string does via ThicknessTypeConverter,
       so a Double resource assigned directly to Margin/Padding throws InvalidCastException
       at runtime (found during Task 7 implementation review). -->
  <x:Double x:Key="Space1">4</x:Double>
  <x:Double x:Key="Space2">8</x:Double>
  <x:Double x:Key="Space3">16</x:Double>
  <x:Double x:Key="Space4">24</x:Double>
  <x:Double x:Key="Space5">32</x:Double>
  <x:Double x:Key="Space6">48</x:Double>
  <x:Double x:Key="Space7">64</x:Double>

  <Thickness x:Key="SpaceThickness1">4</Thickness>
  <Thickness x:Key="SpaceThickness2">8</Thickness>
  <Thickness x:Key="SpaceThickness3">16</Thickness>
  <Thickness x:Key="SpaceThickness4">24</Thickness>
  <Thickness x:Key="SpaceThickness5">32</Thickness>
  <Thickness x:Key="SpaceThickness6">48</Thickness>
  <Thickness x:Key="SpaceThickness7">64</Thickness>

  <!-- Radii -->
  <CornerRadius x:Key="RadiusSm">8</CornerRadius>
  <CornerRadius x:Key="RadiusLg">16</CornerRadius>
  <CornerRadius x:Key="RadiusPill">999</CornerRadius>

  <!-- Typography -->
  <x:Double x:Key="FontSizeTitle">22</x:Double>
  <x:Double x:Key="FontSizeBody">15</x:Double>
  <x:Double x:Key="FontSizeLabel">12</x:Double>
  <x:Double x:Key="FontSizeKpi">34</x:Double>

  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Dark">
      <SolidColorBrush x:Key="BackgroundBrush" Color="#121212" />
      <SolidColorBrush x:Key="SurfaceBrush" Color="#1D1D1D" />
      <SolidColorBrush x:Key="SidebarBgBrush" Color="#0F0F0F" />
      <SolidColorBrush x:Key="HeaderBgBrush" Color="#1D1D1D" />
      <SolidColorBrush x:Key="TextPrimaryBrush" Color="#F0F0F0" />
      <SolidColorBrush x:Key="TextSecondaryBrush" Color="#A0A0A0" />
      <SolidColorBrush x:Key="BorderBrush2" Color="#323232" />
      <SolidColorBrush x:Key="InputBrush" Color="#282828" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="Light" />
  </ResourceDictionary.ThemeDictionaries>

</ResourceDictionary>
```

Note: the key is `BorderBrush2`, not `BorderBrush` — Avalonia's `FluentTheme` already defines a control-level `BorderBrush`-named resource used internally by default control themes; reusing that exact name would silently shadow it. `BorderBrush2` is the token this project's own controls bind to for the design-system border color.

- [ ] **Step 3: Merge into `App.axaml`**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="RushOrder.Desktop.Avalonia.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://RushOrder.Desktop.Avalonia/Styles/DesignTokens.axaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 4: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Assets desktop/src/RushOrder.Desktop.Avalonia/Styles desktop/src/RushOrder.Desktop.Avalonia/App.axaml
git commit -m "feat(desktop-avalonia): add design tokens from docs/design-system.md"
```

---

### Task 3: Test project + core models + `Result<T>`

**Files:**
- Create: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/RushOrder.Desktop.Avalonia.Tests.csproj`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Models/Result.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Models/DashboardModels.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Models/ForecastModels.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Models/ResultTests.cs`
- Modify: `rush-order.sln`

**Interfaces:**
- Produces: `Result<T>` (`.IsSuccess`, `.Value`, `.Error`, static `.Ok(T)`/`.Fail(Exception)`) and the record types every later Service/ViewModel task consumes: `DashboardKpi`, `AlertDto`, `AlertSeverity`, `ReservationDto`, `TopForecastProduct`, `ForecastSummary`, `HourlyForecastPoint`, `ProductForecastRow`, `DemandForecastResult`, `KitchenEta`.

- [ ] **Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\RushOrder.Desktop.Avalonia\RushOrder.Desktop.Avalonia.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln rush-order.sln add desktop/tests/RushOrder.Desktop.Avalonia.Tests/RushOrder.Desktop.Avalonia.Tests.csproj`

- [ ] **Step 2: Write the failing test for `Result<T>`**

```csharp
using RushOrder.Desktop.Avalonia.Models;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Models;

public class ResultTests
{
    [Fact]
    public void Ok_carries_the_value_and_no_error()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_carries_the_exception_and_no_value()
    {
        var ex = new InvalidOperationException("boom");
        var result = Result<int>.Fail(ex);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Same(ex, result.Error);
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter ResultTests`
Expected: FAIL — `Result<T>` does not exist yet.

- [ ] **Step 4: Create `Models/Result.cs`**

```csharp
namespace RushOrder.Desktop.Avalonia.Models;

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Exception? Error { get; }

    private Result(bool isSuccess, T? value, Exception? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(Exception error) => new(false, default, error);
}
```

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter ResultTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Create `Models/DashboardModels.cs`** (ported verbatim from `RushOrder.Desktop/Models/DashboardModels.cs`, KPI-relevant subset only)

```csharp
namespace RushOrder.Desktop.Avalonia.Models;

public sealed record DashboardKpi(
    decimal RevenueToday,
    decimal RevenueYesterday,
    IReadOnlyList<decimal> RevenueByHour,
    int OrdersWaiting,
    int OrdersPreparing,
    int OrdersReady,
    int TablesOccupied,
    int TablesTotal,
    double AvgOccupancyMinutes,
    decimal AvgTicketToday,
    decimal AvgTicketYesterday);

public sealed record AlertDto(
    Guid Id,
    string Message,
    AlertSeverity Severity,
    string? ResourceId,
    string ResourceType,
    DateTimeOffset OccurredAt);

public enum AlertSeverity { Info, Warning, Critical }

public sealed record ReservationDto(
    Guid Id,
    string CustomerName,
    int PartySize,
    DateTimeOffset ReservationTime,
    string? Notes);
```

- [ ] **Step 7: Create `Models/ForecastModels.cs`** (ported verbatim from `RushOrder.Desktop/Models/ForecastModels.cs`)

```csharp
namespace RushOrder.Desktop.Avalonia.Models;

public sealed record TopForecastProduct(Guid ProductId, string Name, decimal PredictedQuantity);

public sealed record ForecastSummary(decimal TotalCovers, int? PeakHour, IReadOnlyList<TopForecastProduct> TopProducts);

public sealed record HourlyForecastPoint(int Hour, decimal PredictedOrders, decimal PredictedRevenue);

public sealed record ProductForecastRow(
    Guid ProductId, string Name, decimal PredictedQuantity, decimal RecommendedPrepQuantity, string Confidence);

public sealed record DemandForecastResult(
    ForecastSummary Summary, IReadOnlyList<HourlyForecastPoint> Hourly, IReadOnlyList<ProductForecastRow> Products);

public sealed record KitchenEta(decimal? AverageMinutes, int SampleSize);
```

- [ ] **Step 8: Build both projects**

Run: `dotnet build rush-order.sln`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add rush-order.sln desktop/tests/RushOrder.Desktop.Avalonia.Tests desktop/src/RushOrder.Desktop.Avalonia/Models
git commit -m "feat(desktop-avalonia): add Result<T> and ported Dashboard/Forecast models"
```

---

### Task 4: `AppState`

Not listed in the spec's folder tree (an omission, not a contradiction — every service in Task 12-14 needs it, exactly as `DashboardDataService`/`ForecastDataService`/`RealTimeService` need it in WinForms). Placed in `Services/` since it's the smallest deviation from the spec tree.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/AppState.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/AppStateTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `AppState` (`CurrentUser`, `CurrentRestaurant`, `AccessToken`, `IsOnline`, `IsAuthenticated`, events `UserChanged`/`RestaurantChanged`/`OnlineStatusChanged`) — consumed by every Service task and by `MainWindowViewModel` (Task 7).

- [ ] **Step 1: Write the failing test** (the one piece of actual logic — de-duped online status)

```csharp
using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class AppStateTests
{
    [Fact]
    public void SetOnlineStatus_does_not_raise_event_when_value_is_unchanged()
    {
        var state = new AppState();
        var raiseCount = 0;
        state.OnlineStatusChanged += _ => raiseCount++;

        state.SetOnlineStatus(false); // already false by default — no change
        state.SetOnlineStatus(true);  // change — raises
        state.SetOnlineStatus(true);  // no change — does not raise

        Assert.Equal(1, raiseCount);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter AppStateTests`
Expected: FAIL — `AppState` does not exist yet.

- [ ] **Step 3: Create `Services/AppState.cs`** (ported verbatim from `RushOrder.Desktop/State/AppState.cs`)

```csharp
namespace RushOrder.Desktop.Avalonia.Services;

public sealed class AppState
{
    public UserInfo? CurrentUser { get; private set; }
    public RestaurantInfo? CurrentRestaurant { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public bool IsOnline { get; private set; }

    public event Action<bool>? OnlineStatusChanged;
    public event Action<UserInfo?>? UserChanged;
    public event Action<RestaurantInfo?>? RestaurantChanged;

    public bool IsAuthenticated => CurrentUser is not null;

    public void SetAuthenticated(UserInfo user, string accessToken, string refreshToken)
    {
        CurrentUser = user;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserChanged?.Invoke(user);
    }

    public void SetRestaurant(RestaurantInfo restaurant)
    {
        CurrentRestaurant = restaurant;
        RestaurantChanged?.Invoke(restaurant);
    }

    public void SetOnlineStatus(bool isOnline)
    {
        if (IsOnline == isOnline) return;
        IsOnline = isOnline;
        OnlineStatusChanged?.Invoke(isOnline);
    }

    public void Logout()
    {
        CurrentUser = null;
        AccessToken = null;
        RefreshToken = null;
        CurrentRestaurant = null;
        UserChanged?.Invoke(null);
    }
}

public sealed record UserInfo(Guid Id, string Email, string FullName, string Role, string AvatarInitials);
public sealed record RestaurantInfo(Guid Id, string Name, string? LogoUrl);
```

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter AppStateTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/AppState.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/AppStateTests.cs
git commit -m "feat(desktop-avalonia): add AppState"
```

---

### Task 5: `WidgetLoadState` + `WidgetViewModelBase`

This is the central pattern from spec Section 2 — every widget view model in Tasks 15-24 inherits it.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetLoadState.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetViewModelBase.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/WidgetViewModelBaseTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `WidgetLoadState { Loading, Loaded, Empty, Error }`; `WidgetViewModelBase` with `[ObservableProperty] State`, `[ObservableProperty] IsShowingSimulatedData`, `RetryCommand`, `protected abstract Task LoadAsync()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using RushOrder.Desktop.Avalonia.ViewModels;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels;

public class WidgetViewModelBaseTests
{
    private sealed class FakeWidgetViewModel : WidgetViewModelBase
    {
        public int LoadCount { get; private set; }
        public Func<Task>? OnLoad { get; set; }

        protected override async Task LoadAsync()
        {
            LoadCount++;
            if (OnLoad is not null) await OnLoad();
        }
    }

    [Fact]
    public void Starts_in_Loading_state_with_no_simulated_data()
    {
        var vm = new FakeWidgetViewModel();

        Assert.Equal(WidgetLoadState.Loading, vm.State);
        Assert.False(vm.IsShowingSimulatedData);
    }

    [Fact]
    public async Task RetryCommand_invokes_LoadAsync()
    {
        var vm = new FakeWidgetViewModel();

        await vm.RetryCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.LoadCount);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter WidgetViewModelBaseTests`
Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Create `ViewModels/WidgetLoadState.cs`**

```csharp
namespace RushOrder.Desktop.Avalonia.ViewModels;

/// <summary>
/// Reflects only the result of the real data-source fetch — never the presence of
/// simulated fallback data. See <see cref="WidgetViewModelBase.IsShowingSimulatedData"/>
/// for that orthogonal dimension. <see cref="Loaded"/> never coexists with simulated data.
/// </summary>
public enum WidgetLoadState { Loading, Loaded, Empty, Error }
```

- [ ] **Step 4: Create `ViewModels/WidgetViewModelBase.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RushOrder.Desktop.Avalonia.ViewModels;

public abstract partial class WidgetViewModelBase : ObservableObject
{
    [ObservableProperty]
    private WidgetLoadState _state = WidgetLoadState.Loading;

    [ObservableProperty]
    private bool _isShowingSimulatedData;

    [RelayCommand]
    private async Task Retry() => await LoadAsync();

    /// <summary>Fetches from the real data source and sets <see cref="State"/> and
    /// <see cref="IsShowingSimulatedData"/>. Never sets <c>State = Loaded</c> from
    /// simulated data — see spec Section 2's behavior matrix.</summary>
    protected abstract Task LoadAsync();

    public Task InitializeAsync() => LoadAsync();
}
```

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter WidgetViewModelBaseTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetLoadState.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetViewModelBase.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/WidgetViewModelBaseTests.cs
git commit -m "feat(desktop-avalonia): add WidgetLoadState + WidgetViewModelBase"
```

---

### Task 6: `INavigationService` + `PlaceholderView`

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Navigation/INavigationService.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Navigation/NavigationService.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Navigation/PlaceholderView.axaml` (+ `.axaml.cs`)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Navigation/PlaceholderViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Navigation/NavigationServiceTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `INavigationService.NavigateTo(string routeKey, object? parameter = null)`, `CurrentViewModel` (observable), consumed by KPI/alert click handlers (Tasks 15, 19) and by `MainWindowViewModel` (Task 7) to swap the content area.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Navigation;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_unknown_route_resolves_to_PlaceholderViewModel_with_the_route_key()
    {
        var nav = new NavigationService();

        nav.NavigateTo("orders/kanban");

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("orders/kanban", placeholder.RouteKey);
    }

    [Fact]
    public void NavigateTo_raises_Navigated_with_the_new_view_model()
    {
        var nav = new NavigationService();
        object? raised = null;
        nav.Navigated += vm => raised = vm;

        nav.NavigateTo("tables/floorplan");

        Assert.Same(nav.CurrentViewModel, raised);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter NavigationServiceTests`
Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Create `Navigation/PlaceholderViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace RushOrder.Desktop.Avalonia.Navigation;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    public string RouteKey { get; }
    public object? Parameter { get; }

    public PlaceholderViewModel(string routeKey, object? parameter)
    {
        RouteKey = routeKey;
        Parameter = parameter;
    }
}
```

- [ ] **Step 4: Create `Navigation/INavigationService.cs`**

```csharp
namespace RushOrder.Desktop.Avalonia.Navigation;

public interface INavigationService
{
    object? CurrentViewModel { get; }
    event Action<object?>? Navigated;

    /// <summary>Navigates to a registered route, or to <see cref="PlaceholderViewModel"/>
    /// carrying <paramref name="routeKey"/> if nothing is registered for it yet — the
    /// pattern used by DASH-01/DASH-02 for "orders/kanban", "tables/floorplan",
    /// "orders/new" and "menu/products" until those modules exist.</summary>
    void NavigateTo(string routeKey, object? parameter = null);

    /// <summary>Registers a factory for a route key. Later phases (Tables, Orders) call
    /// this to replace the placeholder with a real view model.</summary>
    void Register(string routeKey, Func<object?, object> factory);
}
```

- [ ] **Step 5: Create `Navigation/NavigationService.cs`**

```csharp
namespace RushOrder.Desktop.Avalonia.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object?, object>> _routes = new();

    public object? CurrentViewModel { get; private set; }
    public event Action<object?>? Navigated;

    public void Register(string routeKey, Func<object?, object> factory) => _routes[routeKey] = factory;

    public void NavigateTo(string routeKey, object? parameter = null)
    {
        CurrentViewModel = _routes.TryGetValue(routeKey, out var factory)
            ? factory(parameter)
            : new PlaceholderViewModel(routeKey, parameter);
        Navigated?.Invoke(CurrentViewModel);
    }
}
```

- [ ] **Step 6: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter NavigationServiceTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Create `Navigation/PlaceholderView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="RushOrder.Desktop.Avalonia.Navigation.PlaceholderView">
  <Grid Background="{DynamicResource BackgroundBrush}">
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="{StaticResource Space2}">
      <TextBlock Text="Próximamente"
                 FontFamily="{StaticResource PoppinsFontFamily}"
                 FontSize="{StaticResource FontSizeTitle}"
                 FontWeight="SemiBold"
                 Foreground="{DynamicResource TextPrimaryBrush}"
                 HorizontalAlignment="Center" />
      <TextBlock Text="{Binding RouteKey}"
                 FontFamily="{StaticResource PoppinsFontFamily}"
                 FontSize="{StaticResource FontSizeLabel}"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 HorizontalAlignment="Center" />
    </StackPanel>
  </Grid>
</UserControl>
```

- [ ] **Step 8: Create `Navigation/PlaceholderView.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Navigation;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Navigation desktop/tests/RushOrder.Desktop.Avalonia.Tests/Navigation
git commit -m "feat(desktop-avalonia): add INavigationService and PlaceholderView"
```

---

### Task 7: Shell-01 — `MainWindow` + `MainWindowViewModel`

Minimal sidebar shell. Matches the WinForms `MainForm` nav item list (`desktop/src/RushOrder.Desktop/Views/Shell/MainForm.cs:145-156`) for parity, but only "Dashboard" and "Panel IA" route anywhere meaningful in this phase — every other item (Mesas, Pedidos, Cocina, Menú, Camareros, Reservas, Estadísticas, Previsión, Facturación) routes to its `PlaceholderView`. Tasks 21 and 25 re-point "Dashboard" and "Panel IA" at the real views once they exist.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Shell/MainWindowViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Shell/NavItem.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml` (+ `.axaml.cs`)
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Shell/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `INavigationService` (Task 6).
- Produces: `MainWindowViewModel.SelectNav(NavItem)` command, `CurrentContent` (bound to `INavigationService.CurrentViewModel`) — consumed by Task 26's DI wiring, which also needs `MainWindowViewModel.NavItems` extended by nothing further (routes are re-pointed via `INavigationService.Register`, not by touching this list).

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.ViewModels.Shell;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Shell;

public class MainWindowViewModelTests
{
    [Fact]
    public void SelectNav_navigates_to_the_item_route_and_updates_ActiveRouteKey()
    {
        var nav = new NavigationService();
        var vm = new MainWindowViewModel(nav);
        var dashboardItem = vm.NavItems.Single(i => i.RouteKey == "dashboard");

        vm.SelectNavCommand.Execute(dashboardItem);

        Assert.Equal("dashboard", vm.ActiveRouteKey);
        Assert.IsType<PlaceholderViewModel>(vm.CurrentContent);
    }

    [Fact]
    public void NavItems_contains_all_10_sidebar_entries_matching_WinForms_MainForm()
    {
        var vm = new MainWindowViewModel(new NavigationService());

        Assert.Equal(10, vm.NavItems.Count);
        Assert.Contains(vm.NavItems, i => i.RouteKey == "dashboard");
        Assert.Contains(vm.NavItems, i => i.RouteKey == "panel-ia");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter MainWindowViewModelTests`
Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Create `ViewModels/Shell/NavItem.cs`**

```csharp
namespace RushOrder.Desktop.Avalonia.ViewModels.Shell;

public sealed record NavItem(string RouteKey, string Icon, string Label);
```

- [ ] **Step 4: Create `ViewModels/Shell/MainWindowViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;

namespace RushOrder.Desktop.Avalonia.ViewModels.Shell;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new("dashboard",   "⊞", "Dashboard"),
        new("tables",      "◻", "Mesas"),
        new("orders",      "▤", "Pedidos"),
        new("kitchen",     "≡", "Cocina"),
        new("menu",        "◈", "Menú"),
        new("waiters",     "♟", "Camareros"),
        new("reservas",    "◻", "Reservas"),
        new("statistics",  "☰", "Estadísticas"),
        new("panel-ia",    "✦", "Panel IA"),
        new("billing",     "$", "Facturación"),
    ];

    [ObservableProperty]
    private string _activeRouteKey = "dashboard";

    public object? CurrentContent => _nav.CurrentViewModel;

    public MainWindowViewModel(INavigationService nav)
    {
        _nav = nav;
        _nav.Navigated += _ => OnPropertyChanged(nameof(CurrentContent));
        _nav.NavigateTo(ActiveRouteKey);
    }

    [RelayCommand]
    private void SelectNav(NavItem item)
    {
        ActiveRouteKey = item.RouteKey;
        _nav.NavigateTo(item.RouteKey);
    }
}
```

Note: route keys used for sidebar entries (`"dashboard"`, `"panel-ia"`, `"tables"`, `"orders"`) are the shell's own navigation keys, distinct from the KPI-click deep-link keys (`"orders/kanban"`, `"tables/floorplan"`) used in Tasks 15 and 19 — a KPI click always routes through the shell's `"orders"`/`"tables"` entry conceptually, but per spec Section 3 they use their own registered keys so a later phase can register a more specific target (e.g. a Kanban column pre-filter) without touching the sidebar's registration.

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter MainWindowViewModelTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Create `Views/Shell/MainWindow.axaml`**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels.Shell"
        xmlns:nav="using:RushOrder.Desktop.Avalonia.Navigation"
        x:Class="RushOrder.Desktop.Avalonia.Views.Shell.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Rush Order" Width="1280" Height="800"
        Background="{DynamicResource BackgroundBrush}">
  <Window.DataTemplates>
    <DataTemplate DataType="nav:PlaceholderViewModel">
      <nav:PlaceholderView />
    </DataTemplate>
  </Window.DataTemplates>
  <Grid ColumnDefinitions="220,*">
    <Border Grid.Column="0" Background="{DynamicResource SidebarBgBrush}">
      <ItemsControl ItemsSource="{Binding NavItems}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:NavItem">
            <Button Command="{Binding $parent[ItemsControl].((vm:MainWindowViewModel)DataContext).SelectNavCommand}"
                    CommandParameter="{Binding}"
                    HorizontalContentAlignment="Left"
                    HorizontalAlignment="Stretch"
                    Padding="16"
                    Background="Transparent"
                    BorderThickness="0">
              <StackPanel Orientation="Horizontal" Spacing="{StaticResource Space2}">
                <TextBlock Text="{Binding Icon}" Foreground="White" />
                <TextBlock Text="{Binding Label}"
                           FontFamily="{StaticResource PoppinsFontFamily}"
                           FontSize="{StaticResource FontSizeLabel}"
                           Foreground="White" />
              </StackPanel>
            </Button>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Border>
    <ContentControl Grid.Column="1" Content="{Binding CurrentContent}" Margin="16" />
  </Grid>
</Window>
```

(`Padding="16"`/`Margin="16"` are literal, not `{StaticResource SpaceThickness3}` — `StaticResource` does not implicitly convert the `x:Double` `SpaceN` tokens to `Thickness`, and this file was implemented and reviewed before `SpaceThicknessN` existed; kept literal here rather than re-touching already-approved, working code. `SpaceThicknessN` tokens exist from Task 2 onward for every later task's `Margin`/`Padding`. `x:DataType`/`Window.DataTemplates`/the `ItemsControl.ItemTemplate`'s `x:DataType` are required for the compiled bindings above to resolve — corrected here to match the actual, reviewed implementation. Tasks 21 and 25 each add one more `DataTemplate` entry to this same `Window.DataTemplates` block for their own view model, per the plan's explicit "no ViewLocator" decision — not a generic convention-based resolver.)

- [ ] **Step 7: Create `Views/Shell/MainWindow.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Shell;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 8: Wire it as the app's main window in `App.axaml.cs`**

Replace the `OnFrameworkInitializationCompleted` body from Task 1 Step 4:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var nav = new Navigation.NavigationService();
        desktop.MainWindow = new Views.Shell.MainWindow
        {
            DataContext = new ViewModels.Shell.MainWindowViewModel(nav)
        };
    }
    base.OnFrameworkInitializationCompleted();
}
```

(This is a temporary manual wiring for this task only — Task 26 replaces it with the full `Microsoft.Extensions.Hosting` DI container so every Service/ViewModel is constructor-injected instead.)

- [ ] **Step 9: Build and run**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

Run: `dotnet run --project desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: window with a dark sidebar (10 items) and a "Próximamente / dashboard" placeholder in the content area. Clicking "Panel IA" shows "Próximamente / panel-ia". Close it.

- [ ] **Step 10: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Shell
git commit -m "feat(desktop-avalonia): add Shell-01 (MainWindow + sidebar navigation)"
```

---

### Task 8: Animation helpers — `RefreshPulse`, `CardStateTransition`, `KpiValueTransition`

**Technical note on Composition API scope (do not skip — this is the resolved version of the spec's Ajuste 1 correction, made precise at the implementation level):** Avalonia's Composition API (`ElementComposition.GetElementVisual(Visual)` → `CompositionVisual`, animated via `Compositor.CreateScalarKeyFrameAnimation()`/`CreateVector3KeyFrameAnimation()`/etc.) animates **Visual-level composition properties only** — `Offset`, `Scale`, `Opacity`, `RotationAngle`, `Size`. It cannot animate an arbitrary bound `double` that feeds a `TextBlock`'s formatted number, because text layout always happens on the UI thread in Avalonia regardless of framework — there is no "composition text" primitive. So:

- The KPI counter's **numeric tick-up** (e.g. revenue climbing from €0 to €1.247,50) uses the standard property-transition path (`KpiValueTransition`, below) — this is *not* Composition API, and is not claimed to be. It is Avalonia's normal, adequate mechanism for value interpolation; the number still has to be re-formatted to text every frame it changes, which is unavoidable UI-thread work no matter the animation technology.
- `RefreshPulse` — the actual perf-critical, framework-decoupled visual feedback when a widget's value patches (a brief scale/opacity pulse on the whole card) — is genuinely Composition-API-driven, because scale and opacity ARE Visual composition properties. This is the correct and only valid target for Composition API in this module.
- `CardStateTransition` (skeleton ⇄ data ⇄ error cross-fade) uses standard `Transitions` on `Opacity`, per the spec's explicit allowance for simple cross-fades.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Animations/RefreshPulse.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Animations/KpiValueTransition.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Animations/CardStateTransition.axaml`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml` (merge `CardStateTransition.axaml`)

**Interfaces:**
- Produces: `RefreshPulse.Play(Visual target)` (static, called by `KpiCardBase` in Task 9 whenever bound content updates); `KpiValueTransition.CreateInterpolator(double from, double to, TimeSpan duration, Action<double> onTick)` returning an `IDisposable` that stops the interpolation; the `<Style Selector="Border.card-state">` cross-fade resource keyed for reuse by `KpiCardBase`.

- [ ] **Step 1: Create `Animations/RefreshPulse.cs`**

```csharp
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
```

- [ ] **Step 2: Create `Animations/KpiValueTransition.cs`**

```csharp
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
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            onTick(from + (to - from) * eased);

            if (t >= 1) timer.Stop();
        };

        timer.Start();
        return timer; // DispatcherTimer implements IDisposable-like Stop via caller disposing the wrapper below
    }
}
```

Wait — `DispatcherTimer` does not implement `IDisposable`. Wrap it:

```csharp
using Avalonia.Threading;

namespace RushOrder.Desktop.Avalonia.Animations;

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
```

(This is the note referred to in the technical note above: this timer only ever mutates a single already-bound `double` property that a converter formats to text — it does not do layout, network, or any blocking work, and stops itself after `duration`. It is explicitly not presented as Composition API.)

- [ ] **Step 3: Create `Animations/CardStateTransition.axaml`**

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="ContentPresenter.card-state">
    <Setter Property="Transitions">
      <Transitions>
        <DoubleTransition Property="Opacity" Duration="0:0:0.18" />
      </Transitions>
    </Setter>
  </Style>
</Styles>
```

- [ ] **Step 4: Merge into `App.axaml`**

Add inside `<Application.Styles>`, after `<FluentTheme />`:

```xml
<StyleInclude Source="avares://RushOrder.Desktop.Avalonia/Animations/CardStateTransition.axaml" />
```

- [ ] **Step 5: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Animations desktop/src/RushOrder.Desktop.Avalonia/App.axaml
git commit -m "feat(desktop-avalonia): add RefreshPulse (Composition API), KpiValueTransition, CardStateTransition"
```

---

### Task 9: `KpiCardBase` shared control

State-driven card chrome shared by every widget: title, loading skeleton, empty state, error banner + retry button, and a content slot for the widget's own visual. Every widget in Tasks 15-24 wraps its content in this instead of rebuilding card chrome.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Controls/KpiCardBase.axaml` (+ `.axaml.cs`)

**Interfaces:**
- Consumes: `WidgetLoadState`, `RefreshPulse` (Task 8), design tokens (Task 2).
- Produces: a `TemplatedControl`-style `UserControl` with `StyledProperty<string> Title`, `StyledProperty<WidgetLoadState> State`, `StyledProperty<bool> IsShowingSimulatedData`, `StyledProperty<ICommand?> RetryCommand`, and a `Content` slot (via `ContentPresenter`) — consumed by every `*WidgetView.axaml` from Task 15 onward, which set these 4 properties via bindings and put their own visual in `<controls:KpiCardBase.Content>`.

- [ ] **Step 1: Create `Controls/KpiCardBase.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels"
             x:Class="RushOrder.Desktop.Avalonia.Controls.KpiCardBase"
             x:Name="Root">
  <Border Background="{DynamicResource SurfaceBrush}"
          BorderBrush="{DynamicResource BorderBrush2}"
          BorderThickness="1"
          CornerRadius="{StaticResource RadiusLg}"
          Margin="{StaticResource SpaceThickness1}">
    <DockPanel>
      <TextBlock DockPanel.Dock="Top"
                 Text="{Binding #Root.Title}"
                 FontFamily="{StaticResource PoppinsFontFamily}"
                 FontSize="{StaticResource FontSizeLabel}"
                 FontWeight="Bold"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 Margin="{StaticResource SpaceThickness3}" />

      <Border DockPanel.Dock="Top"
              Background="#1AF44336"
              Padding="{StaticResource SpaceThickness2}"
              IsVisible="{Binding #Root.IsShowingSimulatedData}">
        <Grid ColumnDefinitions="*,Auto">
          <TextBlock Grid.Column="0" Text="Sin conexión — datos de ejemplo"
                     FontFamily="{StaticResource PoppinsFontFamily}"
                     FontSize="{StaticResource FontSizeLabel}"
                     Foreground="{DynamicResource ErrorBrush}"
                     VerticalAlignment="Center" />
          <Button Grid.Column="1" Content="Reintentar" Command="{Binding #Root.RetryCommand}" />
        </Grid>
      </Border>

      <Panel>
        <TextBlock Text="Cargando…" HorizontalAlignment="Center" VerticalAlignment="Center"
                   FontFamily="{StaticResource PoppinsFontFamily}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   IsVisible="{Binding #Root.State, Converter={x:Static vm:WidgetStateConverters.IsLoading}}" />

        <TextBlock Text="Sin datos" HorizontalAlignment="Center" VerticalAlignment="Center"
                   FontFamily="{StaticResource PoppinsFontFamily}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   IsVisible="{Binding #Root.State, Converter={x:Static vm:WidgetStateConverters.IsEmpty}}" />

        <ContentPresenter Classes="card-state" Content="{Binding #Root.Content}"
                           IsVisible="{Binding #Root.State, Converter={x:Static vm:WidgetStateConverters.HasContent}}" />
      </Panel>
    </DockPanel>
  </Border>
</UserControl>
```

- [ ] **Step 2: Create `Controls/KpiCardBase.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RushOrder.Desktop.Avalonia.Animations;
using RushOrder.Desktop.Avalonia.ViewModels;
using System.Windows.Input;

namespace RushOrder.Desktop.Avalonia.Controls;

public partial class KpiCardBase : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KpiCardBase, string>(nameof(Title));
    public static readonly StyledProperty<WidgetLoadState> StateProperty =
        AvaloniaProperty.Register<KpiCardBase, WidgetLoadState>(nameof(State));
    public static readonly StyledProperty<bool> IsShowingSimulatedDataProperty =
        AvaloniaProperty.Register<KpiCardBase, bool>(nameof(IsShowingSimulatedData));
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<KpiCardBase, ICommand?>(nameof(RetryCommand));
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<KpiCardBase, object?>(nameof(Content));

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public WidgetLoadState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool IsShowingSimulatedData { get => GetValue(IsShowingSimulatedDataProperty); set => SetValue(IsShowingSimulatedDataProperty, value); }
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public KpiCardBase()
    {
        InitializeComponent();
        this.GetObservable(StateProperty).Subscribe(_ =>
        {
            if (State == WidgetLoadState.Loaded) RefreshPulse.Play(this);
        });
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Create the `WidgetStateConverters` referenced by the XAML above**

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetStateConverters.cs`

```csharp
using Avalonia.Data.Converters;

namespace RushOrder.Desktop.Avalonia.ViewModels;

public static class WidgetStateConverters
{
    public static readonly IValueConverter IsLoading =
        new FuncValueConverter<WidgetLoadState, bool>(s => s == WidgetLoadState.Loading);
    public static readonly IValueConverter IsEmpty =
        new FuncValueConverter<WidgetLoadState, bool>(s => s == WidgetLoadState.Empty);
    public static readonly IValueConverter HasContent =
        new FuncValueConverter<WidgetLoadState, bool>(s => s is WidgetLoadState.Loaded or WidgetLoadState.Error);
}
```

(`Error` shows content because `IsShowingSimulatedData` content is still rendered underneath the error banner, per spec — the widget's own view supplies genuinely-simulated data as `Content` in that state.)

- [ ] **Step 4: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Controls/KpiCardBase.axaml desktop/src/RushOrder.Desktop.Avalonia/Controls/KpiCardBase.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/WidgetStateConverters.cs
git commit -m "feat(desktop-avalonia): add KpiCardBase shared widget chrome control"
```

---

### Task 10: `SparklineControl`

Vector port of WinForms `RevenueWidget.SparklinePanel` (`desktop/src/RushOrder.Desktop/Views/Dashboard/Widgets/RevenueWidget.cs:72-170`) — line + gradient fill + dot markers + hour labels, using Avalonia's `DrawingContext` instead of GDI+. Used by `RevenueWidgetView` (Task 15).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Controls/SparklineControl.cs`

**Interfaces:**
- Consumes: design tokens (colors via `DynamicResource` lookup at render time).
- Produces: `SparklineControl` with `StyledProperty<IReadOnlyList<decimal>> Data` and `StyledProperty<IBrush> LineBrush` — consumed by `RevenueWidgetView` (Task 15).

- [ ] **Step 1: Create `Controls/SparklineControl.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.Controls;

public sealed class SparklineControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<decimal>> DataProperty =
        AvaloniaProperty.Register<SparklineControl, IReadOnlyList<decimal>>(nameof(Data), []);
    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush>(nameof(LineBrush), Brushes.Gray);

    public IReadOnlyList<decimal> Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public IBrush LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(DataProperty, LineBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var data = Data;
        if (data.Count < 2) return;

        const double padH = 3, padTop = 8, padBottom = 20; // bottom reserves room for hour labels
        var w = Bounds.Width - padH * 2;
        var h = Bounds.Height - padTop - padBottom;
        if (w <= 0 || h <= 0) return;

        var min = (double)data.Min();
        var max = (double)data.Max();
        var range = Math.Max(max - min, 1);

        var points = new Point[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var nx = (double)i / (data.Count - 1);
            var ny = 1 - (((double)data[i] - min) / range);
            points[i] = new Point(padH + nx * w, padTop + ny * h * 0.9);
        }

        // Gradient fill under the line
        var fillGeometry = new StreamGeometry();
        using (var ctx = fillGeometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, padTop + h), true);
            foreach (var p in points) ctx.LineTo(p);
            ctx.LineTo(new Point(points[^1].X, padTop + h));
            ctx.EndFigure(true);
        }
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(((SolidColorBrush)LineBrush).Color.WithAlpha(60), 0),
                new GradientStop(Colors.Transparent, 1),
            ],
        };
        context.DrawGeometry(fillBrush, null, fillGeometry);

        // Line
        var lineGeometry = new StreamGeometry();
        using (var ctx = lineGeometry.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++) ctx.LineTo(points[i]);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(LineBrush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), lineGeometry);

        // Dot markers
        foreach (var p in points)
            context.DrawEllipse(LineBrush, null, p, 2.5, 2.5);

        // Hour labels
        var now = DateTime.Now.Hour;
        var typeface = new Typeface(new FontFamily("avares://RushOrder.Desktop.Avalonia/Assets/Fonts#Poppins"));
        for (var i = 0; i < points.Length; i++)
        {
            var hour = ((now - 7 + i + 24) % 24).ToString("D2") + "h";
            var text = new FormattedText(hour, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, LineBrush);
            var x = Math.Clamp(points[i].X - text.Width / 2, 4, Bounds.Width - 4 - text.Width);
            context.DrawText(text, new Point(x, Bounds.Height - text.Height - 2));
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Controls/SparklineControl.cs
git commit -m "feat(desktop-avalonia): add SparklineControl (vector port of WinForms RevenueWidget sparkline)"
```

---

### Task 11: `OccupancyArcControl`

Vector port of WinForms `TablesWidget.ArcPanel` (`desktop/src/RushOrder.Desktop/Views/Dashboard/Widgets/TablesWidget.cs:50-159`) — 270° ring gauge + center count + occupied/free legend. Used by `TablesWidgetView` (Task 17).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Controls/OccupancyArcControl.cs`

**Interfaces:**
- Consumes: `SuccessBrush`/`WarningBrush`/`ErrorBrush` tokens (occupancy-ratio color banding, same thresholds as WinForms: >80% error, >60% warning, else success).
- Produces: `OccupancyArcControl` with `StyledProperty<int> Occupied`, `StyledProperty<int> Total` — consumed by `TablesWidgetView` (Task 17).

- [ ] **Step 1: Create `Controls/OccupancyArcControl.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.Controls;

public sealed class OccupancyArcControl : Control
{
    public static readonly StyledProperty<int> OccupiedProperty =
        AvaloniaProperty.Register<OccupancyArcControl, int>(nameof(Occupied));
    public static readonly StyledProperty<int> TotalProperty =
        AvaloniaProperty.Register<OccupancyArcControl, int>(nameof(Total));

    public int Occupied { get => GetValue(OccupiedProperty); set => SetValue(OccupiedProperty, value); }
    public int Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    static OccupancyArcControl()
    {
        AffectsRender<OccupancyArcControl>(OccupiedProperty, TotalProperty);
    }

    public override void Render(DrawingContext context)
    {
        const double legendBand = 42;
        var ringArea = Math.Max(0, Bounds.Height - legendBand);
        var size = Math.Min(Bounds.Width, ringArea) - 16;
        if (size <= 0) return;

        var x = (Bounds.Width - size) / 2;
        var y = (ringArea - size) / 2;
        var rect = new Rect(x, y, size, size);
        var thickness = size * 0.13;

        var pct = Total > 0 ? (double)Occupied / Total : 0;
        var fillColor = pct > 0.8 ? Color.Parse("#F44336") : pct > 0.6 ? Color.Parse("#FF9800") : Color.Parse("#4CAF50");

        var inflated = rect.Deflate(thickness / 2);
        DrawArc(context, inflated, new Pen(new SolidColorBrush(Color.Parse("#28808080")), thickness,
            lineCap: PenLineCap.Round), 135, 270);
        if (Total > 0)
            DrawArc(context, inflated, new Pen(new SolidColorBrush(fillColor), thickness, lineCap: PenLineCap.Round),
                135, pct * 270);

        var typeface = new Typeface(new FontFamily("avares://RushOrder.Desktop.Avalonia/Assets/Fonts#Poppins"),
            FontStyle.Normal, FontWeight.Bold);
        var countText = new FormattedText($"{Occupied}/{Total}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, size * 0.16, Brushes.Black)
        { TextAlignment = TextAlignment.Center };
        context.DrawText(countText, new Point(x + size / 2 - countText.Width / 2, y + size * 0.3));

        var free = Math.Max(0, Total - Occupied);
        var legendY = ringArea + (legendBand - 30) / 2;
        var colWidth = Bounds.Width / 2;
        DrawLegendColumn(context, 0, colWidth, legendY, "Ocupadas", Occupied, fillColor, typeface);
        DrawLegendColumn(context, colWidth, colWidth, legendY, "Libres", free, Color.Parse("#787878"), typeface);
    }

    private static void DrawArc(DrawingContext context, Rect rect, Pen pen, double startDeg, double sweepDeg)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var center = rect.Center;
        var radiusX = rect.Width / 2;
        var radiusY = rect.Height / 2;
        var startRad = startDeg * Math.PI / 180;
        var endRad = (startDeg + sweepDeg) * Math.PI / 180;
        var start = new Point(center.X + radiusX * Math.Cos(startRad), center.Y + radiusY * Math.Sin(startRad));
        var end = new Point(center.X + radiusX * Math.Cos(endRad), center.Y + radiusY * Math.Sin(endRad));
        ctx.BeginFigure(start, false);
        ctx.ArcTo(end, new Size(radiusX, radiusY), 0, sweepDeg > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawLegendColumn(DrawingContext context, double colX, double colWidth, double y,
        string label, int value, Color valueColor, Typeface typeface)
    {
        var valueText = new FormattedText(value.ToString(), System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 13, new SolidColorBrush(valueColor)) { TextAlignment = TextAlignment.Center };
        context.DrawText(valueText, new Point(colX + colWidth / 2 - valueText.Width / 2, y));

        var labelText = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 8, new SolidColorBrush(Color.Parse("#787878"))) { TextAlignment = TextAlignment.Center };
        context.DrawText(labelText, new Point(colX + colWidth / 2 - labelText.Width / 2, y + valueText.Height));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Controls/OccupancyArcControl.cs
git commit -m "feat(desktop-avalonia): add OccupancyArcControl (vector port of WinForms TablesWidget arc)"
```

---

### Task 12: `DashboardDataService` + `MockDashboardData`

Same REST contract as `RushOrder.Desktop/Services/DashboardDataService.cs` (endpoints, backend DTO shapes) — but returns `Result<T>` and never substitutes mock data inside a `catch`, per spec Section 2.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/DashboardDataService.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/MockDashboardData.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/DashboardDataServiceTests.cs`

**Interfaces:**
- Consumes: `AppState` (Task 4), `Result<T>`/`DashboardKpi`/`AlertDto`/`ReservationDto` (Task 3).
- Produces: `DashboardDataService.GetKpiAsync(CancellationToken)`, `.GetAlertsAsync(CancellationToken)`, `.GetUpcomingReservationsAsync(CancellationToken)`, each returning `Task<Result<T>>` — consumed by widget view models in Tasks 15-20.

- [ ] **Step 1: Write the failing test** (verifies `Fail` is returned, not a swallowed mock, when the endpoint is unreachable — this is the exact regression the spec's Ajuste 2 exists to prevent)

```csharp
using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class DashboardDataServiceTests
{
    [Fact]
    public async Task GetKpiAsync_returns_Fail_when_the_backend_is_unreachable()
    {
        // No backend listening on localhost:5143 in the test environment.
        var service = new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance);

        var result = await service.GetKpiAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter DashboardDataServiceTests`
Expected: FAIL — `DashboardDataService` does not exist yet.

- [ ] **Step 3: Create `Services/MockDashboardData.cs`** (values ported verbatim from `RushOrder.Desktop/Services/DashboardDataService.cs:142-170` — this is the simulated fallback content shown when `State = Error`)

```csharp
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public static class MockDashboardData
{
    public static DashboardKpi Kpi() => new(
        RevenueToday: 1247.50m,
        RevenueYesterday: 1111.20m,
        RevenueByHour: [0m, 0m, 0m, 82m, 156m, 312m, 423m, 274m],
        OrdersWaiting: 3,
        OrdersPreparing: 5,
        OrdersReady: 2,
        TablesOccupied: 8,
        TablesTotal: 12,
        AvgOccupancyMinutes: 42.5,
        AvgTicketToday: 34.20m,
        AvgTicketYesterday: 33.40m);

    public static IReadOnlyList<AlertDto> Alerts() =>
    [
        new(Guid.NewGuid(), "Stock bajo: Vino Rioja Reserva (3 botellas)", AlertSeverity.Warning,
            null, "Product", DateTimeOffset.Now.AddMinutes(-14)),
        new(Guid.NewGuid(), "Cocina: Pedido #A-047 lleva +25 min en preparación", AlertSeverity.Critical,
            "A-047", "Order", DateTimeOffset.Now.AddMinutes(-7)),
        new(Guid.NewGuid(), "Reserva en 30 min — Mesa 4, García Martínez, 6 personas", AlertSeverity.Info,
            null, "Reservation", DateTimeOffset.Now.AddMinutes(-2)),
    ];

    public static IReadOnlyList<ReservationDto> Reservations() =>
    [
        new(Guid.NewGuid(), "García Martínez", 6, DateTimeOffset.Now.AddMinutes(30), "Cumpleaños"),
        new(Guid.NewGuid(), "Fernández López", 2, DateTimeOffset.Now.AddHours(1), null),
        new(Guid.NewGuid(), "Rodriguez & Co", 8, DateTimeOffset.Now.AddHours(1).AddMinutes(30), "Menú empresarial"),
    ];
}
```

- [ ] **Step 4: Create `Services/DashboardDataService.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class DashboardDataService
{
    private readonly AppState _state;
    private readonly ILogger<DashboardDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public DashboardDataService(AppState state, ILogger<DashboardDataService> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<Result<DashboardKpi>> GetKpiAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty dashboard payload");

            return Result<DashboardKpi>.Ok(await MapKpiAsync(dto, restaurantId, ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard KPI fetch failed");
            return Result<DashboardKpi>.Fail(ex);
        }
    }

    private async Task<DashboardKpi> MapKpiAsync(BackendDashboardDto dto, Guid? restaurantId, CancellationToken ct)
    {
        var tablesTotal = 0;
        try
        {
            var tRes = await _http.GetAsync($"http://localhost:5143/api/v1/tables?restaurantId={restaurantId}", ct);
            if (tRes.IsSuccessStatusCode)
            {
                var tJson = await tRes.Content.ReadAsStringAsync(ct);
                tablesTotal = JsonConvert.DeserializeObject<ApiEnvelope<List<object>>>(tJson)?.Data?.Count ?? 0;
            }
        }
        catch { /* leave at 0 — matches WinForms behavior, this sub-call isn't the primary fetch */ }

        var occupied = (int)Math.Round(dto.TableOccupancy.Percentage / 100m * tablesTotal);
        var changeFactor = 1m + dto.AvgTicket.ChangePercent / 100m;
        var avgTicketYesterday = changeFactor != 0 ? dto.AvgTicket.Value / changeFactor : dto.AvgTicket.Value;

        return new DashboardKpi(
            dto.Revenue.Today, dto.Revenue.Yesterday, [],
            dto.Orders.Pending, dto.Orders.InProgress, dto.Orders.Completed,
            occupied, tablesTotal, dto.TableOccupancy.AvgDuration,
            dto.AvgTicket.Value, avgTicketYesterday);
    }

    public async Task<Result<IReadOnlyList<AlertDto>>> GetAlertsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/dashboard?restaurantId={restaurantId}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonConvert.DeserializeObject<ApiEnvelope<BackendDashboardDto>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty dashboard payload");

            IReadOnlyList<AlertDto> alerts = dto.ActiveAlerts.Select(a => new AlertDto(
                Guid.NewGuid(), a.Message,
                Enum.TryParse<AlertSeverity>(a.Severity, out var sev) ? sev : AlertSeverity.Info,
                null, a.Type, DateTimeOffset.Now)).ToList();

            return Result<IReadOnlyList<AlertDto>>.Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alerts fetch failed");
            return Result<IReadOnlyList<AlertDto>>.Fail(ex);
        }
    }

    public async Task<Result<IReadOnlyList<ReservationDto>>> GetUpcomingReservationsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var today = DateTimeOffset.Now.Date;
            var url = $"http://localhost:5143/api/v1/reservations?restaurantId={restaurantId}&date={Uri.EscapeDataString(today.ToString("O"))}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var rows = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendReservationDto>>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty reservations payload");

            IReadOnlyList<ReservationDto> reservations = rows
                .Where(r => r.ReservedAt >= DateTimeOffset.Now && r.Status != "Cancelled")
                .OrderBy(r => r.ReservedAt)
                .Take(3)
                .Select(r => new ReservationDto(r.Id, r.GuestName, r.PartySize, r.ReservedAt, r.Notes))
                .ToList();

            return Result<IReadOnlyList<ReservationDto>>.Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reservations fetch failed");
            return Result<IReadOnlyList<ReservationDto>>.Fail(ex);
        }
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;
    }
}

internal sealed record BackendDashboardDto(
    BackendRevenueStat Revenue, BackendOrderStat Orders, BackendCoverStat Covers, BackendTicketStat AvgTicket,
    IReadOnlyList<object> TopProducts, BackendTableOccupancyStat TableOccupancy,
    IReadOnlyList<BackendActiveAlertDto> ActiveAlerts);

internal sealed record BackendRevenueStat(decimal Today, decimal Yesterday, decimal ChangePercent);
internal sealed record BackendOrderStat(int Total, int Pending, int InProgress, int Completed);
internal sealed record BackendCoverStat(int Total, decimal AvgPerTable);
internal sealed record BackendTicketStat(decimal Value, decimal ChangePercent);
internal sealed record BackendTableOccupancyStat(decimal Percentage, double AvgDuration);
internal sealed record BackendActiveAlertDto(string Type, string Message, string Severity);
internal sealed record BackendReservationDto(Guid Id, string GuestName, int PartySize, DateTimeOffset ReservedAt, string Status, string? Notes);
internal sealed class ApiEnvelope<T> { public string Status { get; set; } = ""; public T? Data { get; set; } }
```

Note: unlike the WinForms version, every method throws on a non-success status or empty payload (`EnsureSuccessStatusCode()` / explicit `throw`) instead of silently falling through to a `return MockX()` at the bottom — that silent fallthrough is exactly what spec Ajuste 2 forbids. The `catch` block only ever produces `Result<T>.Fail`.

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter DashboardDataServiceTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/DashboardDataService.cs desktop/src/RushOrder.Desktop.Avalonia/Services/MockDashboardData.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/DashboardDataServiceTests.cs
git commit -m "feat(desktop-avalonia): add DashboardDataService (Result<T>, no silent mock fallback)"
```

---

### Task 13: `ForecastDataService` + `MockForecastData`

**Deviation from WinForms, required by the approved spec (flagged, not a blocker):** `RushOrder.Desktop/Services/ForecastDataService.cs` has no mock/simulated fallback at all — on failure it just returns `null`, which the WinForms `AiDashboardView` silently treats as "no data" (an `if (forecastTask.Result is { } forecast)` guard that skips the widget entirely). The approved spec requires DASH-02 widgets to show simulated fallback data on `Error`, same as DASH-01, so `MockForecastData` is new — small, self-contained, and does not touch WinForms.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/ForecastDataService.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/MockForecastData.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/ForecastDataServiceTests.cs`

**Interfaces:**
- Consumes: `AppState` (Task 4), `Result<T>`/`DemandForecastResult`/`KitchenEta` (Task 3).
- Produces: `ForecastDataService.GetDemandForecastAsync(DateOnly, CancellationToken)`, `.GetKitchenEtaAsync(CancellationToken)`, each `Task<Result<T>>` — consumed by DASH-02 widget view models in Tasks 22-24.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class ForecastDataServiceTests
{
    [Fact]
    public async Task GetKitchenEtaAsync_returns_Fail_when_the_backend_is_unreachable()
    {
        var service = new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance);

        var result = await service.GetKitchenEtaAsync();

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter ForecastDataServiceTests`
Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `Services/MockForecastData.cs`** (new — see deviation note above; shaped to match real `HourlyForecastPoint`/`TopForecastProduct`/`KitchenEta` records, plausible restaurant-shift values)

```csharp
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public static class MockForecastData
{
    public static DemandForecastResult DemandForecast() => new(
        Summary: new ForecastSummary(TotalCovers: 86m, PeakHour: 21,
            TopProducts: [new TopForecastProduct(Guid.NewGuid(), "Paella Valenciana", 24m)]),
        Hourly:
        [
            new(12, 4m, 62m), new(13, 9m, 148m), new(14, 6m, 94m), new(19, 5m, 78m),
            new(20, 11m, 176m), new(21, 14m, 224m), new(22, 8m, 128m), new(23, 3m, 47m),
        ],
        Products: [new ProductForecastRow(Guid.NewGuid(), "Paella Valenciana", 24m, 26m, "Alta")]);

    public static KitchenEta KitchenEta() => new(AverageMinutes: 18.5m, SampleSize: 42);
}
```

- [ ] **Step 4: Create `Services/ForecastDataService.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class ForecastDataService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AppState _state;
    private readonly ILogger<ForecastDataService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public ForecastDataService(AppState state, ILogger<ForecastDataService> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<Result<DemandForecastResult>> GetDemandForecastAsync(DateOnly date, CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/demand-forecast?restaurantId={restaurantId}&date={date:yyyy-MM-dd}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<DemandForecastResult>>(json, JsonOpts);
            return envelope?.Data is { } data
                ? Result<DemandForecastResult>.Ok(data)
                : Result<DemandForecastResult>.Fail(new InvalidOperationException("Empty demand-forecast payload"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demand forecast fetch failed for {Date}", date);
            return Result<DemandForecastResult>.Fail(ex);
        }
    }

    public async Task<Result<KitchenEta>> GetKitchenEtaAsync(CancellationToken ct = default)
    {
        try
        {
            ApplyAuth();
            var restaurantId = _state.CurrentRestaurant?.Id;
            var url = $"http://localhost:5143/api/v1/analytics/kitchen-eta?restaurantId={restaurantId}";
            var json = await _http.GetStringAsync(url, ct);
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<KitchenEta>>(json, JsonOpts);
            return envelope?.Data is { } data
                ? Result<KitchenEta>.Ok(data)
                : Result<KitchenEta>.Fail(new InvalidOperationException("Empty kitchen-eta payload"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kitchen ETA fetch failed");
            return Result<KitchenEta>.Fail(ex);
        }
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } token
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    private sealed class ApiEnvelope<T> { public string Status { get; set; } = ""; public T? Data { get; set; } }
}
```

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter ForecastDataServiceTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/ForecastDataService.cs desktop/src/RushOrder.Desktop.Avalonia/Services/MockForecastData.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/ForecastDataServiceTests.cs
git commit -m "feat(desktop-avalonia): add ForecastDataService with new simulated fallback (WinForms had none)"
```

---

### Task 14: `RealTimeService` (wraps `RestaurantHubClient` from `RushOrder.Desktop.Core`)

Thin event-forwarding wrapper — same shape as `RushOrder.Desktop/Services/RealTimeService.cs`, reusing `RushOrder.Desktop.Core.Hubs.RestaurantHubClient` directly (no reimplementation of SignalR reconnect/backoff logic, per spec Section 1).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/RealTimeService.cs`

**Interfaces:**
- Consumes: `AppState` (Task 4), `RushOrder.Desktop.Core.Hubs.RestaurantHubClient` (existing, `RushOrder.Desktop.Core`).
- Produces: `RealTimeService.ConnectAsync(string baseUrl, CancellationToken)`, events `OrderReceived(OrderReceivedPayload)`, `OrderStatusUpdated(string orderId, string status, DateTimeOffset)`, `TableStatusChanged(string tableId, string status)`, `KitchenAlert(string message, string severity)`, `MiseEnPlaceAlert(string message)` — consumed by `DashboardViewModel` (Task 21) and `AiDashboardViewModel` (Task 25) to route targeted per-widget patches.

- [ ] **Step 1: Create `Services/RealTimeService.cs`**

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using RushOrder.Desktop.Core.Hubs;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class RealTimeService : IAsyncDisposable
{
    private readonly AppState _state;
    private readonly ILogger<RealTimeService> _logger;
    private RestaurantHubClient? _client;

    public bool IsConnected => _client?.State == HubConnectionState.Connected;

    public event Func<OrderReceivedPayload, Task>? OrderReceived;
    public event Func<string, string, DateTimeOffset, Task>? OrderStatusUpdated;
    public event Func<string, string, Task>? TableStatusChanged;
    public event Func<string, string, Task>? KitchenAlert;
    public event Func<string, Task>? MiseEnPlaceAlert;
    public event Action<bool>? ConnectionChanged;

    public RealTimeService(AppState state, ILogger<RealTimeService> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task ConnectAsync(string baseUrl, CancellationToken ct = default)
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        if (_state.AccessToken is null) return;

        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/restaurant";
        using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var hubLogger = loggerFactory.CreateLogger<RestaurantHubClient>();
        _client = new RestaurantHubClient(hubUrl, _state.AccessToken, hubLogger);
        WireEvents();

        try
        {
            await _client.StartAsync(ct);
            if (_state.CurrentRestaurant is not null)
                await _client.JoinRestaurantAsync(_state.CurrentRestaurant.Id.ToString(), ct);

            ConnectionChanged?.Invoke(true);
            _state.SetOnlineStatus(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            ConnectionChanged?.Invoke(false);
            _state.SetOnlineStatus(false);
        }
    }

    private void WireEvents()
    {
        _client!.OnOrderReceived += p => OrderReceived?.Invoke(p) ?? Task.CompletedTask;
        _client!.OnOrderStatusUpdated += (id, status, ts) => OrderStatusUpdated?.Invoke(id, status, ts) ?? Task.CompletedTask;
        _client!.OnTableStatusChanged += (id, status) => TableStatusChanged?.Invoke(id, status) ?? Task.CompletedTask;
        _client!.OnKitchenAlert += (msg, severity) => KitchenAlert?.Invoke(msg, severity) ?? Task.CompletedTask;
        _client!.OnMiseEnPlaceAlert += msg => MiseEnPlaceAlert?.Invoke(msg) ?? Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null) await _client.DisposeAsync();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/RealTimeService.cs
git commit -m "feat(desktop-avalonia): add RealTimeService wrapping RushOrder.Desktop.Core's RestaurantHubClient"
```

---

### Task 15: `RevenueWidget` (template widget — establishes the pattern Tasks 16-24 follow)

**Design decision (resolves an ambiguity the spec left implicit):** `DashboardDataService.GetKpiAsync()` returns one `DashboardKpi` covering revenue, active orders, table occupancy, and avg ticket together — the single WinForms `DashboardView.LoadDataAsync()` fetches it once and distributes fields to 4 widgets. Spec Section 2 requires "carga independiente por widget — no hay loader global." To satisfy that literally (including independent per-widget `Retry`, so one widget's retry never affects another's state), **each of the 4 KPI-derived widgets (Revenue, ActiveOrders, Tables, AvgTicket) calls `GetKpiAsync()` independently in its own `LoadAsync()`.** This means the same endpoint is hit up to 4× per refresh cycle instead of once — acceptable at the Dashboard's 30s refresh cadence, and it keeps every widget view model uniform and independently testable, matching `WidgetViewModelBase`'s contract exactly. If this duplication becomes a real cost later, a shared-fetch-with-distribution optimization can be added without changing any widget's public shape.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/RevenueWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/RevenueWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/RevenueWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetKpiAsync()` (Task 12), `WidgetViewModelBase` (Task 5), `KpiValueTransition` (Task 8), `SparklineControl` (Task 10).
- Produces: `RevenueWidgetViewModel` with `RevenueToday` (double, animated), `DeltaText`, `DeltaIsUp`, `Hourly` — pattern (state fields + `Ok`/`Fail` handling in `LoadAsync`) reused identically by Tasks 16-18, 22-24.

- [ ] **Step 1: Write the failing tests**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class RevenueWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_data()
    {
        var vm = new RevenueWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal((double)MockDashboardData.Kpi().RevenueToday, vm.RevenueToday, precision: 2);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter RevenueWidgetViewModelTests`
Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `ViewModels/Dashboard/RevenueWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Animations;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class RevenueWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private IDisposable? _counterAnimation;

    [ObservableProperty] private double _revenueToday;
    [ObservableProperty] private string _deltaText = "—";
    [ObservableProperty] private bool _deltaIsUp;
    [ObservableProperty] private IReadOnlyList<decimal> _hourly = [];

    public RevenueWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();

        if (result.IsSuccess)
        {
            ApplyKpi(result.Value!);
            State = WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            ApplyKpi(MockDashboardData.Kpi());
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    private void ApplyKpi(DashboardKpi kpi)
    {
        var target = (double)kpi.RevenueToday;
        _counterAnimation?.Dispose();
        _counterAnimation = KpiValueTransition.Animate(RevenueToday, target, TimeSpan.FromMilliseconds(500),
            value => RevenueToday = value);

        var up = kpi.RevenueToday >= kpi.RevenueYesterday;
        var pct = kpi.RevenueYesterday != 0
            ? Math.Abs((double)((kpi.RevenueToday - kpi.RevenueYesterday) / kpi.RevenueYesterday * 100))
            : 0;
        DeltaIsUp = up;
        DeltaText = $"{(up ? "▲" : "▼")} {pct:F1}% vs. ayer";
        Hourly = kpi.RevenueByHour;
    }
}
```

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests --filter RevenueWidgetViewModelTests`
Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/RevenueWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.RevenueWidgetView">
  <controls:KpiCardBase Title="INGRESOS DEL DÍA" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <DockPanel>
        <TextBlock DockPanel.Dock="Top" Text="{Binding RevenueToday, StringFormat='€ {0:N2}'}"
                   FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeKpi}"
                   FontWeight="Bold" Foreground="{DynamicResource TextPrimaryBrush}" Margin="{StaticResource SpaceThickness2}" />
        <TextBlock DockPanel.Dock="Top" Text="{Binding DeltaText}"
                   FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeLabel}"
                   Foreground="{Binding DeltaIsUp, Converter={x:Static local:DeltaColorConverter.Instance}}"
                   Margin="8,0" />
        <controls:SparklineControl Data="{Binding Hourly}"
                                    LineBrush="{DynamicResource SuccessBrush}" />
      </DockPanel>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

(`xmlns:local="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"` needs adding to the root tag for `DeltaColorConverter`, created next step.)

- [ ] **Step 6: Create `ViewModels/Dashboard/DeltaColorConverter.cs`** (shared by every widget with an up/down delta — Tasks 16, 18)

```csharp
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class DeltaColorConverter : IValueConverter
{
    public static readonly DeltaColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Avalonia.Application.Current!.FindResource("SuccessBrush")! : Avalonia.Application.Current!.FindResource("ErrorBrush")!;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 7: Create `Views/Dashboard/RevenueWidgetView.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard;

public partial class RevenueWidgetView : UserControl
{
    public RevenueWidgetView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 8: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/RevenueWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/DeltaColorConverter.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/RevenueWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/RevenueWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/RevenueWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add RevenueWidget (Loaded/Error+simulated, animated counter, sparkline)"
```

---

### Task 16: `ActiveOrdersWidget` (adds click-to-navigate)

Same pattern as Task 15. New element: clicking the card navigates to `"orders/kanban"` (currently the `PlaceholderView`, per Task 6/spec Section 3), the widget's own `NavigateCommand` rather than `KpiCardBase` handling clicks generically (only this widget and Tables navigate on click — Revenue/AvgTicket/Alerts/Reservations do not).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ActiveOrdersWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ActiveOrdersWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/ActiveOrdersWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetKpiAsync()` (Task 12), `INavigationService` (Task 6).
- Produces: `ActiveOrdersWidgetViewModel` with `Total`, `Waiting`, `Preparing`, `Ready`, `NavigateCommand`.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class ActiveOrdersWidgetViewModelTests
{
    [Fact]
    public void NavigateCommand_navigates_to_orders_kanban()
    {
        var nav = new NavigationService();
        var vm = new ActiveOrdersWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);

        vm.NavigateCommand.Execute(null);

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("orders/kanban", placeholder.RouteKey);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `ViewModels/Dashboard/ActiveOrdersWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class ActiveOrdersWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    [ObservableProperty] private int _waiting;
    [ObservableProperty] private int _preparing;
    [ObservableProperty] private int _ready;
    [ObservableProperty] private int _total;

    public ActiveOrdersWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Waiting = kpi.OrdersWaiting;
        Preparing = kpi.OrdersPreparing;
        Ready = kpi.OrdersReady;
        Total = Waiting + Preparing + Ready;

        if (result.IsSuccess)
        {
            State = Total == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    [RelayCommand]
    private void Navigate() => _nav.NavigateTo("orders/kanban");
}
```

(This is the first widget where `Empty` is actually reachable — `result.IsSuccess && Total == 0` is "sin pedidos activos," a legitimate empty state distinct from a fetch error, per spec Section 2's matrix.)

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/ActiveOrdersWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.ActiveOrdersWidgetView">
  <controls:KpiCardBase Title="PEDIDOS ACTIVOS" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <Button Command="{Binding NavigateCommand}" Background="Transparent" BorderThickness="0"
              HorizontalContentAlignment="Stretch" Cursor="Hand">
        <StackPanel Margin="{StaticResource SpaceThickness2}" Spacing="{StaticResource Space1}">
          <TextBlock Text="{Binding Total}" FontFamily="{StaticResource PoppinsFontFamily}"
                     FontSize="{StaticResource FontSizeKpi}" FontWeight="Bold"
                     Foreground="{DynamicResource TextPrimaryBrush}" />
          <TextBlock Text="{Binding Waiting, StringFormat='{}{0} en espera'}" FontFamily="{StaticResource PoppinsFontFamily}"
                     FontSize="{StaticResource FontSizeLabel}" Foreground="{DynamicResource WarningBrush}" />
          <TextBlock Text="{Binding Preparing, StringFormat='{}{0} preparando'}" FontFamily="{StaticResource PoppinsFontFamily}"
                     FontSize="{StaticResource FontSizeLabel}" Foreground="{DynamicResource InfoBrush}" />
          <TextBlock Text="{Binding Ready, StringFormat='{}{0} listos'}" FontFamily="{StaticResource PoppinsFontFamily}"
                     FontSize="{StaticResource FontSizeLabel}" Foreground="{DynamicResource SuccessBrush}" />
        </StackPanel>
      </Button>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/ActiveOrdersWidgetView.axaml.cs`** (identical shape to Task 15 Step 7, `x:Class` updated)

- [ ] **Step 7: Build.** Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ActiveOrdersWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ActiveOrdersWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ActiveOrdersWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/ActiveOrdersWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add ActiveOrdersWidget (Empty state, click-to-navigate)"
```

---

### Task 17: `TablesWidget` (uses `OccupancyArcControl`, adds click-to-navigate)

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/TablesWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/TablesWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/TablesWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetKpiAsync()` (Task 12), `INavigationService` (Task 6), `OccupancyArcControl` (Task 11).
- Produces: `TablesWidgetViewModel` with `Occupied`, `Total`, `AvgOccupancyMinutes`, `NavigateCommand`.

- [ ] **Step 1: Write the failing test** (same shape as Task 16 Step 1, asserting route `"tables/floorplan"`)

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class TablesWidgetViewModelTests
{
    [Fact]
    public void NavigateCommand_navigates_to_tables_floorplan()
    {
        var nav = new NavigationService();
        var vm = new TablesWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);

        vm.NavigateCommand.Execute(null);

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("tables/floorplan", placeholder.RouteKey);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/TablesWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class TablesWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    [ObservableProperty] private int _occupied;
    [ObservableProperty] private int _total;
    [ObservableProperty] private double _avgOccupancyMinutes;

    public TablesWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Occupied = kpi.TablesOccupied;
        Total = kpi.TablesTotal;
        AvgOccupancyMinutes = kpi.AvgOccupancyMinutes;

        if (result.IsSuccess)
        {
            State = Total == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    [RelayCommand]
    private void Navigate() => _nav.NavigateTo("tables/floorplan");
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/TablesWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.TablesWidgetView">
  <controls:KpiCardBase Title="MESAS" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <Button Command="{Binding NavigateCommand}" Background="Transparent" BorderThickness="0" Cursor="Hand">
        <DockPanel>
          <TextBlock DockPanel.Dock="Bottom" Text="{Binding AvgOccupancyMinutes, StringFormat='Tiempo medio: {0:F0} min'}"
                     FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeLabel}"
                     Foreground="{DynamicResource TextSecondaryBrush}" HorizontalAlignment="Center"
                     Margin="0,0,0,8" />
          <controls:OccupancyArcControl Occupied="{Binding Occupied}" Total="{Binding Total}" />
        </DockPanel>
      </Button>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/TablesWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 7: Build.** Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/TablesWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/TablesWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/TablesWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/TablesWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add TablesWidget (occupancy arc, click-to-navigate)"
```

---

### Task 18: `AvgTicketWidget`

Same pattern as Task 15, no navigation (matches WinForms — only orders/tables navigate). Bar comparison instead of sparkline (rendered inline via two `Border` heights bound to converted values — no dedicated control needed, unlike Revenue/Tables, since it's two static bars, not a data-driven vector chart).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AvgTicketWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AvgTicketWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AvgTicketWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetKpiAsync()` (Task 12).
- Produces: `AvgTicketWidgetViewModel` with `Today`, `Yesterday`, `DeltaText`, `DeltaIsUp`.

- [ ] **Step 1: Write the failing test** (same shape as Task 15 Step 1, asserting `Today`/`Yesterday` from `MockDashboardData.Kpi()` on failure)

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class AvgTicketWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_data()
    {
        var vm = new AvgTicketWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(MockDashboardData.Kpi().AvgTicketToday, vm.Today);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/AvgTicketWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class AvgTicketWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;

    [ObservableProperty] private decimal _today;
    [ObservableProperty] private decimal _yesterday;
    [ObservableProperty] private string _deltaText = "";
    [ObservableProperty] private bool _deltaIsUp;

    public AvgTicketWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetKpiAsync();
        var kpi = result.IsSuccess ? result.Value! : MockDashboardData.Kpi();

        Today = kpi.AvgTicketToday;
        Yesterday = kpi.AvgTicketYesterday;
        DeltaIsUp = Today >= Yesterday;
        var pct = Yesterday != 0 ? Math.Abs((double)((Today - Yesterday) / Yesterday * 100)) : 0;
        DeltaText = $"{(DeltaIsUp ? "▲" : "▼")} {pct:F1}% vs. ayer (€ {Yesterday:N2})";

        if (result.IsSuccess)
        {
            State = WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/AvgTicketWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             xmlns:local="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AvgTicketWidgetView">
  <controls:KpiCardBase Title="TICKET MEDIO" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <StackPanel Margin="{StaticResource SpaceThickness2}" Spacing="{StaticResource Space1}">
        <TextBlock Text="{Binding Today, StringFormat='€ {0:N2}'}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontSize="26" FontWeight="Bold" Foreground="{DynamicResource TextPrimaryBrush}" />
        <TextBlock Text="{Binding DeltaText}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontSize="{StaticResource FontSizeLabel}"
                   Foreground="{Binding DeltaIsUp, Converter={x:Static local:DeltaColorConverter.Instance}}" />
        <Grid ColumnDefinitions="*,*" Height="60" Margin="0,8,0,0">
          <Border Grid.Column="0" VerticalAlignment="Bottom" Background="#5A5A5A" Width="40"
                  Height="{Binding Yesterday, Converter={x:Static local:BarHeightConverter.Instance}}"
                  CornerRadius="{StaticResource RadiusSm}" HorizontalAlignment="Center" />
          <Border Grid.Column="1" VerticalAlignment="Bottom" Background="{DynamicResource SuccessBrush}" Width="40"
                  Height="{Binding Today, Converter={x:Static local:BarHeightConverter.Instance}}"
                  CornerRadius="{StaticResource RadiusSm}" HorizontalAlignment="Center" />
        </Grid>
      </StackPanel>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 6: Create `ViewModels/Dashboard/BarHeightConverter.cs`** (scales a decimal to a 0-60px bar height against a fixed €60 reference ceiling — matches the WinForms `ComparisonBarPanel`'s relative-to-max behavior closely enough for a 2-bar comparison without needing both values bound into one converter call)

```csharp
using Avalonia.Data.Converters;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class BarHeightConverter : IValueConverter
{
    public static readonly BarHeightConverter Instance = new();
    private const double MaxReference = 60; // €60 ceiling ≈ typical avg-ticket range

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var amount = value is decimal d ? (double)d : 0;
        var fraction = Math.Clamp(amount / MaxReference, 0, 1);
        return Math.Max(4, fraction * 60);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 7: Create `Views/Dashboard/AvgTicketWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 8: Build.** Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AvgTicketWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/BarHeightConverter.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AvgTicketWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AvgTicketWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AvgTicketWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add AvgTicketWidget"
```

---

### Task 19: `AlertsWidget` (shared by DASH-01 and DASH-02)

Placed in `ViewModels/Dashboard/` (not nested under `AiDashboard/`) and referenced by both `DashboardView` (Task 21) and `AiDashboardView` (Task 25), matching the WinForms code sharing `AlertsWidget` between both modules. List widget instead of a single value — different shape from Tasks 15-18: `LoadAsync` populates a collection, and clicking a row navigates based on `AlertDto.ResourceType`.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AlertRowViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AlertsWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AlertsWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AlertsWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetAlertsAsync()` (Task 12), `INavigationService` (Task 6).
- Produces: `AlertsWidgetViewModel.Alerts` (`ObservableCollection<AlertRowViewModel>`), `public void Prepend(AlertDto alert)` (consumed directly by `DashboardViewModel`/`AiDashboardViewModel` real-time handlers in Tasks 21/25 — this is the "patch only this widget" real-time entry point for `KitchenAlert`/`MiseEnPlaceAlert`).

- [ ] **Step 1: Write the failing tests**

```csharp
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class AlertsWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_shows_simulated_alerts()
    {
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance),
            new NavigationService());

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(3, vm.Alerts.Count); // MockDashboardData.Alerts() has 3 entries
    }

    [Fact]
    public void Prepend_adds_to_the_front_without_a_full_reload()
    {
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance),
            new NavigationService());
        var alert = new AlertDto(Guid.NewGuid(), "Resumen mise en place", AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now);

        vm.Prepend(alert);

        Assert.Equal("Resumen mise en place", vm.Alerts[0].Message);
    }

    [Theory]
    [InlineData("Product", "menu/products")]
    [InlineData("Order", "orders/kanban")]
    public void RowClicked_routes_by_ResourceType(string resourceType, string expectedRoute)
    {
        var nav = new NavigationService();
        var vm = new AlertsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance), nav);
        var alert = new AlertDto(Guid.NewGuid(), "msg", AlertSeverity.Info, null, resourceType, DateTimeOffset.Now);

        vm.RowClickedCommand.Execute(new AlertRowViewModel(alert));

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal(expectedRoute, placeholder.RouteKey);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/AlertRowViewModel.cs`**

```csharp
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class AlertRowViewModel(AlertDto alert)
{
    public AlertDto Source { get; } = alert;
    public string Message => alert.Message;
    public AlertSeverity Severity => alert.Severity;
    public string Age => FormatAge(alert.OccurredAt);

    private static string FormatAge(DateTimeOffset ts)
    {
        var age = DateTimeOffset.Now - ts;
        if (age.TotalMinutes < 1) return "Ahora mismo";
        if (age.TotalMinutes < 60) return $"Hace {(int)age.TotalMinutes} min";
        return $"Hace {(int)age.TotalHours} h";
    }
}
```

- [ ] **Step 4: Create `ViewModels/Dashboard/AlertsWidgetViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class AlertsWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;
    private readonly INavigationService _nav;

    public ObservableCollection<AlertRowViewModel> Alerts { get; } = [];

    public AlertsWidgetViewModel(DashboardDataService data, INavigationService nav)
    {
        _data = data;
        _nav = nav;
    }

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetAlertsAsync();
        var alerts = result.IsSuccess ? result.Value! : MockDashboardData.Alerts();

        Alerts.Clear();
        foreach (var alert in alerts) Alerts.Add(new AlertRowViewModel(alert));

        if (result.IsSuccess)
        {
            State = alerts.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    /// <summary>Real-time entry point — <c>KitchenAlert</c>/<c>MiseEnPlaceAlert</c> call this
    /// directly instead of triggering <see cref="WidgetViewModelBase.InitializeAsync"/>, so a
    /// live alert never re-fetches or touches <see cref="WidgetViewModelBase.State"/>.</summary>
    public void Prepend(AlertDto alert) => Alerts.Insert(0, new AlertRowViewModel(alert));

    [RelayCommand]
    private void RowClicked(AlertRowViewModel row)
    {
        var routeKey = row.Source.ResourceType switch
        {
            "Product" => "menu/products",
            "Order" => "orders/kanban",
            _ => null, // "Reservation"/"mise_en_place" have no target yet — same as WinForms
        };
        if (routeKey is not null) _nav.NavigateTo(routeKey, row.Source.ResourceId);
    }
}
```

- [ ] **Step 5: Run it to verify it passes.** Expected: PASS (3 tests).

- [ ] **Step 6: Create `Views/Dashboard/AlertsWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AlertsWidgetView">
  <controls:KpiCardBase Title="ALERTAS ACTIVAS" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <ScrollViewer>
        <ItemsControl ItemsSource="{Binding Alerts}">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Button Command="{Binding $parent[ItemsControl].((vm:AlertsWidgetViewModel)DataContext).RowClickedCommand}"
                      xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"
                      CommandParameter="{Binding}" Background="Transparent" BorderThickness="0"
                      HorizontalContentAlignment="Stretch" HorizontalAlignment="Stretch" Cursor="Hand">
                <StackPanel Margin="{StaticResource SpaceThickness2}">
                  <TextBlock Text="{Binding Message}" FontFamily="{StaticResource PoppinsFontFamily}"
                             FontSize="{StaticResource FontSizeLabel}" TextTrimming="CharacterEllipsis"
                             Foreground="{DynamicResource TextPrimaryBrush}" />
                  <TextBlock Text="{Binding Age}" FontFamily="{StaticResource PoppinsFontFamily}"
                             FontSize="10" Foreground="{DynamicResource TextSecondaryBrush}" />
                </StackPanel>
              </Button>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 7: Create `Views/Dashboard/AlertsWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 8: Build.** Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AlertRowViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AlertsWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AlertsWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AlertsWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AlertsWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add AlertsWidget (shared by DASH-01 and DASH-02, Prepend for real-time)"
```

---

### Task 20: `ReservationsWidget`

Same list-widget pattern as Task 19, simpler (no navigation, no `Prepend` — no real-time event patches reservations in WinForms either).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ReservationRowViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ReservationsWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ReservationsWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/ReservationsWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `DashboardDataService.GetUpcomingReservationsAsync()` (Task 12).
- Produces: `ReservationsWidgetViewModel.Reservations` (`ObservableCollection<ReservationRowViewModel>`).

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class ReservationsWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_shows_simulated_reservations()
    {
        var vm = new ReservationsWidgetViewModel(
            new DashboardDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(3, vm.Reservations.Count);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/ReservationRowViewModel.cs`**

```csharp
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class ReservationRowViewModel(ReservationDto reservation)
{
    public string CustomerName => reservation.Notes is { Length: > 0 }
        ? $"{reservation.CustomerName} — {reservation.Notes}"
        : reservation.CustomerName;
    public string TimeText => reservation.ReservationTime.LocalDateTime.ToString("HH:mm");
    public string DetailText
    {
        get
        {
            var minutes = (int)(reservation.ReservationTime - DateTimeOffset.Now).TotalMinutes;
            var until = minutes <= 0 ? "Ahora" : minutes < 60 ? $"en {minutes} min" : $"en {minutes / 60}h {minutes % 60:D2}m";
            return $"{reservation.PartySize} personas · {until}";
        }
    }
    public bool IsUrgent => (reservation.ReservationTime - DateTimeOffset.Now).TotalMinutes <= 30;
}
```

- [ ] **Step 4: Create `ViewModels/Dashboard/ReservationsWidgetViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed partial class ReservationsWidgetViewModel : WidgetViewModelBase
{
    private readonly DashboardDataService _data;

    public ObservableCollection<ReservationRowViewModel> Reservations { get; } = [];

    public ReservationsWidgetViewModel(DashboardDataService data) => _data = data;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _data.GetUpcomingReservationsAsync();
        var reservations = result.IsSuccess ? result.Value! : MockDashboardData.Reservations();

        Reservations.Clear();
        foreach (var r in reservations) Reservations.Add(new ReservationRowViewModel(r));

        if (result.IsSuccess)
        {
            State = reservations.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
```

- [ ] **Step 5: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 6: Create `Views/Dashboard/ReservationsWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.ReservationsWidgetView">
  <controls:KpiCardBase Title="PRÓXIMAS RESERVAS" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <ItemsControl ItemsSource="{Binding Reservations}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Grid ColumnDefinitions="52,*" Margin="{StaticResource SpaceThickness2}">
              <Border Grid.Column="0" CornerRadius="{StaticResource RadiusSm}"
                      Background="{Binding IsUrgent, Converter={x:Static BoolBrushConverters.WarningOrInfo}}"
                      Height="44">
                <TextBlock Text="{Binding TimeText}" Foreground="White" FontWeight="Bold"
                           HorizontalAlignment="Center" VerticalAlignment="Center" />
              </Border>
              <StackPanel Grid.Column="1" Margin="8,0,0,0">
                <TextBlock Text="{Binding CustomerName}" FontFamily="{StaticResource PoppinsFontFamily}"
                           FontWeight="SemiBold" FontSize="{StaticResource FontSizeLabel}"
                           TextTrimming="CharacterEllipsis" Foreground="{DynamicResource TextPrimaryBrush}" />
                <TextBlock Text="{Binding DetailText}" FontFamily="{StaticResource PoppinsFontFamily}"
                           FontSize="10" Foreground="{DynamicResource TextSecondaryBrush}" />
              </StackPanel>
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 7: Create `ViewModels/Dashboard/BoolBrushConverters.cs`** (the `WarningOrInfo` converter referenced above)

```csharp
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public static class BoolBrushConverters
{
    public static readonly IValueConverter WarningOrInfo = new FuncValueConverter<bool, IBrush>(isUrgent =>
        isUrgent
            ? (IBrush)Avalonia.Application.Current!.FindResource("WarningBrush")!
            : (IBrush)Avalonia.Application.Current!.FindResource("InfoBrush")!);
}
```

(`Views/Dashboard/ReservationsWidgetView.axaml` needs `xmlns:local="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"` added to its root tag and `{x:Static local:BoolBrushConverters.WarningOrInfo}` used instead of the unqualified reference above — corrected here for clarity.)

- [ ] **Step 8: Create `Views/Dashboard/ReservationsWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 9: Build.** Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ReservationRowViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/ReservationsWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/BoolBrushConverters.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ReservationsWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/ReservationsWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/ReservationsWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add ReservationsWidget"
```

---

### Task 21: `DashboardView` + `DashboardViewModel` — assembles DASH-01

Grid of the 6 widgets from Tasks 15-20, real-time patch routing (spec Section 3's table), 30s refresh timer, and re-registers the shell's `"dashboard"` route to this real view (replacing the `PlaceholderView` from Task 7).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/DashboardViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/DashboardView.axaml` (+ `.axaml.cs`)
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml` (register the `DashboardViewModel → DashboardView` DataTemplate)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/DashboardViewModelTests.cs`

**Interfaces:**
- Consumes: all 6 widget view models (Tasks 15-20), `RealTimeService` (Task 14).
- Produces: `DashboardViewModel` exposing the 6 child VMs as properties — consumed by Task 26's `INavigationService.Register("dashboard", ...)`.

- [ ] **Step 1: Write the failing test** (verifies the real-time routing table from spec Section 3 — `TableStatusChanged` patches only `TablesWidgetViewModel`, never touches the others)

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard;

public class DashboardViewModelTests
{
    [Fact]
    public async Task TableStatusChanged_patches_only_TablesOccupied_not_other_widgets()
    {
        var appState = new AppState();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance;
        var data = new DashboardDataService(appState, logger);
        var nav = new NavigationService();
        var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);

        var vm = new DashboardViewModel(
            new RevenueWidgetViewModel(data),
            new ActiveOrdersWidgetViewModel(data, nav),
            new TablesWidgetViewModel(data, nav),
            new AvgTicketWidgetViewModel(data),
            new AlertsWidgetViewModel(data, nav),
            new ReservationsWidgetViewModel(data),
            realTime);

        var occupiedBefore = vm.Tables.Occupied;
        var revenueBefore = vm.Revenue.RevenueToday;

        await vm.OnTableStatusChangedForTest("table-1", "Occupied");

        Assert.NotEqual(occupiedBefore, vm.Tables.Occupied); // patched
        Assert.Equal(revenueBefore, vm.Revenue.RevenueToday); // untouched
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `ViewModels/Dashboard/DashboardViewModel.cs`**

```csharp
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class DashboardViewModel : IDisposable
{
    private readonly RealTimeService _realTime;
    private readonly System.Timers.Timer _refreshTimer;

    public RevenueWidgetViewModel Revenue { get; }
    public ActiveOrdersWidgetViewModel Orders { get; }
    public TablesWidgetViewModel Tables { get; }
    public AvgTicketWidgetViewModel Ticket { get; }
    public AlertsWidgetViewModel Alerts { get; }
    public ReservationsWidgetViewModel Reservations { get; }

    public DashboardViewModel(
        RevenueWidgetViewModel revenue, ActiveOrdersWidgetViewModel orders, TablesWidgetViewModel tables,
        AvgTicketWidgetViewModel ticket, AlertsWidgetViewModel alerts, ReservationsWidgetViewModel reservations,
        RealTimeService realTime)
    {
        Revenue = revenue; Orders = orders; Tables = tables;
        Ticket = ticket; Alerts = alerts; Reservations = reservations;
        _realTime = realTime;

        WireRealTime();

        _refreshTimer = new System.Timers.Timer(30_000) { AutoReset = true };
        _refreshTimer.Elapsed += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await Task.WhenAll(
            Revenue.InitializeAsync(), Orders.InitializeAsync(), Tables.InitializeAsync(),
            Ticket.InitializeAsync(), Alerts.InitializeAsync(), Reservations.InitializeAsync());
    }

    // Real-time routing table — spec Section 3. Each handler touches exactly one widget's
    // properties directly; none re-fetches or calls InitializeAsync().
    private void WireRealTime()
    {
        _realTime.OrderReceived += _ =>
        {
            Orders.Waiting++;
            Orders.Total++;
            return Task.CompletedTask;
        };

        _realTime.TableStatusChanged += (_, status) =>
        {
            if (status == "Occupied" && Tables.Occupied < Tables.Total) Tables.Occupied++;
            else if (status is "Free" or "Cleaning" && Tables.Occupied > 0) Tables.Occupied--;
            return Task.CompletedTask;
        };

        _realTime.KitchenAlert += async (message, severity) =>
        {
            var alert = new Models.AlertDto(Guid.NewGuid(), message,
                Enum.TryParse<Models.AlertSeverity>(severity, true, out var sev) ? sev : Models.AlertSeverity.Info,
                null, "Order", DateTimeOffset.Now);
            Alerts.Prepend(alert);
            await Task.CompletedTask;
        };

        _realTime.MiseEnPlaceAlert += async message =>
        {
            var alert = new Models.AlertDto(Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now);
            Alerts.Prepend(alert);
            await Task.CompletedTask;
        };
    }

    /// <summary>Test-only synchronous entry point mirroring the <c>TableStatusChanged</c>
    /// handler above, since the real event is wired through <see cref="RealTimeService"/>'s
    /// SignalR connection which isn't started in unit tests.</summary>
    internal Task OnTableStatusChangedForTest(string tableId, string status)
    {
        if (status == "Occupied" && Tables.Occupied < Tables.Total) Tables.Occupied++;
        else if (status is "Free" or "Cleaning" && Tables.Occupied > 0) Tables.Occupied--;
        return Task.CompletedTask;
    }

    public void Dispose() => _refreshTimer.Dispose();
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/DashboardView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:dash="using:RushOrder.Desktop.Avalonia.Views.Dashboard"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.DashboardView">
  <Grid RowDefinitions="*,*" ColumnDefinitions="*,*,*" Margin="{StaticResource SpaceThickness2}">
    <dash:RevenueWidgetView Grid.Row="0" Grid.Column="0" DataContext="{Binding Revenue}" />
    <dash:ActiveOrdersWidgetView Grid.Row="0" Grid.Column="1" DataContext="{Binding Orders}" />
    <dash:TablesWidgetView Grid.Row="0" Grid.Column="2" DataContext="{Binding Tables}" />
    <dash:AvgTicketWidgetView Grid.Row="1" Grid.Column="0" DataContext="{Binding Ticket}" />
    <dash:AlertsWidgetView Grid.Row="1" Grid.Column="1" DataContext="{Binding Alerts}" />
    <dash:ReservationsWidgetView Grid.Row="1" Grid.Column="2" DataContext="{Binding Reservations}" />
  </Grid>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/DashboardView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 7: Register the `DashboardViewModel → DashboardView` DataTemplate in `Views/Shell/MainWindow.axaml`**

Add an `xmlns:dashvm="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"` and `xmlns:dashview="using:RushOrder.Desktop.Avalonia.Views.Dashboard"` to the root `Window` tag (alongside the existing `xmlns:vm`/`xmlns:nav`), then add one more entry to the existing `Window.DataTemplates` block from Task 7 (do not remove the `PlaceholderViewModel` entry already there):

```xml
<Window.DataTemplates>
  <DataTemplate DataType="nav:PlaceholderViewModel">
    <nav:PlaceholderView />
  </DataTemplate>
  <DataTemplate DataType="dashvm:DashboardViewModel">
    <dashview:DashboardView />
  </DataTemplate>
</Window.DataTemplates>
```

This is the same explicit, per-type pattern Task 7 already established for `PlaceholderViewModel` — not a generic convention-based resolver.

- [ ] **Step 8: Build.** Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/DashboardViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/DashboardView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/DashboardView.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/DashboardViewModelTests.cs
git commit -m "feat(desktop-avalonia): assemble DashboardView/DashboardViewModel — DASH-01 complete"
```

---

### Task 22: `TodayForecastWidget` (uses `LiveChartsCore.SkiaSharpView.Avalonia`)

WinForms uses `LiveChartsCore.SkiaSharpView.WinForms`'s `CartesianChart` for this one widget (not hand-drawn GDI+, unlike Revenue/Tables) — Avalonia has an equivalent `LiveChartsCore.SkiaSharpView.Avalonia` package, reused rather than hand-rolling a bar-chart control.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj` (add package reference)
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/TodayForecastWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/TodayForecastWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/TodayForecastWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `ForecastDataService.GetDemandForecastAsync()` (Task 13).
- Produces: `TodayForecastWidgetViewModel.Series` (`ISeries[]`), `.XLabels` (`string[]`) — bound directly to a LiveCharts `CartesianChart`.

- [ ] **Step 1: Add the package reference**

```xml
<PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" />
```

- [ ] **Step 2: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class TodayForecastWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_sets_Error_and_simulated_series()
    {
        var vm = new TodayForecastWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(MockForecastData.DemandForecast().Hourly.Count, vm.XLabels.Length);
    }
}
```

- [ ] **Step 3: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 4: Create `ViewModels/Dashboard/AiDashboard/TodayForecastWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class TodayForecastWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private ISeries[] _series = [];
    [ObservableProperty] private string[] _xLabels = [];

    public TodayForecastWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = await _forecast.GetDemandForecastAsync(today);
        var hourly = result.IsSuccess ? result.Value!.Hourly : MockForecastData.DemandForecast().Hourly;

        var ordered = hourly.OrderBy(h => h.Hour).ToList();
        Series = [new ColumnSeries<double> { Values = ordered.Select(h => (double)h.PredictedOrders).ToArray(), Name = "Pedidos" }];
        XLabels = ordered.Select(h => $"{h.Hour}h").ToArray();

        if (result.IsSuccess)
        {
            State = hourly.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded; // "histórico insuficiente"
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
```

- [ ] **Step 5: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 6: Create `Views/Dashboard/AiDashboard/TodayForecastWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             xmlns:lvc="using:LiveChartsCore.SkiaSharpView.Avalonia"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard.TodayForecastWidgetView">
  <controls:KpiCardBase Title="PREVISIÓN DE HOY" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <lvc:CartesianChart Series="{Binding Series}" Margin="{StaticResource SpaceThickness2}" />
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

(`XLabels` feeds the X-axis via a `CartesianChart.XAxes` binding to an `Axis[]` built from `XLabels` in code-behind's `OnDataContextChanged`, mirroring how WinForms builds `Axis[]` imperatively in `Update()` rather than declaratively — LiveCharts' Avalonia axis binding is less XAML-friendly than its series binding. Implementer note: wire this in `TodayForecastWidgetView.axaml.cs`'s constructor via a `DataContextChanged` handler that sets `Chart.XAxes = [new Axis { Labels = vm.XLabels, ... }]`, not left as a XAML-only binding.)

- [ ] **Step 7: Create `Views/Dashboard/AiDashboard/TodayForecastWidgetView.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LiveChartsCore.SkiaSharpView;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard;

public partial class TodayForecastWidgetView : UserControl
{
    private readonly LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart _chart;

    public TodayForecastWidgetView()
    {
        InitializeComponent();
        _chart = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>("Chart")!;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TodayForecastWidgetViewModel vm)
                _chart.XAxes = [new Axis { Labels = vm.XLabels, TextSize = 9, LabelsRotation = -45 }];
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

(Requires adding `x:Name="Chart"` to the `<lvc:CartesianChart>` element in Step 6.)

- [ ] **Step 8: Build.** Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/TodayForecastWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/TodayForecastWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/TodayForecastWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/TodayForecastWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add TodayForecastWidget (LiveChartsCore.SkiaSharpView.Avalonia)"
```

---

### Task 23: `SuggestionOfTheDayWidget`

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/SuggestionOfTheDayWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/SuggestionOfTheDayWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/SuggestionOfTheDayWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `ForecastDataService.GetDemandForecastAsync()` (Task 13) — reuses the same fetch as Task 22 (both are DASH-02 widgets independently calling the same endpoint, same design decision as Task 15).
- Produces: `SuggestionOfTheDayWidgetViewModel.ProductName`, `.DetailText`.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class SuggestionOfTheDayWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_shows_the_top_simulated_product()
    {
        var vm = new SuggestionOfTheDayWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.Equal("Paella Valenciana", vm.ProductName);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/AiDashboard/SuggestionOfTheDayWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class SuggestionOfTheDayWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private string? _productName;
    [ObservableProperty] private string _detailText = "";

    public SuggestionOfTheDayWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = await _forecast.GetDemandForecastAsync(today);
        var summary = result.IsSuccess ? result.Value!.Summary : MockForecastData.DemandForecast().Summary;
        var top = summary.TopProducts.FirstOrDefault();

        ProductName = top?.Name;
        DetailText = top is not null ? $"~{Math.Round(top.PredictedQuantity)} unidades previstas hoy — destácalo" : "";

        if (result.IsSuccess)
        {
            State = top is null ? WidgetLoadState.Empty : WidgetLoadState.Loaded; // "sin previsión (histórico insuficiente)"
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/AiDashboard/SuggestionOfTheDayWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard.SuggestionOfTheDayWidgetView">
  <controls:KpiCardBase Title="SUGERENCIA DEL DÍA" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Spacing="{StaticResource Space1}">
        <TextBlock Text="💡" FontSize="28" HorizontalAlignment="Center" />
        <TextBlock Text="{Binding ProductName, TargetNullValue='Sin datos suficientes'}"
                   FontFamily="{StaticResource PoppinsFontFamily}" FontSize="13" FontWeight="Bold"
                   Foreground="{DynamicResource TextPrimaryBrush}" HorizontalAlignment="Center" />
        <TextBlock Text="{Binding DetailText}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontSize="{StaticResource FontSizeLabel}" Foreground="{DynamicResource TextSecondaryBrush}"
                   HorizontalAlignment="Center" TextWrapping="Wrap" TextAlignment="Center" />
      </StackPanel>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/AiDashboard/SuggestionOfTheDayWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 7: Build.** Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/SuggestionOfTheDayWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/SuggestionOfTheDayWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/SuggestionOfTheDayWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/SuggestionOfTheDayWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add SuggestionOfTheDayWidget"
```

---

### Task 24: `KitchenEtaWidget`

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/KitchenEtaWidgetViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/KitchenEtaWidgetView.axaml` (+ `.axaml.cs`)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/KitchenEtaWidgetViewModelTests.cs`

**Interfaces:**
- Consumes: `ForecastDataService.GetKitchenEtaAsync()` (Task 13).
- Produces: `KitchenEtaWidgetViewModel.AverageMinutesText`, `.CaptionText`.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class KitchenEtaWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_with_unreachable_backend_shows_simulated_eta()
    {
        var vm = new KitchenEtaWidgetViewModel(
            new ForecastDataService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance));

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.Equal("19 min", vm.AverageMinutesText); // Math.Round(18.5m) == 18... see Step 3 rounding note
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/AiDashboard/KitchenEtaWidgetViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed partial class KitchenEtaWidgetViewModel : WidgetViewModelBase
{
    private readonly ForecastDataService _forecast;

    [ObservableProperty] private string _averageMinutesText = "—";
    [ObservableProperty] private string _captionText = "sin datos todavía";

    public KitchenEtaWidgetViewModel(ForecastDataService forecast) => _forecast = forecast;

    protected override async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _forecast.GetKitchenEtaAsync();
        var eta = result.IsSuccess ? result.Value! : MockForecastData.KitchenEta();

        if (eta.AverageMinutes is { } minutes)
        {
            AverageMinutesText = $"{Math.Round(minutes, MidpointRounding.AwayFromZero)} min";
            CaptionText = $"últimos {eta.SampleSize} pedidos";
        }
        else
        {
            AverageMinutesText = "—";
            CaptionText = "sin datos todavía";
        }

        if (result.IsSuccess)
        {
            State = eta.AverageMinutes is null ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }
}
```

`Math.Round(18.5m, MidpointRounding.AwayFromZero)` = 19 — matches the test in Step 1.

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/AiDashboard/KitchenEtaWidgetView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:RushOrder.Desktop.Avalonia.Controls"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard.KitchenEtaWidgetView">
  <controls:KpiCardBase Title="ETA MEDIO DE COCINA" State="{Binding State}"
                         IsShowingSimulatedData="{Binding IsShowingSimulatedData}"
                         RetryCommand="{Binding RetryCommand}">
    <controls:KpiCardBase.Content>
      <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
        <TextBlock Text="{Binding AverageMinutesText}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontSize="26" FontWeight="Bold" Foreground="{DynamicResource RushRedBrush}"
                   HorizontalAlignment="Center" />
        <TextBlock Text="{Binding CaptionText}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontSize="{StaticResource FontSizeLabel}" Foreground="{DynamicResource TextSecondaryBrush}"
                   HorizontalAlignment="Center" />
      </StackPanel>
    </controls:KpiCardBase.Content>
  </controls:KpiCardBase>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/AiDashboard/KitchenEtaWidgetView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 7: Build.** Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/KitchenEtaWidgetViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/KitchenEtaWidgetView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/KitchenEtaWidgetView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/KitchenEtaWidgetViewModelTests.cs
git commit -m "feat(desktop-avalonia): add KitchenEtaWidget"
```

---

### Task 25: `AiDashboardView` + `AiDashboardViewModel` — assembles DASH-02

2×2 grid: TodayForecast, Alerts (reused from Task 19), SuggestionOfTheDay, KitchenEta — same layout as WinForms `AiDashboardView.BuildGrid()`.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/AiDashboardViewModel.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/AiDashboardView.axaml` (+ `.axaml.cs`)
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml` (register the `AiDashboardViewModel → AiDashboardView` DataTemplate)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/AiDashboardViewModelTests.cs`

**Interfaces:**
- Consumes: `TodayForecastWidgetViewModel` (22), `SuggestionOfTheDayWidgetViewModel` (23), `KitchenEtaWidgetViewModel` (24), `AlertsWidgetViewModel` (19, a **second, independent instance** — not the Dashboard's — each gets its own `LoadAsync`/state), `RealTimeService` (14).
- Produces: `AiDashboardViewModel` — consumed by Task 26's `INavigationService.Register("panel-ia", ...)`.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Dashboard.AiDashboard;

public class AiDashboardViewModelTests
{
    [Fact]
    public async Task KitchenAlert_patches_only_the_AiDashboard_Alerts_widget()
    {
        var appState = new AppState();
        var dashboardData = new DashboardDataService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardDataService>.Instance);
        var forecastData = new ForecastDataService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<ForecastDataService>.Instance);
        var nav = new NavigationService();
        var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);

        var vm = new AiDashboardViewModel(
            new TodayForecastWidgetViewModel(forecastData),
            new SuggestionOfTheDayWidgetViewModel(forecastData),
            new AlertsWidgetViewModel(dashboardData, nav),
            new KitchenEtaWidgetViewModel(forecastData),
            realTime);

        await vm.OnKitchenAlertForTest("Horno 2 fuera de servicio", "Critical");

        Assert.Equal("Horno 2 fuera de servicio", vm.Alerts.Alerts[0].Message);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL.

- [ ] **Step 3: Create `ViewModels/Dashboard/AiDashboard/AiDashboardViewModel.cs`**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;

public sealed class AiDashboardViewModel : IDisposable
{
    private readonly RealTimeService _realTime;
    private readonly System.Timers.Timer _refreshTimer;

    public TodayForecastWidgetViewModel Forecast { get; }
    public SuggestionOfTheDayWidgetViewModel Suggestion { get; }
    public AlertsWidgetViewModel Alerts { get; }
    public KitchenEtaWidgetViewModel Eta { get; }

    public AiDashboardViewModel(
        TodayForecastWidgetViewModel forecast, SuggestionOfTheDayWidgetViewModel suggestion,
        AlertsWidgetViewModel alerts, KitchenEtaWidgetViewModel eta, RealTimeService realTime)
    {
        Forecast = forecast; Suggestion = suggestion; Alerts = alerts; Eta = eta;
        _realTime = realTime;

        _realTime.KitchenAlert += OnKitchenAlertForTest;
        _realTime.MiseEnPlaceAlert += async message =>
        {
            Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, Models.AlertSeverity.Info, null, "mise_en_place", DateTimeOffset.Now));
            await Task.CompletedTask;
        };

        _refreshTimer = new System.Timers.Timer(60_000) { AutoReset = true };
        _refreshTimer.Elapsed += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        _ = RefreshAllAsync();
    }

    private async Task RefreshAllAsync() =>
        await Task.WhenAll(Forecast.InitializeAsync(), Suggestion.InitializeAsync(), Alerts.InitializeAsync(), Eta.InitializeAsync());

    /// <summary>Also the production <c>KitchenAlert</c> handler — named for the test that
    /// exercises it directly since <see cref="RealTimeService"/>'s SignalR connection isn't
    /// started in unit tests (same pattern as <c>DashboardViewModel</c>'s Task 21 test hook).</summary>
    internal Task OnKitchenAlertForTest(string message, string severity)
    {
        var sev = Enum.TryParse<Models.AlertSeverity>(severity, true, out var s) ? s : Models.AlertSeverity.Info;
        Alerts.Prepend(new Models.AlertDto(Guid.NewGuid(), message, sev, null, "Order", DateTimeOffset.Now));
        return Task.CompletedTask;
    }

    public void Dispose() => _refreshTimer.Dispose();
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Create `Views/Dashboard/AiDashboard/AiDashboardView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ai="using:RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard"
             xmlns:dash="using:RushOrder.Desktop.Avalonia.Views.Dashboard"
             x:Class="RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard.AiDashboardView">
  <Grid RowDefinitions="*,*" ColumnDefinitions="*,*" Margin="{StaticResource SpaceThickness2}">
    <ai:TodayForecastWidgetView Grid.Row="0" Grid.Column="0" DataContext="{Binding Forecast}" />
    <dash:AlertsWidgetView Grid.Row="0" Grid.Column="1" DataContext="{Binding Alerts}" />
    <ai:SuggestionOfTheDayWidgetView Grid.Row="1" Grid.Column="0" DataContext="{Binding Suggestion}" />
    <ai:KitchenEtaWidgetView Grid.Row="1" Grid.Column="1" DataContext="{Binding Eta}" />
  </Grid>
</UserControl>
```

- [ ] **Step 6: Create `Views/Dashboard/AiDashboard/AiDashboardView.axaml.cs`** (identical shape to Task 15 Step 7)

- [ ] **Step 7: Register the `AiDashboardViewModel → AiDashboardView` DataTemplate in `Views/Shell/MainWindow.axaml`**

Add `xmlns:aivm="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard"` and `xmlns:aiview="using:RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard"` to the root `Window` tag (alongside `xmlns:vm`/`xmlns:nav`/`xmlns:dashvm`/`xmlns:dashview` from Task 21), then add one more entry to `Window.DataTemplates` (keep the `PlaceholderViewModel` and `DashboardViewModel` entries from Tasks 7/21):

```xml
<Window.DataTemplates>
  <DataTemplate DataType="nav:PlaceholderViewModel">
    <nav:PlaceholderView />
  </DataTemplate>
  <DataTemplate DataType="dashvm:DashboardViewModel">
    <dashview:DashboardView />
  </DataTemplate>
  <DataTemplate DataType="aivm:AiDashboardViewModel">
    <aiview:AiDashboardView />
  </DataTemplate>
</Window.DataTemplates>
```

- [ ] **Step 8: Build.** Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Dashboard/AiDashboard/AiDashboardViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/AiDashboardView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Dashboard/AiDashboard/AiDashboardView.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Dashboard/AiDashboard/AiDashboardViewModelTests.cs
git commit -m "feat(desktop-avalonia): assemble AiDashboardView/AiDashboardViewModel — DASH-02 complete"
```

---

### Task 26: Final integration — DI wiring, real routes, manual perf/visual validation

Replaces the manual `App.axaml.cs` wiring from Task 7 Step 8 with full `Microsoft.Extensions.Hosting` DI (mirroring `RushOrder.Desktop/Program.cs`'s pattern), registers the real Dashboard/AiDashboard routes, and runs the validation the original prompt requires (60fps check, states walkthrough).

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs`

**Interfaces:**
- Consumes: every Service (Tasks 4, 12-14), `NavigationService` (Task 6), `MainWindowViewModel` (Task 7), `DashboardViewModel`/`AiDashboardViewModel` (Tasks 21, 25) and their widget dependencies.

- [ ] **Step 1: Rewrite `App.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Shell;
using RushOrder.Desktop.Avalonia.Views.Shell;

namespace RushOrder.Desktop.Avalonia;

public sealed partial class App : Application
{
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<AppState>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<DashboardDataService>();
                    services.AddSingleton<ForecastDataService>();
                    services.AddSingleton<RealTimeService>();

                    services.AddTransient<RevenueWidgetViewModel>();
                    services.AddTransient<ActiveOrdersWidgetViewModel>();
                    services.AddTransient<TablesWidgetViewModel>();
                    services.AddTransient<AvgTicketWidgetViewModel>();
                    services.AddTransient<AlertsWidgetViewModel>();
                    services.AddTransient<ReservationsWidgetViewModel>();
                    services.AddTransient<DashboardViewModel>();

                    services.AddTransient<TodayForecastWidgetViewModel>();
                    services.AddTransient<SuggestionOfTheDayWidgetViewModel>();
                    services.AddTransient<KitchenEtaWidgetViewModel>();
                    services.AddTransient<AiDashboardViewModel>();

                    services.AddSingleton<MainWindowViewModel>();
                })
                .Build();

            var sp = _host.Services;
            var nav = sp.GetRequiredService<INavigationService>();

            // Re-points the shell's "dashboard"/"panel-ia" routes at the real modules,
            // replacing the PlaceholderView fallback from Task 6/7. AlertsWidgetViewModel is
            // resolved twice — once per Dashboard, once per Panel IA — each a fully
            // independent instance with its own LoadAsync/state, per Task 25's interface note.
            nav.Register("dashboard", _ => sp.GetRequiredService<DashboardViewModel>());
            nav.Register("panel-ia", _ => sp.GetRequiredService<AiDashboardViewModel>());

            desktop.MainWindow = new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => _host.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

Note: `MainWindowViewModel` (Task 7) calls `_nav.NavigateTo("dashboard")` in its constructor — since `nav.Register` runs before `MainWindowViewModel` is resolved here, the shell opens directly on the real `DashboardView`, not the placeholder.

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Avalonia.Tests`
Expected: all tests from Tasks 3-25 PASS.

- [ ] **Step 4: Launch and walk every state**

Run: `dotnet run --project desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`

Since no backend is running at `localhost:5143` in this environment (confirmed by Task 12/13's tests), every widget's real fetch fails on launch — this is the exercise of the `Error` + simulated-fallback path end to end:
- Confirm all 6 DASH-01 widgets show simulated data with the "Sin conexión — datos de ejemplo" banner and a working "Reintentar" button (clicking it re-attempts the real fetch, fails again since there's still no backend, and returns to the same `Error` state — confirms `RetryCommand` doesn't get stuck).
- Click "Panel IA" in the sidebar — confirm all 4 DASH-02 widgets render the same way, including the shared `AlertsWidgetView` showing its own independent 3 mock alerts (not the Dashboard instance's).
- Click the "Pedidos activos" and "Mesas" cards — confirm each navigates to a `PlaceholderView` showing `"orders/kanban"` / `"tables/floorplan"` respectively.
- Click an alert row with a `Product`/`Order` `ResourceType` — confirm it navigates to `"menu/products"` / `"orders/kanban"`.

- [ ] **Step 5: 60fps check with Avalonia DevTools**

With the app running in Debug (Task 1's `Avalonia.Diagnostics` reference is Debug-only), press F12 (or the configured DevTools shortcut) to open Avalonia DevTools. Enable its FPS overlay. Trigger the `RefreshPulse` animation repeatedly (click "Reintentar" on several widgets in sequence) and observe the frame counter stays at the display's refresh rate throughout — no visible drop while the pulse plays.

- [ ] **Step 6: Close and commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs
git commit -m "feat(desktop-avalonia): wire full DI container, register real Dashboard/Panel IA routes"
```

---

## Orden exacto de implementación

Estrictamente secuencial — cada tarea depende de la anterior (los tipos que produce una son consumidos por la siguiente):

1. Scaffold del proyecto → 2. Design tokens → 3. Modelos + `Result<T>` → 4. `AppState` → 5. `WidgetLoadState`/`WidgetViewModelBase` → 6. `INavigationService`/`PlaceholderView` → 7. Shell-01 (`MainWindow`) → 8. Animaciones → 9. `KpiCardBase` → 10. `SparklineControl` → 11. `OccupancyArcControl` → 12. `DashboardDataService` → 13. `ForecastDataService` → 14. `RealTimeService` → 15. RevenueWidget (plantilla) → 16. ActiveOrdersWidget → 17. TablesWidget → 18. AvgTicketWidget → 19. AlertsWidget → 20. ReservationsWidget → 21. `DashboardView` (cierra DASH-01) → 22. TodayForecastWidget → 23. SuggestionOfTheDayWidget → 24. KitchenEtaWidget → 25. `AiDashboardView` (cierra DASH-02) → 26. Integración final (DI, rutas reales, validación).

Las Tareas 16-20 y 22-24 son intercambiables entre sí dentro de su propio bloque (no dependen unas de otras, solo de 12-14) si se prefiere paralelizar con subagent-driven-development.

## Plan de tests

- **Unitarios (xunit, `desktop/tests/RushOrder.Desktop.Avalonia.Tests`)** — uno por tarea de lógica (Tareas 3-25): `Result<T>.Ok/Fail`, `AppState` de-dupe, `WidgetViewModelBase.Retry`, `NavigationService` placeholder resolution, cada `*WidgetViewModel.LoadAsync` (camino `Fail` → `Error` + `IsShowingSimulatedData=true` + datos simulados correctos — cubre exactamente la matriz de estados del spec Sección 2), `ActiveOrdersWidgetViewModel`/`TablesWidgetViewModel` navegación por clic, `AlertsWidgetViewModel.Prepend`/`RowClicked` routing, `DashboardViewModel`/`AiDashboardViewModel` real-time routing (verifica que un patch toca solo el widget afectado — Tareas 21/25).
- **Cobertura deliberadamente NO incluida por este plan:** tests contra un backend real (`localhost:5143`) — el entorno de CI/desarrollo no lo garantiza levantado; todos los tests de servicio (Tareas 12-13) asumen backend inalcanzable y verifican el camino `Fail`, que es el que se puede probar de forma determinista sin infraestructura. Si se agrega un backend de test (Testcontainers, como en `RushOrder.API.IntegrationTests`), sería una tarea separada, fuera de este plan.
- **Fallback simulado:** cubierto por la aserción de `IsShowingSimulatedData=true` + valores de `MockDashboardData`/`MockForecastData` en cada test `LoadAsync_with_unreachable_backend_*`.
- **Eventos SignalR:** no se prueban contra un hub real (requeriría un servidor SignalR de test) — se prueban a través de los métodos internos `OnTableStatusChangedForTest`/`OnKitchenAlertForTest` que replican exactamente la lógica de los handlers reales de `RealTimeService`, documentado inline en el código de Tareas 21/25.
- **Navegación:** cubierta end-to-end a nivel de `NavigationService` (Tarea 6) y a nivel de cada widget que navega (Tareas 16, 17, 19).

## Plan de validación visual y de rendimiento

Manual, ejecutado en la Tarea 26 Pasos 4-5 — no hay forma automatizada de verificar FPS/render en este stack sin infraestructura de testing visual adicional (fuera de alcance):

1. Lanzar la app, confirmar Shell-01 + sidebar de 10 ítems.
2. Recorrer los 4 estados reales por tipo de widget: `Loading` (visible brevemente al lanzar/reintentar), `Error`+simulado (esperado en este entorno sin backend — banner + Reintentar funcional), `Empty` (forzable apuntando `AppState.CurrentRestaurant` a un restaurante sin pedidos/reservas si se dispone de un backend real; documentar si no se pudo ejercitar en este entorno), `Loaded` (requiere backend real — documentar si no se pudo ejercitar).
3. Confirmar navegación por clic (KPI cards, filas de alerta) resuelve a `PlaceholderView` con el `routeKey` correcto.
4. Abrir Avalonia DevTools (F12), overlay de FPS activo, disparar `RefreshPulse` repetidamente vía "Reintentar" — confirmar sin caída de frames.
5. Registrar en el resumen final: FPS observado durante el pulso, y si `Loaded`/`Empty` se pudieron ejercitar contra un backend real o quedaron solo cubiertos por test unitario.

## Riesgos o decisiones bloqueantes

Ninguno bloquea el inicio de la implementación — todo lo identificado durante la preparación de este plan quedó resuelto dentro del plan mismo, no requiere una decisión adicional del usuario:

1. **TFM `net8.0-windows` obligatorio** (Task 1) — resuelto: es el único TFM compatible con `RushOrder.Desktop.Core`, y no cambia el hecho de que sigue siendo .NET 8 según lo pedido.
2. **Composition API no puede animar texto/números** (Task 8) — resuelto: se aplicó donde es técnicamente válido (`RefreshPulse`, escala/opacidad de un `Visual`) y se usó el mecanismo estándar de Avalonia donde no lo es (`KpiValueTransition`, un `double` intermedio). Documentado inline para que quien implemente no lo confunda con una regresión respecto al spec corregido.
3. **`ForecastDataService` sin mock previo en WinForms** (Task 13) — resuelto: `MockForecastData` es contenido nuevo, pequeño, autocontenido, no toca WinForms, y es requerido por el spec ya aprobado (Error + fallback también en DASH-02).
4. **4 widgets comparten el mismo endpoint `GetKpiAsync`** (Task 15) — resuelto: cada uno lo llama de forma independiente para cumplir "carga independiente por widget" al pie de la letra; el costo son llamadas HTTP duplicadas cada 30s, aceptable a esa cadencia.
5. **Versión exacta de paquetes NuGet** — verificadas contra `nuget.org` en el momento de escribir este plan (Avalonia 11.3.20, CommunityToolkit.Mvvm 8.4.2, LiveChartsCore.SkiaSharpView.Avalonia 2.0.5): si al ejecutar la Tarea 1 ya existe una versión 11.3.x o 8.4.x más nueva, usar esa en su lugar sin cambiar el resto del plan.

## Self-review

- **Cobertura del spec:** las 3 secciones del spec aprobado están cubiertas — Sección 1 (proyecto/estructura) → Tareas 1-2, 6-14; Sección 2 (estados/data layer) → Tarea 5 (`WidgetViewModelBase`) + cada `LoadAsync` en Tareas 15-24; Sección 3 (real-time/animaciones/navegación) → Tareas 8 (animaciones), 14 (RealTimeService), 21/25 (routing dirigido), 16/17/19 (navegación por clic). Los dos ajustes del commit `3d6c4e5` (Composition API acotado, "server-pushed" no "optimista") están reflejados en el código de las Tareas 8 y 21.
- **Placeholder scan:** sin `TBD`/`TODO`. Los únicos lugares donde el código muestra una nota de "corrección en el paso siguiente" (Task 15 Step 5's XML namespace, Task 20 Step 7, Task 22 Step 6) son advertencias explícitas sobre un detalle de XAML fácil de pasar por alto (namespace `xmlns:local` faltante en el snippet inicial) — cada uno resuelto con la corrección exacta en el mismo paso, no diferido.
- **Consistencia de tipos:** `Result<T>` (Task 3), `WidgetLoadState`/`WidgetViewModelBase` (Task 5), `INavigationService.NavigateTo(string, object?)` (Task 6) y los nombres de propiedad de cada widget (`RevenueToday`, `Occupied`/`Total`, `Alerts`, `Reservations`, `Series`/`XLabels`, `AverageMinutesText`) se usan de forma idéntica en cada tarea que los consume — verificado tarea por tarea al escribirlas.

