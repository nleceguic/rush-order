# TBL-01 — Plano de Mesas Avalonia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement TBL-01 (Plano de mesas) in `RushOrder.Desktop.Avalonia` — a zoomable/pannable floor plan canvas showing tables by state (libre/ocupada/reservada), an edit mode for drag/resize with persisted-position save, a selection side panel with a "Crear pedido" CTA navigating to `orders/new`, and live state updates via `RealTimeService`, all at 60fps with 30+ tables on screen.

**Architecture:** One lightweight `TableVisual` control per table (not a single monolithic custom-painted canvas like the WinForms original) hosted in an `ItemsControl` over a `Canvas` panel. Live drag/resize interactions use `RenderTransform` (never touch layout-affecting properties per frame); the floor plan's pan/zoom is one `RenderTransform` on the canvas host. A real-time `TableStatusChanged` patch mutates exactly one `TableItemViewModel`'s bound properties — no collection replace, no other `TableVisual` re-renders. No periodic polling timer exists for this module (unlike Dashboard/Panel IA) — the plan loads once, then lives entirely off `RealTimeService` push events, which sidesteps the periodic-refresh-blanks-to-Loading class of bug fixed in the Dashboard branch's final review.

**Tech Stack:** .NET 8 (`net8.0-windows`), Avalonia 11.3.20, `CommunityToolkit.Mvvm` 8.4.2 — same versions already pinned in this repo, no new packages.

**Predecessor:** `docs/superpowers/plans/2026-09-04-dashboard-panel-ia-avalonia-implementation.md`, merged to `master` at `428bfb6`. This plan starts from that state and reuses its established infrastructure verbatim: `Result<T>` (`Models/Result.cs`), `INavigationService`/`NavigationService`/`PlaceholderViewModel` (`Navigation/`), `RealTimeService` (`Services/RealTimeService.cs`), `AppState` (`Services/AppState.cs`), `WidgetLoadState` (`ViewModels/WidgetLoadState.cs`), `WidgetStateConverters` (`ViewModels/WidgetStateConverters.cs`), `RefreshPulse`/`CardStateTransition` (`Animations/`), `DesignTokens.axaml` (`Styles/`), the `Window.DataTemplates` registration pattern in `Views/Shell/MainWindow.axaml`, and the `Host.CreateDefaultBuilder()` DI wiring in `App.axaml.cs`.

**Spec status:** No standalone TBL-01 spec document exists in `docs/superpowers/specs/`. This plan's functional/performance requirements come directly from the human's detailed implementation-phase brief (equivalent in specificity to the brief that originally drove the Dashboard/Panel IA spec, per the fast-path workflow already agreed for Tables/Orders) plus the verified technical research in the "API Research Findings" section below. No new spec file is created.

## Technical Findings From Investigation (read before implementing)

**Backend limitation, human-confirmed decision:** the backend has no storage for a table's `Width`/`Height` at all (`TableService.SavePositionsAsync` in the WinForms app only ever sends `positionX`/`positionY`; `TableFloorPlanDto` carries no size fields). Resize in edit mode is therefore **visual/session-only** — dragging a resize handle changes the in-memory `TableItemViewModel.Width`/`.Height` and "Guardar cambios" persists position only; the size reverts to its loaded/default value on the next load. This is not a regression — the existing WinForms floor plan has the exact same silent limitation today. Task 6 below documents this explicitly in the Save flow and in a UI-visible note; do not attempt to persist size to the backend.

**Real-time sync errors never discard known-good state.** Unlike the Dashboard/Panel IA widgets (which show simulated fallback data and an error banner INSTEAD of stale content on a failed fetch), the floor plan's spec explicitly requires the opposite for anything after the first load: a `RealTimeService` disconnect shows a small non-blocking banner, and the canvas keeps rendering the last known-good `Tables` collection untouched. This is why `TableFloorPlanViewModel` does not inherit `WidgetViewModelBase` — that base class's contract (`Retry` re-running `LoadAsync`, `IsShowingSimulatedData` implying replaced content) is shaped for a small KPI card that's fully replaced on every state change, not a canvas that must never be torn down under the user while they're mid-drag. `TableFloorPlanViewModel` reuses the `WidgetLoadState` enum (identical 4 values needed: `Loading`/`Loaded`/`Empty`/`Error`) as its own `State` property type — avoiding a duplicate enum — but defines its own minimal state-holding contract by hand instead of inheriting the base class built for a different UI shape.

**No active-orders list in the detail panel.** WinForms' `TableDetailPanel` shows an "active orders" list fed by `TableService.GetTableOrdersAsync`, which the WinForms code itself documents as always falling back to mock data (`OrdersController has no tableId filter`). The human's brief for this phase asks for table info + a "Crear pedido" CTA, not an orders list — so this phase's detail panel intentionally omits it rather than shipping a list that can never show real data. This can be added when ORD-02 exists.

## API Research Findings (verified against Avalonia's actual behavior, not assumed)

- **`RenderTransform` does not affect layout.** Confirmed via Avalonia's own docs (`docs.avaloniaui.net/docs/graphics-animation/render-vs-layout-transforms`): *"A render transform changes how a control is drawn without affecting layout — the control's position and size in the layout system remain unchanged, and other controls do not move to accommodate the transform."* The docs explicitly recommend `RenderTransform` over `LayoutTransformControl` for anything performance-sensitive, since a layout transform "triggers a full layout pass" on every change. This is the mechanism this plan uses for both per-table drag/resize-preview and canvas-wide pan/zoom.
- **Avalonia's pointer hit-testing correctly accounts for `RenderTransform` on an element and its ancestors.** This was a real, known Avalonia bug (GitHub issue [AvaloniaUI/Avalonia#1558](https://github.com/AvaloniaUI/Avalonia/issues/1558), "RenderTransform is not taken into account when processing pointer events") — but it was fixed by the PR "Respect RenderTransform in GetPosition" and closed on 2019-03-02, years before the Avalonia 11.x line existed. Verified via `gh api repos/AvaloniaUI/Avalonia/issues/1558` directly against the GitHub API — closed, with a merged fix, not just stale. This means a `ScaleTransform`+`TranslateTransform` applied to the canvas host for zoom/pan does **not** require manual inverse-matrix coordinate math for child hit-testing — Avalonia's own `PointerPressed`/`PointerMoved` routing and `e.GetPosition(control)` already resolve correctly through the transform. This is a deliberate simplification versus the WinForms original, which hand-rolls `ScreenToCanvas()` inverse math because GDI+ has no such transform-aware input pipeline.
- **The Composition API (`ElementComposition.GetElementVisual`, `Compositor.CreateVector3KeyFrameAnimation`, already used by `Animations/RefreshPulse.cs`) is for genuine visual-level animations that should run smoothly independent of UI-thread frame pacing** — confirmed via `docs.avaloniaui.net/docs/graphics-animation/composition-animations`. It is not needed for direct 1:1 pointer-following drag (a plain synchronous `RenderTransform` assignment on `PointerMoved` is simpler and sufficient there, and is what WPF/Avalonia apps conventionally use for direct-manipulation drag). This plan reserves the Composition API for exactly one thing, matching the Dashboard precedent: `RefreshPulse.Play(tableVisual)` when a table's `State` changes via a real-time patch — the same call already built and reviewed in the Dashboard branch, reused verbatim, not reimplemented.
- **`Canvas`'s arrange pass has no inter-child dependency** — a `Canvas` positions each child independently at its own `Canvas.Left`/`Canvas.Top`, sized to its own `Width`/`Height`; changing one child's position or size only re-arranges that one child, never its siblings (this is standard, extremely well-established `Canvas` behavior shared across WPF/UWP/Avalonia's near-identical `Canvas` implementations — not something this plan treats as novel or needing its own citation, but stated here because it's *why* committing a table's final `X`/`Y`/`Width`/`Height` once per drag/resize (rather than every `PointerMoved`) is cheap and does not cause the "full relayout" the human's brief explicitly warns against).
- **No third-party pan/zoom library is introduced** (a `PanAndZoom` NuGet package by wieslawsoltes exists for Avalonia, found during research) — matching this project's established minimal-dependency convention (LiveCharts was the one exception, justified by chart-rendering complexity no token/control could reasonably replace). Zoom/pan here is ~40 lines of transform math this plan writes directly, mirroring the WinForms original's own formula, not a new dependency.

## Global Constraints

- New files only, added to the existing `RushOrder.Desktop.Avalonia` project and its test project. Never modify `RushOrder.Desktop` (WinForms) or `RushOrder.Desktop.Core`.
- No shared classlib — `Models`/`Services` for tables are new files in the Avalonia project, following the exact duplication precedent already established (per the Dashboard plan's Global Constraints, carried forward unchanged).
- Target framework `net8.0-windows`, Avalonia 11.3.20, `CommunityToolkit.Mvvm` 8.4.2 — no version changes, no new NuGet packages.
- `TableService` returns `Result<T>` from the real fetch only — same no-silent-mock-fallback discipline as `DashboardDataService`/`ForecastDataService`. `MockTableData` is called by `TableFloorPlanViewModel` on `Result<T>.Fail`, never by the service itself.
- Drag and resize interactions must use `RenderTransform` for the live, per-frame visual — never a bound `Width`/`Height`/`Canvas.Left`/`Canvas.Top` update on every `PointerMoved`. The persisted/committed value updates exactly once, on `PointerReleased`.
- Canvas pan/zoom is one `RenderTransform` (a `TransformGroup` of `ScaleTransform` + `TranslateTransform`) on the canvas host — never a per-`PointerMoved` recomputation of the visual tree or of any child's own properties.
- A `RealTimeService.TableStatusChanged` event must mutate only the one affected `TableItemViewModel`'s properties — never `ObservableCollection.Clear()`/re-`Add()` the whole `Tables` collection, never call `InitializeAsync()`/`LoadAsync()`.
- A `RealTimeService` disconnect/reconnect must never clear or replace an already-loaded `Tables` collection — only a boolean "sync lost" indicator toggles.
- Resize does not persist `Width`/`Height` to the backend (documented backend limitation, human-confirmed) — `SavePositionsAsync` sends `positionX`/`positionY` only, matching the existing `SavePositionRequest` shape already used by `DashboardModels.cs`'s WinForms counterpart.
- No role/permission system — a single `CanEditLayout()` method on `TableFloorPlanViewModel` returning `true` unconditionally is the only extension point, matching the human's explicit "no full permission system yet" instruction.
- No `ViewLocator` — routes register explicit `DataTemplate` entries in `Views/Shell/MainWindow.axaml`'s existing `Window.DataTemplates` block, exactly like Tasks 7/21/25 of the Dashboard plan.
- `orders/new` remains a placeholder route (resolves to `PlaceholderView`) — ORD-02 is out of scope for this phase. The selected table is passed as the `parameter` argument to `INavigationService.NavigateTo`, using the navigation signature already in place (`NavigateTo(string routeKey, object? parameter = null)`) — no signature change needed.
- Every color/spacing/radius value comes from `DesignTokens.axaml`'s existing tokens (`Space1..7`/`SpaceThickness1..7`/`RadiusSm`/`RadiusLg`/`RadiusPill`/brand and semantic brushes) — no hardcoded hex/px, and `Margin`/`Padding` (`Thickness`-typed) must use `SpaceThicknessN`, never `x:Double SpaceN` directly (the exact bug caught and fixed twice in the Dashboard branch).

---

### Task 1: Table models

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Models/TableModels.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `TableDto`, `TableState`, `TableShapeType`, `SavePositionRequest` — consumed by `TableService` (Task 2), `TableItemViewModel` (Task 3), and every later task.

- [ ] **Step 1: Create `Models/TableModels.cs`** (ported verbatim in shape from the WinForms `Models/DashboardModels.cs`'s table-related records — property names, order, and types match exactly, since `TableViewModel`s in Tasks 3+ and `TableService` in Task 2 are written against this exact shape)

```csharp
namespace RushOrder.Desktop.Avalonia.Models;

public sealed record TableDto(
    Guid Id,
    int Number,
    int Capacity,
    TableState State,
    TableShapeType ShapeType,
    float X,
    float Y,
    float Width,
    float Height,
    bool HasPendingOrder,
    DateTimeOffset? OccupiedSince,
    string? CurrentWaiter);

public enum TableState { Free, Occupied, Reserved, Cleaning }
public enum TableShapeType { Rectangular, Circular }

public sealed record SavePositionRequest(Guid Id, float X, float Y);
```

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Models/TableModels.cs
git commit -m "feat(desktop-avalonia): add table models (TBL-01)"
```

---

### Task 2: `TableService` + `MockTableData`

Same `Result<T>` discipline as `DashboardDataService`/`ForecastDataService` — the service never substitutes mock data on failure; `TableFloorPlanViewModel` (Task 4) does that after seeing a `Result<T>.Fail`. Only `GetTablesAsync` and `SavePositionsAsync` are ported — `GetTableOrdersAsync` and `UpdateTableStateAsync` are intentionally not (see "Technical Findings" above: no active-orders list in this phase, and table state changes arrive via `RealTimeService`, never a client-initiated PUT, in this phase's scope).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/TableService.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Services/MockTableData.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/TableServiceTests.cs`

**Interfaces:**
- Consumes: `AppState` (existing), `Result<T>`/`TableDto` (Task 1).
- Produces: `TableService.GetTablesAsync(CancellationToken)` → `Task<Result<IReadOnlyList<TableDto>>>`; `.SavePositionsAsync(IEnumerable<SavePositionRequest>, CancellationToken)` → `Task` (throws `InvalidOperationException` listing failed IDs, matching the WinForms original's already-correct behavior of surfacing partial-save failures rather than swallowing them) — consumed by `TableFloorPlanViewModel` (Task 4).

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Services;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Services;

public class TableServiceTests
{
    [Fact]
    public async Task GetTablesAsync_returns_Fail_when_the_backend_is_unreachable()
    {
        var service = new TableService(new AppState(), Microsoft.Extensions.Logging.Abstractions.NullLogger<TableService>.Instance);

        var result = await service.GetTablesAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — `TableService` doesn't exist yet.

- [ ] **Step 3: Create `Services/MockTableData.cs`** (a 6-table subset of the WinForms `TableService.MockTables()` grid — enough to exercise all 3 visible states plus one pending-order table; Task 18's 30+-table stress data is separate, synthetic, and does not reuse this small illustrative set)

```csharp
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public static class MockTableData
{
    public static IReadOnlyList<TableDto> Tables()
    {
        var now = DateTimeOffset.Now;
        return
        [
            new(Guid.NewGuid(), 1, 2, TableState.Occupied, TableShapeType.Circular, 40, 40, 80, 80, true, now.AddMinutes(-55), "Ana"),
            new(Guid.NewGuid(), 2, 2, TableState.Free, TableShapeType.Circular, 140, 40, 80, 80, false, null, null),
            new(Guid.NewGuid(), 3, 4, TableState.Reserved, TableShapeType.Rectangular, 260, 40, 140, 90, false, null, null),
            new(Guid.NewGuid(), 4, 4, TableState.Occupied, TableShapeType.Circular, 40, 170, 100, 100, false, now.AddMinutes(-20), "Carlos"),
            new(Guid.NewGuid(), 5, 6, TableState.Free, TableShapeType.Rectangular, 180, 170, 140, 90, false, null, null),
            new(Guid.NewGuid(), 6, 2, TableState.Cleaning, TableShapeType.Circular, 360, 170, 80, 80, false, null, null),
        ];
    }
}
```

- [ ] **Step 4: Create `Services/TableService.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.Services;

public sealed class TableService
{
    private const string BaseUrl = "http://localhost:5143/api/v1/tables";

    private readonly AppState _state;
    private readonly ILogger<TableService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public TableService(AppState state, ILogger<TableService> logger)
    {
        _state = state;
        _logger = logger;
    }

    // /floorplan (not the plain list) is the endpoint that returns position + active-order
    // info; the backend still has no Width/Height/ShapeType/OccupiedSince/CurrentWaiter
    // storage at all, so those are defaulted in MapFloorPlan — matches the WinForms original.
    public async Task<Result<IReadOnlyList<TableDto>>> GetTablesAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuth();
            var id = _state.CurrentRestaurant?.Id;
            var response = await _http.GetAsync($"{BaseUrl}/floorplan?restaurantId={id}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var rows = JsonConvert.DeserializeObject<ApiEnvelope<List<TableFloorPlanDto>>>(json)?.Data
                       ?? throw new InvalidOperationException("Empty floorplan payload");

            IReadOnlyList<TableDto> tables = rows.Select(MapFloorPlan).ToList();
            return Result<IReadOnlyList<TableDto>>.Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tables fetch failed");
            return Result<IReadOnlyList<TableDto>>.Fail(ex);
        }
    }

    private static TableDto MapFloorPlan(TableFloorPlanDto t)
    {
        var state = Enum.TryParse<TableState>(t.Status, out var s) ? s : TableState.Free;
        var x = (float)(t.PositionX ?? 40);
        var y = (float)(t.PositionY ?? 40);
        return new TableDto(t.Id, ParseTableNumber(t.Name), t.Capacity, state, TableShapeType.Circular,
            x, y, 80, 80, t.ActiveOrderCount > 0, null, null);
    }

    // Backend tables are named "Mesa N" — desktop models the number, not the name.
    private static int ParseTableNumber(string name)
    {
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    // Position-only, matching the confirmed backend limitation — Width/Height cannot persist.
    // A partial failure surfaces via a thrown exception naming the failed IDs (never swallowed),
    // matching the WinForms original's already-correct behavior.
    public async Task SavePositionsAsync(IEnumerable<SavePositionRequest> positions, CancellationToken ct = default)
    {
        SetAuth();
        var failedIds = new List<Guid>();

        foreach (var req in positions)
        {
            var body = JsonConvert.SerializeObject(new { positionX = (double)req.X, positionY = (double)req.Y });
            var response = await _http.PutAsync($"{BaseUrl}/{req.Id}",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Saving position for table {TableId} failed with {StatusCode}", req.Id, response.StatusCode);
                failedIds.Add(req.Id);
            }
        }

        if (failedIds.Count > 0)
            throw new InvalidOperationException($"No se pudo guardar la posición de {failedIds.Count} mesa(s): {string.Join(", ", failedIds)}");
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;
}

// Matches backend's TableFloorPlanDto (Tables/DTOs/TableFloorPlanDto.cs).
internal sealed record TableFloorPlanDto(
    Guid Id, string Name, int Capacity, string? Zone, string Status,
    double? PositionX, double? PositionY, int ActiveOrderCount, string? CurrentOrderNumber);

internal sealed class ApiEnvelope<T> { public string Status { get; set; } = ""; public T? Data { get; set; } }
```

- [ ] **Step 5: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/TableService.cs desktop/src/RushOrder.Desktop.Avalonia/Services/MockTableData.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Services/TableServiceTests.cs
git commit -m "feat(desktop-avalonia): add TableService (Result<T>, no silent mock fallback) (TBL-01)"
```

---

### Task 3: `TableItemViewModel`

The per-table bindable item — one instance per table, held in `TableFloorPlanViewModel.Tables` (Task 4). `X`/`Y`/`Width`/`Height` are the **committed** values (bound to `Canvas.Left`/`Canvas.Top`/`Width`/`Height` in Task 9) — live drag/resize preview never touches these mid-gesture (Task 9/11 use a `RenderTransform` instead), only on `PointerReleased`.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableItemViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableItemViewModelTests.cs`

**Interfaces:**
- Consumes: `TableDto` (Task 1).
- Produces: `TableItemViewModel` with `[ObservableProperty]` `State`, `X`, `Y`, `Width`, `Height`, `HasPendingOrder`, `OccupiedSince`, `CurrentWaiter`, `IsSelected`; read-only `Id`/`Number`/`Capacity`/`ShapeType`; `ToDto()`/`FromDto(TableDto)` for load/snapshot round-tripping; `OccupancyLabel()` — consumed by `TableFloorPlanViewModel` (Task 4), `TableVisual` (Task 9), `TableDetailPanelView` (Task 14).

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Models;
using RushOrder.Desktop.Avalonia.ViewModels.Tables;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Tables;

public class TableItemViewModelTests
{
    [Fact]
    public void FromDto_and_ToDto_round_trip_every_field()
    {
        var dto = new TableDto(Guid.NewGuid(), 7, 4, TableState.Occupied, TableShapeType.Circular,
            120f, 80f, 100f, 100f, true, DateTimeOffset.Now.AddMinutes(-10), "Ana");

        var vm = TableItemViewModel.FromDto(dto);
        var roundTripped = vm.ToDto();

        Assert.Equal(dto, roundTripped);
    }

    [Fact]
    public void OccupancyLabel_returns_empty_string_when_not_occupied()
    {
        var dto = new TableDto(Guid.NewGuid(), 1, 2, TableState.Free, TableShapeType.Circular, 0, 0, 80, 80, false, null, null);
        var vm = TableItemViewModel.FromDto(dto);

        Assert.Equal("", vm.OccupancyLabel());
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `ViewModels/Tables/TableItemViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public sealed partial class TableItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public int Number { get; }
    public int Capacity { get; }
    public TableShapeType ShapeType { get; }

    [ObservableProperty] private TableState _state;
    [ObservableProperty] private float _x;
    [ObservableProperty] private float _y;
    [ObservableProperty] private float _width;
    [ObservableProperty] private float _height;
    [ObservableProperty] private bool _hasPendingOrder;
    [ObservableProperty] private DateTimeOffset? _occupiedSince;
    [ObservableProperty] private string? _currentWaiter;
    [ObservableProperty] private bool _isSelected;

    private TableItemViewModel(Guid id, int number, int capacity, TableShapeType shapeType)
    {
        Id = id; Number = number; Capacity = capacity; ShapeType = shapeType;
    }

    public static TableItemViewModel FromDto(TableDto dto) => new(dto.Id, dto.Number, dto.Capacity, dto.ShapeType)
    {
        State = dto.State,
        X = dto.X,
        Y = dto.Y,
        Width = dto.Width,
        Height = dto.Height,
        HasPendingOrder = dto.HasPendingOrder,
        OccupiedSince = dto.OccupiedSince,
        CurrentWaiter = dto.CurrentWaiter,
    };

    public TableDto ToDto() => new(Id, Number, Capacity, State, ShapeType, X, Y, Width, Height, HasPendingOrder, OccupiedSince, CurrentWaiter);

    public string OccupancyLabel()
    {
        if (OccupiedSince is null) return "";
        var elapsed = DateTime.Now - OccupiedSince.Value.LocalDateTime;
        return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}";
    }
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableItemViewModel.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableItemViewModelTests.cs
git commit -m "feat(desktop-avalonia): add TableItemViewModel (TBL-01)"
```

---

### Task 4: `TableFloorPlanViewModel` — core (construction, load, state)

Does **not** inherit `WidgetViewModelBase` — see "Technical Findings" above for why (the base class's contract assumes full-replace-on-error semantics that would violate "never discard known-good state on a sync error"). Reuses the `WidgetLoadState` enum type directly. This task covers construction and the one-time initial load only; edit mode (Task 5), selection/navigation (Task 6), and real-time wiring (Task 7) are separate tasks that extend this same class.

**Out of scope, explicitly (matches the human's brief, not an oversight):** no "add table" / "delete table" commands — this phase edits position/size of tables that already exist. No active-orders list (see "Technical Findings"). No role/permission system beyond the single `CanEditLayout()` extension point.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs`

**Interfaces:**
- Consumes: `TableService` (Task 2), `TableItemViewModel` (Task 3), `INavigationService`/`RealTimeService` (existing).
- Produces: `TableFloorPlanViewModel` with `Tables` (`ObservableCollection<TableItemViewModel>`), `State` (`WidgetLoadState`), `IsShowingSimulatedData`, `RetryCommand`, `CanEditLayout()`, `InitializeAsync()` — consumed by Tasks 5-7 (same class, extended) and Task 16 (assembly).

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels;
using RushOrder.Desktop.Avalonia.ViewModels.Tables;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.ViewModels.Tables;

public class TableFloorPlanViewModelTests
{
    private static TableFloorPlanViewModel MakeSut()
    {
        var appState = new AppState();
        var tables = new TableService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<TableService>.Instance);
        var nav = new NavigationService();
        var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);
        return new TableFloorPlanViewModel(tables, nav, realTime);
    }

    [Fact]
    public async Task InitializeAsync_with_unreachable_backend_sets_Error_and_loads_simulated_tables()
    {
        var vm = MakeSut();

        await vm.InitializeAsync();

        Assert.Equal(WidgetLoadState.Error, vm.State);
        Assert.True(vm.IsShowingSimulatedData);
        Assert.Equal(MockTableData.Tables().Count, vm.Tables.Count);
    }

    [Fact]
    public void CanEditLayout_returns_true_with_no_permission_system_yet()
    {
        var vm = MakeSut();

        Assert.True(vm.CanEditLayout());
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `ViewModels/Tables/TableFloorPlanViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public sealed partial class TableFloorPlanViewModel : ObservableObject, IDisposable
{
    private readonly TableService _tables;
    private readonly INavigationService _nav;
    private readonly RealTimeService _realTime;

    public ObservableCollection<TableItemViewModel> Tables { get; } = [];

    [ObservableProperty] private WidgetLoadState _state = WidgetLoadState.Loading;
    [ObservableProperty] private bool _isShowingSimulatedData;

    public TableFloorPlanViewModel(TableService tables, INavigationService nav, RealTimeService realTime)
    {
        _tables = tables;
        _nav = nav;
        _realTime = realTime;
    }

    /// <summary>Extension point for future role/permission checks (e.g. Owner/Manager only).
    /// Unconditionally true today — no permission system exists yet, per explicit scope.</summary>
    public bool CanEditLayout() => true;

    public async Task InitializeAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        State = WidgetLoadState.Loading;
        var result = await _tables.GetTablesAsync();
        var dtos = result.IsSuccess ? result.Value! : Services.MockTableData.Tables();

        Tables.Clear();
        foreach (var dto in dtos) Tables.Add(TableItemViewModel.FromDto(dto));

        if (result.IsSuccess)
        {
            State = dtos.Count == 0 ? WidgetLoadState.Empty : WidgetLoadState.Loaded;
            IsShowingSimulatedData = false;
        }
        else
        {
            State = WidgetLoadState.Error;
            IsShowingSimulatedData = true;
        }
    }

    [RelayCommand]
    private async Task Retry() => await LoadAsync();

    public void Dispose() { /* Task 7 adds RealTimeService unsubscription here */ }
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs
git commit -m "feat(desktop-avalonia): add TableFloorPlanViewModel core (load, state, CanEditLayout) (TBL-01)"
```

---

### Task 5: Edit mode — toggle, save, cancel

Extends `TableFloorPlanViewModel` (Task 4) with `IsEditMode`, `ToggleEditModeCommand`, `SaveLayoutCommand`, `CancelEditCommand`. A snapshot of every table's `X`/`Y`/`Width`/`Height` is taken on entering edit mode so `Cancel` can revert in-memory changes that were never saved. **`SaveLayoutCommand` persists position only** (`SavePositionRequest` has no size field — see "Technical Findings"/Global Constraints): a resize made in this session survives Cancel-then-re-edit within the same app run, but is not sent to the backend and will not survive the next load. `Views/Tables/TableFloorPlanView.axaml` (Task 16) surfaces this as a small, permanent note near the "Guardar cambios" button — not a one-time dismissible warning — so a user resizing a table is never surprised days later.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs` (append)

**Interfaces:**
- Consumes: `TableService.SavePositionsAsync()` (Task 2).
- Produces: `IsEditMode`, `IsSaving`, `SaveErrorMessage`, `ToggleEditModeCommand`, `SaveLayoutCommand`, `CancelEditCommand` — consumed by `TableFloorPlanView`'s toolbar (Task 15) and `TableVisual`'s drag/resize gating (Tasks 9, 11 — dragging/resizing is only enabled `if (IsEditMode)`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task ToggleEditMode_then_CancelEdit_reverts_an_in_memory_move()
{
    var vm = MakeSut();
    await vm.InitializeAsync();
    var table = vm.Tables[0];
    var originalX = table.X;

    vm.ToggleEditModeCommand.Execute(null);
    table.X = originalX + 500; // simulates a drag commit (Task 9) without a real drag
    vm.CancelEditCommand.Execute(null);

    Assert.Equal(originalX, table.X);
    Assert.False(vm.IsEditMode);
}

[Fact]
public async Task SaveLayout_sends_position_only_and_exits_edit_mode_on_success()
{
    // Backend unreachable in this environment — SavePositionsAsync throws (every position
    // PUT fails), so this exercises the failure path: SaveErrorMessage is set, edit mode
    // is NOT exited on failure (the user should not lose their in-progress edits silently).
    var vm = MakeSut();
    await vm.InitializeAsync();
    vm.ToggleEditModeCommand.Execute(null);

    await vm.SaveLayoutCommand.ExecuteAsync(null);

    Assert.NotNull(vm.SaveErrorMessage);
    Assert.True(vm.IsEditMode);
    Assert.False(vm.IsSaving);
}
```

Add these inside the existing `TableFloorPlanViewModelTests` class from Task 4.

- [ ] **Step 2: Run them to verify they fail.** Expected: FAIL — members don't exist yet.

- [ ] **Step 3: Extend `ViewModels/Tables/TableFloorPlanViewModel.cs`**

Add to the class body (alongside the existing members from Task 4):

```csharp
[ObservableProperty] private bool _isEditMode;
[ObservableProperty] private bool _isSaving;
[ObservableProperty] private string? _saveErrorMessage;

private List<Models.TableDto>? _editModeSnapshot;

[RelayCommand]
private void ToggleEditMode()
{
    if (!CanEditLayout()) return;

    if (!IsEditMode)
    {
        _editModeSnapshot = Tables.Select(t => t.ToDto()).ToList();
        IsEditMode = true;
    }
    else
    {
        IsEditMode = false;
        _editModeSnapshot = null;
    }
}

[RelayCommand]
private void CancelEdit()
{
    if (_editModeSnapshot is not null)
    {
        foreach (var dto in _editModeSnapshot)
        {
            var table = Tables.FirstOrDefault(t => t.Id == dto.Id);
            if (table is null) continue;
            table.X = dto.X;
            table.Y = dto.Y;
            table.Width = dto.Width;
            table.Height = dto.Height;
        }
    }
    IsEditMode = false;
    _editModeSnapshot = null;
    SaveErrorMessage = null;
}

[RelayCommand]
private async Task SaveLayout()
{
    IsSaving = true;
    SaveErrorMessage = null;
    try
    {
        var positions = Tables.Select(t => new Models.SavePositionRequest(t.Id, t.X, t.Y));
        await _tables.SavePositionsAsync(positions);
        IsEditMode = false;
        _editModeSnapshot = null;
    }
    catch (Exception ex)
    {
        SaveErrorMessage = ex.Message;
    }
    finally
    {
        IsSaving = false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass.** Expected: PASS.

- [ ] **Step 5: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs
git commit -m "feat(desktop-avalonia): add edit mode toggle/save/cancel to TableFloorPlanViewModel (TBL-01)"
```

---

### Task 6: Selection + "Crear pedido" navigation

Extends `TableFloorPlanViewModel` with `SelectedTable`, `SelectTableCommand`, `ClearSelectionCommand`, `CreateOrderCommand`. Navigation reuses `INavigationService.NavigateTo(string, object?)` exactly as-is (no signature change) — `"orders/new"` is a brand-new route key, unrelated to the already-registered `"orders/kanban"` placeholder (Dashboard's Active Orders widget), and currently resolves to `PlaceholderView` the same way every other not-yet-built route does.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs` (append)

**Interfaces:**
- Consumes: `INavigationService` (already a constructor dependency from Task 4).
- Produces: `SelectedTable`, `SelectTableCommand`, `ClearSelectionCommand`, `CreateOrderCommand` — consumed by `TableVisual`'s click handling (Task 9) and `TableDetailPanelView`'s CTA button (Task 14).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task CreateOrderCommand_navigates_to_orders_new_with_the_selected_table()
{
    var appState = new AppState();
    var tables = new TableService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<TableService>.Instance);
    var nav = new NavigationService();
    var realTime = new RealTimeService(appState, Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeService>.Instance);
    var vm = new TableFloorPlanViewModel(tables, nav, realTime);
    await vm.InitializeAsync();
    var table = vm.Tables[0];

    vm.SelectTableCommand.Execute(table);
    vm.CreateOrderCommand.Execute(null);

    Assert.True(table.IsSelected);
    var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
    Assert.Equal("orders/new", placeholder.RouteKey);
    var passedTable = Assert.IsType<Models.TableDto>(placeholder.Parameter);
    Assert.Equal(table.Id, passedTable.Id);
}
```

Add inside the existing `TableFloorPlanViewModelTests` class.

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — members don't exist yet.

- [ ] **Step 3: Extend `ViewModels/Tables/TableFloorPlanViewModel.cs`**

Add to the class body:

```csharp
[ObservableProperty] private TableItemViewModel? _selectedTable;

[RelayCommand]
private void SelectTable(TableItemViewModel table)
{
    if (SelectedTable is not null) SelectedTable.IsSelected = false;
    SelectedTable = table;
    table.IsSelected = true;
}

[RelayCommand]
private void ClearSelection()
{
    if (SelectedTable is not null) SelectedTable.IsSelected = false;
    SelectedTable = null;
}

[RelayCommand]
private void CreateOrder()
{
    if (SelectedTable is null) return;
    _nav.NavigateTo("orders/new", SelectedTable.ToDto());
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs
git commit -m "feat(desktop-avalonia): add selection and Crear pedido navigation to TableFloorPlanViewModel (TBL-01)"
```

---

### Task 7: Real-time wiring — targeted table patch + non-blocking sync banner

**Applies the UI-thread-dispatch lesson from the Dashboard branch's final review from the start**, rather than needing a second pass to fix a crash: `RealTimeService`'s events are SignalR callbacks that run off the UI thread. Every handler below wraps its mutation in `Dispatcher.UIThread.InvokeAsync(...)`, exactly matching the pattern already fixed into `DashboardViewModel`/`AiDashboardViewModel` in that branch (commit `3759082`).

`TableStatusChanged(tableId, status)` mutates only the one matching `TableItemViewModel`'s `State`/`OccupiedSince` — never `Tables.Clear()`/re-`Add()`, never `InitializeAsync()`. `ConnectionChanged(bool)` only flips `IsSyncErrorVisible` — it never touches `Tables` or `State`, satisfying "el error de sincronización no debe borrar el último estado válido conocido" exactly: the canvas keeps rendering whatever it already had, with a small banner on top.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs` (append)

**Interfaces:**
- Consumes: `RealTimeService.TableStatusChanged`/`.ConnectionChanged` (already existing events, unmodified).
- Produces: `IsSyncErrorVisible` — consumed by `TableFloorPlanView`'s banner (Task 16).

- [ ] **Step 1: Write the failing test** (mirrors `DashboardViewModelTests`'s isolation-test shape — an internal test hook that IS the production handler, per the unified-method lesson from that branch's own fix)

```csharp
[Fact]
public async Task OnTableStatusChanged_patches_only_the_matching_table()
{
    var vm = MakeSut();
    await vm.InitializeAsync();
    var target = vm.Tables[0];
    var other = vm.Tables[1];
    var otherStateBefore = other.State;

    await vm.OnTableStatusChanged(target.Id.ToString(), "Reserved");

    Assert.Equal(TableState.Reserved, target.State);
    Assert.Equal(otherStateBefore, other.State);
}

[Fact]
public void OnConnectionChanged_toggles_the_sync_banner_without_touching_Tables()
{
    var vm = MakeSut();
    var countBefore = vm.Tables.Count;

    vm.OnConnectionChanged(false);
    Assert.True(vm.IsSyncErrorVisible);

    vm.OnConnectionChanged(true);
    Assert.False(vm.IsSyncErrorVisible);
    Assert.Equal(countBefore, vm.Tables.Count);
}
```

Add `using RushOrder.Desktop.Avalonia.Models;` to the test file if not already present (for `TableState`). Add both inside the existing `TableFloorPlanViewModelTests` class.

- [ ] **Step 2: Run them to verify they fail.** Expected: FAIL — members don't exist yet.

- [ ] **Step 3: Extend `ViewModels/Tables/TableFloorPlanViewModel.cs`**

Add `using Avalonia.Threading;` and `using RushOrder.Desktop.Avalonia.Models;` to the top, and add to the class body:

```csharp
[ObservableProperty] private bool _isSyncErrorVisible;

// Called from the constructor (Step 3b below).
private void WireRealTime()
{
    _realTime.TableStatusChanged += OnTableStatusChanged;
    _realTime.ConnectionChanged += OnConnectionChanged;
}

internal async Task OnTableStatusChanged(string tableId, string status)
{
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (!Guid.TryParse(tableId, out var id)) return;
        var table = Tables.FirstOrDefault(t => t.Id == id);
        if (table is null) return;
        if (!Enum.TryParse<TableState>(status, out var state)) return;

        table.State = state;
        table.OccupiedSince = state == TableState.Occupied
            ? (table.OccupiedSince ?? DateTimeOffset.Now)
            : null;
    });
}

internal void OnConnectionChanged(bool isConnected) => IsSyncErrorVisible = !isConnected;
```

- [ ] **Step 3b: Call `WireRealTime()` from the constructor and unsubscribe in `Dispose()`**

Update the constructor and `Dispose()` (both already exist from Task 4):

```csharp
public TableFloorPlanViewModel(TableService tables, INavigationService nav, RealTimeService realTime)
{
    _tables = tables;
    _nav = nav;
    _realTime = realTime;
    WireRealTime();
}
```

```csharp
public void Dispose()
{
    _realTime.TableStatusChanged -= OnTableStatusChanged;
    _realTime.ConnectionChanged -= OnConnectionChanged;
}
```

- [ ] **Step 4: Run tests to verify they pass.** Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/ViewModels/Tables/TableFloorPlanViewModelTests.cs
git commit -m "feat(desktop-avalonia): wire RealTimeService — targeted table patch, non-blocking sync banner (TBL-01)"
```

---

### Task 8: `TableStateColorConverter`

Maps `TableState` to the exact same design-token brushes the WinForms floor plan already uses (`TableShape.GetStateColor` in `desktop/src/RushOrder.Desktop/Views/Tables/TableShape.cs:60-67`, ported to token names): `Free` → `SuccessBrush`, `Occupied` → `RushRedBrush` (the app's one accent color, called `Primary` in WinForms' `ThemeManager`), `Reserved` → `InfoBrush`, `Cleaning` → `TextSecondaryBrush`.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableStateColorConverter.cs`

**Interfaces:**
- Consumes: `TableState` (Task 1), design tokens (existing).
- Produces: `TableStateColorConverter.Instance` (`IValueConverter`) — consumed by `TableVisual` (Task 9).

- [ ] **Step 1: Create `ViewModels/Tables/TableStateColorConverter.cs`** (same `global::Avalonia.Application` qualification precedent as `DeltaColorConverter`/`BoolBrushConverters` — required because this project's own root namespace, `RushOrder.Desktop.Avalonia`, shadows the global `Avalonia` namespace)

```csharp
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public sealed class TableStateColorConverter : IValueConverter
{
    public static readonly TableStateColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var key = value switch
        {
            TableState.Free => "SuccessBrush",
            TableState.Occupied => "RushRedBrush",
            TableState.Reserved => "InfoBrush",
            TableState.Cleaning => "TextSecondaryBrush",
            _ => "TextSecondaryBrush",
        };
        return global::Avalonia.Application.Current!.FindResource(key)!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableStateColorConverter.cs
git commit -m "feat(desktop-avalonia): add TableStateColorConverter (TBL-01)"
```

---

### Task 9: `TableVisual` control — rendering + drag

One `TableVisual` per table (not a single monolithic custom-painted control like the WinForms original). Shape (circular/rectangular), state color, selection ring, and the pending-order dot are all declarative XAML bound to `TableItemViewModel` (Task 3) — no custom `Render(DrawingContext)` override needed for this control, unlike `SparklineControl`/`OccupancyArcControl`.

**Drag mechanics (the Global Constraints' core requirement, applied concretely here):**
- `PointerPressed` records the press position (in the ancestor `Canvas`'s coordinate space — see below) and captures the pointer.
- `PointerMoved`, only while `IsEditMode` is true, computes a delta and sets `RenderTransform = new TranslateTransform(dx, dy)` — a pure render-time change per Avalonia's own docs (`docs/graphics-animation/render-vs-layout-transforms`: *"other controls do not move to accommodate the transform"*), so no measure/arrange pass runs on ANY table, including the one being dragged, for the whole gesture.
- `PointerReleased` commits exactly once: adds the total delta to the `TableItemViewModel`'s own `X`/`Y` (its `DataContext`, mutated directly — no event needed, mirroring how `TodayForecastWidgetView.axaml.cs` already mutates state via its own `DataContext` in code-behind), then resets `RenderTransform` to `null`. This single commit is the only point where `Canvas.Left`/`Canvas.Top` (bound to `X`/`Y` by Task 12's container style) changes — one `Canvas` arrange pass for one child, not per-frame.
- Positions are read via `e.GetPosition(ancestorCanvas)`, not `e.GetPosition(this)` — per the API Research Findings, Avalonia correctly resolves a point through a `RenderTransform`'d ancestor (the Canvas host's own zoom/pan transform from Task 12), so the returned coordinates are already in unscaled "table space" regardless of current zoom — the exact division the WinForms original does by hand (`(screen.X - _pan.X) / _zoom`) Avalonia performs internally. The ancestor `Canvas` reference is resolved once, on `OnAttachedToVisualTree`, via `this.FindAncestorOfType<Canvas>()` (a standard `Avalonia.VisualTree` extension method), not re-queried per pointer event.
- A plain click (press+release with total movement under a 3px threshold) does not move anything — it raises `Clicked` instead, regardless of `IsEditMode` (selection works in both modes; only dragging is edit-mode-gated).

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableVisualConverters.cs`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableItemViewModel.cs` (add the drag-delta helper)
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/TableDragMathTests.cs`

**Interfaces:**
- Consumes: `TableItemViewModel` (Task 3, as `DataContext`), `TableStateColorConverter` (Task 8).
- Produces: `TableVisual` with `IsEditMode` (`StyledProperty<bool>`, bound by Task 11's `ItemTemplate` to the floor plan VM's `IsEditMode`) and `event Action<TableItemViewModel>? Clicked` — consumed by `FloorPlanCanvasView` (Task 11) to wire selection.

- [ ] **Step 1: Write the failing test** — the drag delta math is extracted as a pure, static function so it's unit-testable without a live pointer/visual tree (full gesture behavior is validated end-to-end in Task 18's manual+headless walkthrough, matching how the Dashboard plan's own DevTools FPS check was necessarily manual, not unit-tested)

```csharp
using Avalonia;
using RushOrder.Desktop.Avalonia.Views.Tables;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Views.Tables;

public class TableDragMathTests
{
    [Fact]
    public void ComputeDelta_returns_the_difference_between_start_and_current()
    {
        var start = new Point(100, 80);
        var current = new Point(130, 65);

        var (dx, dy) = TableVisual.ComputeDelta(start, current);

        Assert.Equal(30f, dx);
        Assert.Equal(-15f, dy);
    }

    [Theory]
    [InlineData(2, 2, false)]   // under the 3px threshold — treated as a click, not a drag
    [InlineData(4, 0, true)]
    [InlineData(0, 4, true)]
    public void ExceedsDragThreshold_matches_the_3px_click_vs_drag_boundary(double dx, double dy, bool expected)
    {
        Assert.Equal(expected, TableVisual.ExceedsDragThreshold(dx, dy));
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Add the drag-delta helper to `ViewModels/Tables/TableItemViewModel.cs`**

Add `[NotifyPropertyChangedFor]` is not needed here (no computed property depends on `X`/`Y`) — no change to this file is actually required for Task 9; the drag math lives entirely in `TableVisual`. (This step intentionally left as a no-op — see Step 4's `TableVisual.cs` for the real logic. Struck through rather than removed so the file list above stays accurate to what Task 9 touches: only the 4 new files, not `TableItemViewModel.cs`.)

- [ ] **Step 4: Create `Views/Tables/TableVisual.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using RushOrder.Desktop.Avalonia.ViewModels.Tables;

namespace RushOrder.Desktop.Avalonia.Views.Tables;

public partial class TableVisual : UserControl
{
    private const double DragThresholdPixels = 3;

    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<TableVisual, bool>(nameof(IsEditMode));

    public bool IsEditMode { get => GetValue(IsEditModeProperty); set => SetValue(IsEditModeProperty, value); }

    public event Action<TableItemViewModel>? Clicked;

    private Canvas? _canvas;
    private Point? _dragStart;
    private bool _dragging;

    public TableVisual() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _canvas = this.FindAncestorOfType<Canvas>();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_canvas is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _dragStart = e.GetPosition(_canvas);
        _dragging = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerMovedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_canvas is null || _dragStart is null || !IsEditMode) return;

        var (dx, dy) = ComputeDelta(_dragStart.Value, e.GetPosition(_canvas));
        if (!_dragging && ExceedsDragThreshold(dx, dy)) _dragging = true;
        if (_dragging) RenderTransform = new TranslateTransform(dx, dy);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (_canvas is null || _dragStart is null) return;

        var (dx, dy) = ComputeDelta(_dragStart.Value, e.GetPosition(_canvas));

        if (_dragging && IsEditMode && DataContext is TableItemViewModel vm)
        {
            vm.X += (float)dx;
            vm.Y += (float)dy;
        }
        else if (!_dragging && DataContext is TableItemViewModel clicked)
        {
            Clicked?.Invoke(clicked);
        }

        RenderTransform = null;
        _dragStart = null;
        _dragging = false;
        e.Handled = true; // prevents the canvas host's empty-space click handler (Task 11) from clearing the selection this click just made
    }

    internal static (double dx, double dy) ComputeDelta(Point start, Point current) =>
        (current.X - start.X, current.Y - start.Y);

    internal static bool ExceedsDragThreshold(double dx, double dy) =>
        Math.Abs(dx) > DragThresholdPixels || Math.Abs(dy) > DragThresholdPixels;
}
```

- [ ] **Step 5: Run it to verify it passes.** Expected: PASS (4 tests).

- [ ] **Step 6: Create `ViewModels/Tables/TableVisualConverters.cs`**

```csharp
using Avalonia.Data.Converters;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public static class TableVisualConverters
{
    public static readonly IValueConverter IsCircular =
        new FuncValueConverter<TableShapeType, bool>(t => t == TableShapeType.Circular);
    public static readonly IValueConverter IsRectangular =
        new FuncValueConverter<TableShapeType, bool>(t => t == TableShapeType.Rectangular);
    public static readonly IValueConverter SelectionStrokeThickness =
        new FuncValueConverter<bool, double>(isSelected => isSelected ? 3 : 2);
}
```

- [ ] **Step 7: Create `Views/Tables/TableVisual.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:RushOrder.Desktop.Avalonia.ViewModels.Tables"
             x:Class="RushOrder.Desktop.Avalonia.Views.Tables.TableVisual"
             x:DataType="local:TableItemViewModel"
             Width="{Binding Width}" Height="{Binding Height}"
             Cursor="Hand">
  <Panel>
    <Ellipse IsVisible="{Binding ShapeType, Converter={x:Static local:TableVisualConverters.IsCircular}}"
             Fill="{DynamicResource SurfaceBrush}"
             Stroke="{Binding State, Converter={x:Static local:TableStateColorConverter.Instance}}"
             StrokeThickness="{Binding IsSelected, Converter={x:Static local:TableVisualConverters.SelectionStrokeThickness}}" />
    <Border IsVisible="{Binding ShapeType, Converter={x:Static local:TableVisualConverters.IsRectangular}}"
            Background="{DynamicResource SurfaceBrush}"
            BorderBrush="{Binding State, Converter={x:Static local:TableStateColorConverter.Instance}}"
            BorderThickness="{Binding IsSelected, Converter={x:Static local:TableVisualConverters.SelectionStrokeThickness}}"
            CornerRadius="{StaticResource RadiusLg}" />
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Spacing="2">
      <TextBlock Text="{Binding Number}" FontFamily="{StaticResource PoppinsFontFamily}"
                 FontWeight="Bold" FontSize="16" Foreground="{DynamicResource TextPrimaryBrush}"
                 HorizontalAlignment="Center" />
    </StackPanel>
    <Ellipse Width="10" Height="10" Fill="{DynamicResource RushRedBrush}"
             HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,-4,-4,0"
             IsVisible="{Binding HasPendingOrder}" />
  </Panel>
</UserControl>
```

(The occupancy-time label from the WinForms original is added in Task 11's data template alongside a small local UI-only refresh timer — kept out of this control so `TableVisual` itself stays a pure, timer-free binding target; see Task 11's note.)

- [ ] **Step 8: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableVisualConverters.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/TableDragMathTests.cs
git commit -m "feat(desktop-avalonia): add TableVisual — rendering + RenderTransform-based drag (TBL-01)"
```

---

### Task 10: Resize handles on `TableVisual`

Extends Task 9's `TableVisual` with four corner handles, visible only when the table is selected AND `IsEditMode` is true. Per the Global Constraints (resize is visual/session-only — the backend has no `Width`/`Height` column), a resize always ends by updating the in-memory `TableItemViewModel.Width`/`Height`/`X`/`Y` only; it is never sent to `TableService`.

**Why `ScaleTransform`, not live `Width`/`Height` mutation:** a `Canvas`'s arrange pass has no inter-child dependency, so mutating one child's `Width`/`Height` on every pointer-move would only ever re-arrange that one child — but it would still run an arrange pass on it 60+ times/sec. `RenderTransform` skips measure/arrange entirely per the same docs cited in Task 9, so it stays strictly cheaper even for a single affected child. This applies the "avoid continuously modifying layout-triggering properties" requirement consistently to both drag and resize, not just to the multi-sibling case.

**Corner-anchor math:** each handle scales around the OPPOSITE corner via `RenderTransformOrigin` (a relative point Avalonia scales `ScaleTransform` around without any manual translate compensation — `RenderTransformOrigin="1,1"` anchors the bottom-right corner, etc.). On release, the scale is converted back into absolute `Width`/`Height`/`X`/`Y` once:

| Handle | Anchor (`RenderTransformOrigin`) | Width/Height delta | X delta | Y delta |
|---|---|---|---|---|
| SE | `0,0` (top-left) | `+dx, +dy` | none | none |
| NW | `1,1` (bottom-right) | `-dx, -dy` | `oldWidth - newWidth` | `oldHeight - newHeight` |
| NE | `0,1` (bottom-left) | `+dx, -dy` | none | `oldHeight - newHeight` |
| SW | `1,0` (top-right) | `-dx, +dy` | `oldWidth - newWidth` | none |

Minimum size is clamped to 40px per side, matching the WinForms original's `MinSize`.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/TableResizeMathTests.cs`

**Interfaces:**
- Consumes: `TableItemViewModel.IsSelected` (Task 3), `TableVisual.IsEditMode` (Task 9).
- Produces: `TableVisual.ComputeResize(corner, startWidth, startHeight, startX, startY, dx, dy)` — an internal static pure function, reused as-is by the test and by the pointer handlers.

- [ ] **Step 1: Write the failing test**

```csharp
using RushOrder.Desktop.Avalonia.Views.Tables;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Views.Tables;

public class TableResizeMathTests
{
    [Fact]
    public void SE_handle_grows_size_and_keeps_the_top_left_corner_fixed()
    {
        var r = TableVisual.ComputeResize(ResizeCorner.SouthEast, 100, 80, 200, 150, dx: 20, dy: 10);
        Assert.Equal(120, r.Width);
        Assert.Equal(90, r.Height);
        Assert.Equal(200, r.X);
        Assert.Equal(150, r.Y);
    }

    [Fact]
    public void NW_handle_grows_size_and_moves_the_top_left_corner_by_the_opposite_delta()
    {
        var r = TableVisual.ComputeResize(ResizeCorner.NorthWest, 100, 80, 200, 150, dx: -20, dy: -10);
        Assert.Equal(120, r.Width);
        Assert.Equal(90, r.Height);
        Assert.Equal(180, r.X);
        Assert.Equal(140, r.Y);
    }

    [Fact]
    public void NE_handle_only_moves_Y_never_X()
    {
        var r = TableVisual.ComputeResize(ResizeCorner.NorthEast, 100, 80, 200, 150, dx: 20, dy: -10);
        Assert.Equal(120, r.Width);
        Assert.Equal(90, r.Height);
        Assert.Equal(200, r.X);
        Assert.Equal(140, r.Y);
    }

    [Fact]
    public void SW_handle_only_moves_X_never_Y()
    {
        var r = TableVisual.ComputeResize(ResizeCorner.SouthWest, 100, 80, 200, 150, dx: -20, dy: 10);
        Assert.Equal(120, r.Width);
        Assert.Equal(90, r.Height);
        Assert.Equal(180, r.X);
        Assert.Equal(150, r.Y);
    }

    [Fact]
    public void Shrinking_below_40px_clamps_to_the_minimum_size()
    {
        var r = TableVisual.ComputeResize(ResizeCorner.SouthEast, 50, 50, 200, 150, dx: -30, dy: -30);
        Assert.Equal(40, r.Width);
        Assert.Equal(40, r.Height);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — `ResizeCorner` / `ComputeResize` don't exist yet.

- [ ] **Step 3: Add the resize math and pointer handlers to `TableVisual.axaml.cs`**

```csharp
public enum ResizeCorner { NorthWest, NorthEast, SouthEast, SouthWest }

public partial class TableVisual : UserControl
{
    private const double MinSize = 40;

    // ... IsEditModeProperty, Clicked event, drag fields/methods from Task 9 unchanged ...

    private ResizeCorner? _resizingCorner;
    private Point? _resizeStart;
    private (double Width, double Height, float X, float Y) _resizeOrigin;

    public TableVisual()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachSelectionWatcher();
    }

    private void AttachSelectionWatcher()
    {
        if (DataContext is TableItemViewModel vm)
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TableItemViewModel.IsSelected)) UpdateHandlesVisibility(); };
        UpdateHandlesVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEditModeProperty) UpdateHandlesVisibility();
    }

    private void UpdateHandlesVisibility()
    {
        ResizeHandles.IsVisible = IsEditMode && DataContext is TableItemViewModel { IsSelected: true };
    }

    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_canvas is null || sender is not Control handle || handle.Tag is not ResizeCorner corner) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _resizingCorner = corner;
        _resizeStart = e.GetPosition(_canvas);
        _resizeOrigin = (Width, Height, ((TableItemViewModel)DataContext!).X, ((TableItemViewModel)DataContext!).Y);
        RenderTransformOrigin = corner switch
        {
            ResizeCorner.SouthEast => RelativePoint.TopLeft,
            ResizeCorner.NorthWest => RelativePoint.BottomRight,
            ResizeCorner.NorthEast => new RelativePoint(0, 1, RelativeUnit.Relative),
            ResizeCorner.SouthWest => new RelativePoint(1, 0, RelativeUnit.Relative),
            _ => RelativePoint.TopLeft,
        };
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    private void OnHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas is null || _resizingCorner is null || _resizeStart is null) return;

        var (dx, dy) = ComputeDelta(_resizeStart.Value, e.GetPosition(_canvas));
        var r = ComputeResize(_resizingCorner.Value, _resizeOrigin.Width, _resizeOrigin.Height, _resizeOrigin.X, _resizeOrigin.Y, dx, dy);
        RenderTransform = new ScaleTransform(r.Width / _resizeOrigin.Width, r.Height / _resizeOrigin.Height);
        e.Handled = true;
    }

    private void OnHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizingCorner is null || _resizeStart is null) return;

        if (DataContext is TableItemViewModel vm)
        {
            var (dx, dy) = ComputeDelta(_resizeStart.Value, e.GetPosition(_canvas!));
            var r = ComputeResize(_resizingCorner.Value, _resizeOrigin.Width, _resizeOrigin.Height, _resizeOrigin.X, _resizeOrigin.Y, dx, dy);
            vm.Width = r.Width;
            vm.Height = r.Height;
            vm.X = r.X;
            vm.Y = r.Y;
        }

        RenderTransform = null;
        RenderTransformOrigin = RelativePoint.TopLeft;
        e.Pointer.Capture(null);
        _resizingCorner = null;
        _resizeStart = null;
        e.Handled = true;
    }

    internal static (double Width, double Height, float X, float Y) ComputeResize(
        ResizeCorner corner, double startWidth, double startHeight, float startX, float startY, double dx, double dy)
    {
        var (rawW, rawH) = corner switch
        {
            ResizeCorner.SouthEast => (startWidth + dx, startHeight + dy),
            ResizeCorner.NorthWest => (startWidth - dx, startHeight - dy),
            ResizeCorner.NorthEast => (startWidth + dx, startHeight - dy),
            ResizeCorner.SouthWest => (startWidth - dx, startHeight + dy),
            _ => (startWidth, startHeight),
        };
        var width = Math.Max(MinSize, rawW);
        var height = Math.Max(MinSize, rawH);

        var x = corner is ResizeCorner.NorthWest or ResizeCorner.SouthWest ? startX + (float)(startWidth - width) : startX;
        var y = corner is ResizeCorner.NorthWest or ResizeCorner.NorthEast ? startY + (float)(startHeight - height) : startY;

        return (width, height, x, y);
    }
}
```

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS (5 tests, plus Task 9's 4 still passing).

- [ ] **Step 5: Add the four handles to `TableVisual.axaml`**

```xml
    <Grid Name="ResizeHandles" IsVisible="False">
      <Rectangle Tag="NorthWest" Width="10" Height="10" Fill="{DynamicResource RushDarkBrush}"
                 HorizontalAlignment="Left" VerticalAlignment="Top" Margin="-5" Cursor="TopLeftCorner"
                 PointerPressed="OnHandlePointerPressed" PointerMoved="OnHandlePointerMoved" PointerReleased="OnHandlePointerReleased" />
      <Rectangle Tag="NorthEast" Width="10" Height="10" Fill="{DynamicResource RushDarkBrush}"
                 HorizontalAlignment="Right" VerticalAlignment="Top" Margin="-5" Cursor="TopRightCorner"
                 PointerPressed="OnHandlePointerPressed" PointerMoved="OnHandlePointerMoved" PointerReleased="OnHandlePointerReleased" />
      <Rectangle Tag="SouthEast" Width="10" Height="10" Fill="{DynamicResource RushDarkBrush}"
                 HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="-5" Cursor="BottomRightCorner"
                 PointerPressed="OnHandlePointerPressed" PointerMoved="OnHandlePointerMoved" PointerReleased="OnHandlePointerReleased" />
      <Rectangle Tag="SouthWest" Width="10" Height="10" Fill="{DynamicResource RushDarkBrush}"
                 HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="-5" Cursor="BottomLeftCorner"
                 PointerPressed="OnHandlePointerPressed" PointerMoved="OnHandlePointerMoved" PointerReleased="OnHandlePointerReleased" />
    </Grid>
```

`Tag="NorthWest"` etc. sets a `string`, but `OnHandlePointerPressed` reads `handle.Tag is not ResizeCorner corner` expecting the enum — fix by setting `Tag` from code instead. Add to `TableVisual`'s constructor, after `InitializeComponent()`:

```csharp
        HandleNW.Tag = ResizeCorner.NorthWest;
        HandleNE.Tag = ResizeCorner.NorthEast;
        HandleSE.Tag = ResizeCorner.SouthEast;
        HandleSW.Tag = ResizeCorner.SouthWest;
```

(rename the four `Rectangle`s' `Tag="..."` attributes above to `x:Name="HandleNW"` etc. instead, so the constructor can reach them by name — the XAML `Tag` attributes are removed once this is in place.)

- [ ] **Step 6: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 7: Run the full test suite.** Expected: PASS, all tests from Tasks 1-10.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableVisual.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/TableResizeMathTests.cs
git commit -m "feat(desktop-avalonia): add corner resize handles to TableVisual — RenderTransformOrigin scaling (TBL-01)"
```

---

### Task 11: `FloorPlanCanvasView` — canvas host, pan, zoom

The host for all `TableVisual`s: an `ItemsControl` whose `ItemsPanel` is a plain `Canvas` (so each table positions independently via `Canvas.Left`/`Canvas.Top`, with no inter-child layout dependency), wrapped by one `RenderTransform` (`ScaleTransform` + `TranslateTransform`) that drives zoom and pan for the ENTIRE floor plan in one render-only step — panning/zooming never touches any individual `TableVisual`'s own properties or triggers a single measure/arrange pass anywhere in the tree, per the same `RenderTransform` docs cited in Tasks 9-10.

**Canvas.Left/Canvas.Top on generated containers:** `ItemsControl` generates a `ContentPresenter` per item; positioning them on the `Canvas` uses the standard Avalonia pattern of a `Style` scoped to the `ItemsControl` targeting `ContentPresenter`, binding the attached `Canvas.Left`/`Canvas.Top` properties straight to the item's `X`/`Y` — no code-behind loop over containers is needed, and this style never changes during drag (the live drag preview is entirely `TableVisual`'s own `RenderTransform` from Task 9; this binding only re-fires once, when `X`/`Y` commit on `PointerReleased`).

**Zoom-toward-pointer:** matches the WinForms original's formula (`docs` value clamp `[0.5, 2.0]`) — the content point under the cursor is computed from the OLD zoom/pan, then the NEW pan is solved so that same content point lands back under the cursor after the zoom changes. Extracted as a pure, testable function.

**Pan:** middle-mouse-button drag directly updates the `TranslateTransform`'s `X`/`Y` — a single property write on the host's own transform, not a Canvas-affecting property, so it never triggers layout either.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml.cs`
- Test: `desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/FloorPlanZoomMathTests.cs`

**Interfaces:**
- Consumes: `TableFloorPlanViewModel.Tables`/`IsEditMode`/`SelectTableCommand`/`ClearSelectionCommand` (Tasks 4-6), `TableVisual` (Tasks 9-10).
- Produces: `FloorPlanCanvasView`, `FloorPlanCanvasView.ComputeZoom(oldZoom, panX, panY, cursor, wheelDelta)` — consumed by the test only; the assembly view (Task 13) hosts this control directly, with `DataContext` flowing down from `TableFloorPlanViewModel`.

- [ ] **Step 1: Write the failing test**

```csharp
using Avalonia;
using RushOrder.Desktop.Avalonia.Views.Tables;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Views.Tables;

public class FloorPlanZoomMathTests
{
    [Fact]
    public void Zooming_in_keeps_the_content_point_under_the_cursor_fixed()
    {
        // Zoomed to 1.0, no pan, cursor at (300,200) -> content point is (300,200)
        var r = FloorPlanCanvasView.ComputeZoom(oldZoom: 1.0, panX: 0, panY: 0, cursor: new Point(300, 200), wheelDelta: 1);

        Assert.True(r.Zoom > 1.0);
        // Re-deriving the content point under the cursor at the NEW zoom/pan must still be (300,200)
        var contentX = (300 - r.PanX) / r.Zoom;
        var contentY = (200 - r.PanY) / r.Zoom;
        Assert.Equal(300, contentX, 3);
        Assert.Equal(200, contentY, 3);
    }

    [Theory]
    [InlineData(1.9, 1, 2.0)]   // clamps at the max
    [InlineData(0.55, -1, 0.5)] // clamps at the min
    public void Zoom_is_clamped_to_the_0_5_to_2_0_range(double oldZoom, double wheelDelta, double expected)
    {
        var r = FloorPlanCanvasView.ComputeZoom(oldZoom, 0, 0, new Point(0, 0), wheelDelta);
        Assert.Equal(expected, r.Zoom, 3);
    }
}
```

- [ ] **Step 2: Run it to verify it fails.** Expected: FAIL — type doesn't exist yet.

- [ ] **Step 3: Create `Views/Tables/FloorPlanCanvasView.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RushOrder.Desktop.Avalonia.ViewModels.Tables;

namespace RushOrder.Desktop.Avalonia.Views.Tables;

public partial class FloorPlanCanvasView : UserControl
{
    private const double MinZoom = 0.5;
    private const double MaxZoom = 2.0;
    private const double ZoomStep = 0.1;

    private Point? _panStart;
    private double _panStartX;
    private double _panStartY;

    public FloorPlanCanvasView()
    {
        InitializeComponent();
        TablesHost.AddHandler(InputElement.PointerPressedEvent, OnTableClickedRoutingSetup, RoutingStrategies.Bubble);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // TableVisual instances are generated per-item by the ItemsControl; each one's Clicked
    // event is wired the moment its container is realized, per the standard Avalonia
    // ContainerPrepared hook (fires once per generated container, including on scroll/reuse).
    private void OnTableClickedRoutingSetup(object? sender, PointerPressedEventArgs e) { /* placeholder replaced in Step 4 */ }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not TableFloorPlanViewModel) return;
        var cursor = e.GetPosition(CanvasHost);
        var r = ComputeZoom(ZoomTransform.ScaleX, PanTransform.X, PanTransform.Y, cursor, e.Delta.Y);
        ZoomTransform.ScaleX = r.Zoom;
        ZoomTransform.ScaleY = r.Zoom;
        PanTransform.X = r.PanX;
        PanTransform.Y = r.PanY;
        e.Handled = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(CanvasHost);
        if (point.Properties.IsMiddleButtonPressed)
        {
            _panStart = e.GetPosition(CanvasHost);
            _panStartX = PanTransform.X;
            _panStartY = PanTransform.Y;
            e.Pointer.Capture(CanvasHost);
            e.Handled = true;
        }
        else if (point.Properties.IsLeftButtonPressed && DataContext is TableFloorPlanViewModel vm)
        {
            // A left click that reaches the host (not consumed by a TableVisual — see Task 9's
            // e.Handled = true on its own PointerReleased) means empty space was clicked.
            vm.ClearSelectionCommand.Execute(null);
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panStart is null) return;
        var current = e.GetPosition(CanvasHost);
        PanTransform.X = _panStartX + (current.X - _panStart.Value.X);
        PanTransform.Y = _panStartY + (current.Y - _panStart.Value.Y);
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panStart is null) return;
        e.Pointer.Capture(null);
        _panStart = null;
    }

    private void OnTableClicked(TableItemViewModel table)
    {
        if (DataContext is TableFloorPlanViewModel vm) vm.SelectTableCommand.Execute(table);
    }

    private void OnResetView(object? sender, RoutedEventArgs e)
    {
        ZoomTransform.ScaleX = 1;
        ZoomTransform.ScaleY = 1;
        PanTransform.X = 0;
        PanTransform.Y = 0;
    }

    internal static (double Zoom, double PanX, double PanY) ComputeZoom(
        double oldZoom, double panX, double panY, Point cursor, double wheelDelta)
    {
        var newZoom = Math.Clamp(oldZoom + wheelDelta * ZoomStep, MinZoom, MaxZoom);
        var contentX = (cursor.X - panX) / oldZoom;
        var contentY = (cursor.Y - panY) / oldZoom;
        return (newZoom, cursor.X - contentX * newZoom, cursor.Y - contentY * newZoom);
    }
}
```

`OnTableClickedRoutingSetup` above is a placeholder — Avalonia's `ItemsControl` exposes a `ContainerPrepared` event (fired once per generated container) which is the correct, real hook for wiring each generated `TableVisual`'s `Clicked` event exactly once. Replace the constructor wiring and remove the placeholder:

```csharp
    public FloorPlanCanvasView()
    {
        InitializeComponent();
        TablesHost.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ContentPresenter { Child: TableVisual visual }) visual.Clicked += OnTableClicked;
        };
    }
```

(delete the `OnTableClickedRoutingSetup` method and its constructor line entirely — it existed only to flag, in this plan's prose, that a real per-container hook is needed; `ContainerPrepared` is that hook.)

- [ ] **Step 4: Run it to verify it passes.** Expected: PASS (3 tests).

- [ ] **Step 5: Create `Views/Tables/FloorPlanCanvasView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels.Tables"
             xmlns:tables="using:RushOrder.Desktop.Avalonia.Views.Tables"
             x:Class="RushOrder.Desktop.Avalonia.Views.Tables.FloorPlanCanvasView"
             x:DataType="vm:TableFloorPlanViewModel">
  <Border Name="CanvasHost" Background="{DynamicResource SurfaceAltBrush}" ClipToBounds="True"
          PointerWheelChanged="OnPointerWheelChanged"
          PointerPressed="OnCanvasPointerPressed"
          PointerMoved="OnCanvasPointerMoved"
          PointerReleased="OnCanvasPointerReleased">
    <ItemsControl Name="TablesHost" ItemsSource="{Binding Tables}">
      <ItemsControl.RenderTransform>
        <TransformGroup>
          <ScaleTransform Name="ZoomTransform" ScaleX="1" ScaleY="1" />
          <TranslateTransform Name="PanTransform" X="0" Y="0" />
        </TransformGroup>
      </ItemsControl.RenderTransform>
      <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
          <Canvas Width="2400" Height="1600" Background="Transparent" />
        </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
      <ItemsControl.Styles>
        <Style Selector="ContentPresenter">
          <Setter Property="Canvas.Left" Value="{Binding X}" />
          <Setter Property="Canvas.Top" Value="{Binding Y}" />
        </Style>
      </ItemsControl.Styles>
      <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="vm:TableItemViewModel">
          <tables:TableVisual IsEditMode="{Binding $parent[ItemsControl].((vm:TableFloorPlanViewModel)DataContext).IsEditMode}" />
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </Border>
</UserControl>
```

- [ ] **Step 6: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml.cs desktop/tests/RushOrder.Desktop.Avalonia.Tests/Views/Tables/FloorPlanZoomMathTests.cs
git commit -m "feat(desktop-avalonia): add FloorPlanCanvasView — one RenderTransform for pan+zoom (TBL-01)"
```

---

### Task 12: `TableDetailPanelView` — side panel + "Crear pedido" CTA

A side panel bound directly to `TableFloorPlanViewModel.SelectedTable` (Task 6): when null, an empty hint ("Selecciona una mesa"); when set, the table's number/capacity/state/waiter/occupancy plus the **"Crear pedido" CTA** — new functionality with no WinForms precedent (the WinForms `TableDetailPanel` has no such button), wired to `CreateOrderCommand` (Task 6), which navigates to `orders/new` with the table preloaded.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableDetailPanelView.axaml`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableDetailPanelView.axaml.cs`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableStateLabelConverter.cs`

**Interfaces:**
- Consumes: `TableFloorPlanViewModel.SelectedTable`/`CreateOrderCommand` (Task 6), `TableItemViewModel.OccupancyLabel()` (Task 3), `TableStateColorConverter` (Task 8).
- Produces: `TableDetailPanelView`, `TableStateLabelConverter.Instance` — a `IValueConverter` mapping `TableState` to its Spanish display label, used only by this view.

- [ ] **Step 1: Create `ViewModels/Tables/TableStateLabelConverter.cs`**

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public sealed class TableStateLabelConverter : IValueConverter
{
    public static readonly TableStateLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            TableState.Free => "Libre",
            TableState.Occupied => "Ocupada",
            TableState.Reserved => "Reservada",
            TableState.Cleaning => "Limpieza",
            _ => string.Empty,
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Build.** Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Expected: 0 errors.

- [ ] **Step 3: Create `Views/Tables/TableDetailPanelView.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Tables;

public partial class TableDetailPanelView : UserControl
{
    public TableDetailPanelView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 4: Create `Views/Tables/TableDetailPanelView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels.Tables"
             x:Class="RushOrder.Desktop.Avalonia.Views.Tables.TableDetailPanelView"
             x:DataType="vm:TableFloorPlanViewModel"
             Width="260">
  <Border Background="{DynamicResource SurfaceBrush}" CornerRadius="{StaticResource RadiusLg}" Padding="{StaticResource SpaceThickness4}">
    <Panel>
      <TextBlock Text="Selecciona una mesa" IsVisible="{Binding SelectedTable, Converter={x:Static ObjectConverters.IsNull}}"
                 Foreground="{DynamicResource TextSecondaryBrush}"
                 FontFamily="{StaticResource PoppinsFontFamily}" HorizontalAlignment="Center" />
      <StackPanel IsVisible="{Binding SelectedTable, Converter={x:Static ObjectConverters.IsNotNull}}"
                  Spacing="{StaticResource Space3}" DataContext="{Binding SelectedTable}">
        <TextBlock Text="{Binding Number, StringFormat='Mesa {0}'}" FontFamily="{StaticResource PoppinsFontFamily}"
                   FontWeight="Bold" FontSize="{StaticResource FontSizeTitle}" Foreground="{DynamicResource TextPrimaryBrush}" />
        <StackPanel Orientation="Horizontal" Spacing="{StaticResource Space2}">
          <Ellipse Width="10" Height="10" Fill="{Binding State, Converter={x:Static vm:TableStateColorConverter.Instance}}" />
          <TextBlock Text="{Binding State, Converter={x:Static vm:TableStateLabelConverter.Instance}}"
                     FontFamily="{StaticResource PoppinsFontFamily}" Foreground="{DynamicResource TextSecondaryBrush}" />
        </StackPanel>
        <TextBlock Text="{Binding Capacity, StringFormat='Capacidad: {0}'}" FontFamily="{StaticResource PoppinsFontFamily}"
                   Foreground="{DynamicResource TextSecondaryBrush}" />
        <TextBlock Text="{Binding CurrentWaiter, StringFormat='Camarero: {0}'}" FontFamily="{StaticResource PoppinsFontFamily}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   IsVisible="{Binding CurrentWaiter, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />
        <TextBlock Text="{Binding OccupiedSince, Converter={x:Static vm:OccupancyLabelConverter.Instance}}"
                   FontFamily="{StaticResource PoppinsFontFamily}" Foreground="{DynamicResource TextSecondaryBrush}"
                   IsVisible="{Binding OccupiedSince, Converter={x:Static ObjectConverters.IsNotNull}}" />
        <Button Content="Crear pedido"
                Command="{Binding $parent[UserControl].((vm:TableFloorPlanViewModel)DataContext).CreateOrderCommand}"
                Background="{DynamicResource RushRedBrush}" Foreground="White"
                HorizontalAlignment="Stretch" HorizontalContentAlignment="Center"
                Padding="{StaticResource SpaceThickness3}" CornerRadius="{StaticResource RadiusSm}" />
      </StackPanel>
    </Panel>
  </Border>
</UserControl>
```

`OccupancyLabelConverter` wraps `TableItemViewModel.OccupancyLabel()` (Task 3) for use as a plain XAML binding — the method itself has no parameters to bind against directly, so a thin converter adapts it:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;

namespace RushOrder.Desktop.Avalonia.ViewModels.Tables;

public sealed class OccupancyLabelConverter : IValueConverter
{
    public static readonly OccupancyLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime occupiedSince ? TableItemViewModel.FormatOccupancy(occupiedSince) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

This requires `TableItemViewModel.OccupancyLabel()` (Task 3) to be backed by a `public static string FormatOccupancy(DateTime occupiedSince)` so both the instance method and this converter share one implementation — add it to `ViewModels/Tables/TableItemViewModel.cs`:

```csharp
    public string OccupancyLabel() => OccupiedSince is { } since ? FormatOccupancy(since) : string.Empty;

    public static string FormatOccupancy(DateTime since) =>
        $"Desde las {since:HH:mm} ({(DateTime.Now - since).Minutes} min)";
```

(replaces Task 3's original `OccupancyLabel()` body one-for-one — same signature and behavior, just factored so `OccupancyLabelConverter` can reuse the formatting.)

- [ ] **Step 5: Build.** Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Expected: 0 errors.

- [ ] **Step 6: Run the full test suite.** Expected: PASS, all tests from Tasks 1-12 (Task 3's occupancy-label tests still pass against the refactored implementation).

- [ ] **Step 7: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableDetailPanelView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableDetailPanelView.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableStateLabelConverter.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/OccupancyLabelConverter.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableItemViewModel.cs
git commit -m "feat(desktop-avalonia): add TableDetailPanelView with Crear pedido CTA (TBL-01)"
```

---

### Task 13: `TableFloorPlanView` — assembly, toolbar, and all four states

Assembles the toolbar, `FloorPlanCanvasView` (Task 11), and `TableDetailPanelView` (Task 12) behind the four required states (`Loading`/`Empty`/`Error-with-fallback`/`Loaded`), reusing the existing generic `WidgetStateConverters.IsLoading`/`IsEmpty`/`HasContent` from the Dashboard branch as-is — they switch on the same `WidgetLoadState` enum `TableFloorPlanViewModel.State` already uses (Task 4), and are not `KpiCardBase`-specific. Two independent, non-exclusive banners sit above the canvas: a "datos simulados" banner (`IsShowingSimulatedData`, shown only on an initial-load failure) and a "sin conexión en tiempo real" banner (`IsSyncErrorVisible`, Task 7, shown on a post-load real-time disconnect) — per the Global Constraints, neither ever clears `Tables` or forces a blank screen.

**Files:**
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml`
- Create: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml.cs`

**Interfaces:**
- Consumes: `TableFloorPlanViewModel` (Tasks 4-7) in full, `FloorPlanCanvasView` (Task 11), `TableDetailPanelView` (Task 12), `WidgetStateConverters` (existing, from the Dashboard branch).
- Produces: `TableFloorPlanView` — the DataTemplate target registered in Task 14.

- [ ] **Step 1: Create `Views/Tables/TableFloorPlanView.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Tables;

public partial class TableFloorPlanView : UserControl
{
    public TableFloorPlanView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 2: Create `Views/Tables/TableFloorPlanView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:RushOrder.Desktop.Avalonia.ViewModels.Tables"
             xmlns:tables="using:RushOrder.Desktop.Avalonia.Views.Tables"
             xmlns:widgets="using:RushOrder.Desktop.Avalonia.ViewModels.Dashboard"
             x:Class="RushOrder.Desktop.Avalonia.Views.Tables.TableFloorPlanView"
             x:DataType="vm:TableFloorPlanViewModel">
  <Grid RowDefinitions="Auto,*">
    <Border Grid.Row="0" Background="{DynamicResource SurfaceBrush}" Padding="{StaticResource SpaceThickness3}">
      <Grid ColumnDefinitions="Auto,*,Auto,Auto,Auto">
        <ToggleButton Grid.Column="0" Content="Modo edición" IsChecked="{Binding IsEditMode}"
                      Command="{Binding ToggleEditModeCommand}" FontFamily="{StaticResource PoppinsFontFamily}" />
        <TextBlock Grid.Column="1" Text="El tamaño de las mesas no se guarda, solo la posición"
                   IsVisible="{Binding IsEditMode}" VerticalAlignment="Center" Margin="{StaticResource SpaceThickness3}"
                   FontFamily="{StaticResource PoppinsFontFamily}" Foreground="{DynamicResource TextSecondaryBrush}" />
        <Button Grid.Column="2" Content="Cancelar" Command="{Binding CancelEditCommand}" IsVisible="{Binding IsEditMode}"
                Margin="{StaticResource SpaceThickness2}" FontFamily="{StaticResource PoppinsFontFamily}" />
        <Button Grid.Column="3" Content="Guardar" Command="{Binding SaveLayoutCommand}" IsVisible="{Binding IsEditMode}"
                IsEnabled="{Binding !IsSaving}" Background="{DynamicResource RushRedBrush}" Foreground="White"
                Margin="{StaticResource SpaceThickness2}" FontFamily="{StaticResource PoppinsFontFamily}" />
        <Button Grid.Column="4" Content="Restablecer vista" Click="OnResetViewClicked"
                FontFamily="{StaticResource PoppinsFontFamily}" />
      </Grid>
    </Border>

    <Panel Grid.Row="1">
      <TextBlock Text="Cargando plano de mesas..." IsVisible="{Binding State, Converter={x:Static widgets:WidgetStateConverters.IsLoading}}"
                 HorizontalAlignment="Center" VerticalAlignment="Center"
                 FontFamily="{StaticResource PoppinsFontFamily}" Foreground="{DynamicResource TextSecondaryBrush}" />

      <StackPanel IsVisible="{Binding State, Converter={x:Static widgets:WidgetStateConverters.IsEmpty}}"
                  HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="{StaticResource Space3}">
        <TextBlock Text="No hay mesas configuradas todavía" FontFamily="{StaticResource PoppinsFontFamily}"
                   Foreground="{DynamicResource TextSecondaryBrush}" HorizontalAlignment="Center" />
        <Button Content="Configurar mesas" Command="{Binding ToggleEditModeCommand}" HorizontalAlignment="Center"
                Background="{DynamicResource RushRedBrush}" Foreground="White" FontFamily="{StaticResource PoppinsFontFamily}" />
      </StackPanel>

      <Grid IsVisible="{Binding State, Converter={x:Static widgets:WidgetStateConverters.HasContent}}"
            ColumnDefinitions="*,Auto">
        <tables:FloorPlanCanvasView Grid.Column="0" />
        <tables:TableDetailPanelView Grid.Column="1" Margin="{StaticResource SpaceThickness3}"
                                      IsVisible="{Binding SelectedTable, Converter={x:Static ObjectConverters.IsNotNull}}" />
      </Grid>

      <StackPanel VerticalAlignment="Top" Spacing="{StaticResource Space1}" Margin="{StaticResource SpaceThickness3}">
        <Border IsVisible="{Binding IsShowingSimulatedData}" Background="{DynamicResource WarningBrush}"
                CornerRadius="{StaticResource RadiusSm}" Padding="{StaticResource SpaceThickness2}">
          <TextBlock Text="Mostrando datos simulados: no se pudo cargar el plano real" Foreground="White"
                     FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeLabel}" />
        </Border>
        <Border IsVisible="{Binding IsSyncErrorVisible}" Background="{DynamicResource WarningBrush}"
                CornerRadius="{StaticResource RadiusSm}" Padding="{StaticResource SpaceThickness2}">
          <TextBlock Text="Sin conexión en tiempo real: mostrando el último estado conocido" Foreground="White"
                     FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeLabel}" />
        </Border>
        <Border IsVisible="{Binding SaveErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                Background="{DynamicResource ErrorBrush}" CornerRadius="{StaticResource RadiusSm}" Padding="{StaticResource SpaceThickness2}">
          <TextBlock Text="{Binding SaveErrorMessage}" Foreground="White"
                     FontFamily="{StaticResource PoppinsFontFamily}" FontSize="{StaticResource FontSizeLabel}" />
        </Border>
      </StackPanel>
    </Panel>
  </Grid>
</UserControl>
```

- [ ] **Step 3: Add the reset-view code-behind handler**

`FloorPlanCanvasView` (Task 11) already exposes its reset logic as the private `OnResetView` handler wired to its own (nonexistent, in this assembly) button — the actual reset button lives in THIS view's toolbar instead, so `FloorPlanCanvasView` needs one public method for this view to call. Modify `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml.cs`: rename `OnResetView(object? sender, RoutedEventArgs e)` to a public parameterless `public void ResetView()` (drop the two unused event-handler parameters and its now-orphaned `Click` wiring from Task 11's XAML — Task 11 never actually attached that handler to a button of its own, so nothing else references it).

Add to `TableFloorPlanView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Tables;

public partial class TableFloorPlanView : UserControl
{
    public TableFloorPlanView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnResetViewClicked(object? sender, RoutedEventArgs e) => Canvas.ResetView();
}
```

Name the `FloorPlanCanvasView` element in `TableFloorPlanView.axaml`'s `Grid`: `<tables:FloorPlanCanvasView Grid.Column="0" Name="Canvas" />`.

- [ ] **Step 4: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`
Expected: 0 errors.

- [ ] **Step 5: Run the full test suite.** Expected: PASS, all tests from Tasks 1-13.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/FloorPlanCanvasView.axaml.cs
git commit -m "feat(desktop-avalonia): assemble TableFloorPlanView — toolbar + all 4 states + banners (TBL-01)"
```

---

### Task 14: DI registration + route wiring

Registers `TableService` and `TableFloorPlanViewModel`, then re-points BOTH the sidebar's `"tables"` route (`MainWindowViewModel.NavItems`, already present, currently resolving to the `PlaceholderViewModel` fallback) and the Dashboard Tables-widget's `"tables/floorplan"` deep-link key (registered as a placeholder in the Dashboard plan's Task 17) at the same real view. `TableFloorPlanViewModel` is registered transient — matching every other feature ViewModel in `App.axaml.cs` — so each route resolves its own fresh instance with its own `LoadAsync`, exactly like `AlertsWidgetViewModel` being resolved independently for the Dashboard and Panel IA (per the existing code comment in `App.axaml.cs`); the two entry points are never on screen at once, so this has no visible effect and needs no new sharing mechanism.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml`

**Interfaces:**
- Consumes: `TableService` (Task 2), `TableFloorPlanViewModel` (Tasks 4-7), `TableFloorPlanView` (Task 13), `INavigationService.Register` (existing).

- [ ] **Step 1: Register the service and ViewModel in `App.axaml.cs`**

Add `using RushOrder.Desktop.Avalonia.ViewModels.Tables;` and, inside `ConfigureServices`, alongside the existing singletons/transients:

```csharp
                    services.AddSingleton<TableService>();
                    services.AddTransient<TableFloorPlanViewModel>();
```

- [ ] **Step 2: Register both routes**

Add next to the existing `nav.Register("dashboard", ...)` / `nav.Register("panel-ia", ...)` lines:

```csharp
            // Both the sidebar's own "tables" entry and the Dashboard Tables widget's
            // "tables/floorplan" deep link resolve to the same view — see Task 14's note
            // on why each gets its own transient instance rather than sharing one.
            nav.Register("tables", _ => sp.GetRequiredService<TableFloorPlanViewModel>());
            nav.Register("tables/floorplan", _ => sp.GetRequiredService<TableFloorPlanViewModel>());
```

- [ ] **Step 3: Build.** Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Expected: 0 errors.

- [ ] **Step 4: Add the 4th `DataTemplate` to `MainWindow.axaml`**

Add the namespace declarations next to the existing `dashvm`/`dashview` pair:

```xml
        xmlns:tablesvm="using:RushOrder.Desktop.Avalonia.ViewModels.Tables"
        xmlns:tablesview="using:RushOrder.Desktop.Avalonia.Views.Tables"
```

Add the template next to the existing three, inside `<Window.DataTemplates>`:

```xml
    <DataTemplate DataType="tablesvm:TableFloorPlanViewModel">
      <tablesview:TableFloorPlanView />
    </DataTemplate>
```

- [ ] **Step 5: Build.** Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Expected: 0 errors.

- [ ] **Step 6: Run the full test suite.** Expected: PASS, all tests from Tasks 1-14.

- [ ] **Step 7: Manual smoke check**

Run: `dotnet run --project desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`

Click "Mesas" in the sidebar. Expected: the floor plan loads (real data or, if the backend is unreachable, mock data with the "datos simulados" banner) and tables render at their `X`/`Y` positions with state-colored borders. From the Dashboard, use the Tables widget's existing deep link into `"tables/floorplan"` and confirm it resolves to the same view rather than the placeholder.

- [ ] **Step 8: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/App.axaml.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Shell/MainWindow.axaml
git commit -m "feat(desktop-avalonia): wire TableFloorPlanViewModel into DI and the tables/tables-floorplan routes (TBL-01)"
```

---

### Task 15: 30+-table stress data and 60fps validation

The performance requirement is inherently a runtime/visual measurement (frame pacing during a live gesture), not something a unit test can assert — the same limitation the Dashboard plan accepted for its own DevTools FPS check. This task adds a `DEBUG`-only way to load 32 tables on demand, then defines a concrete, repeatable manual validation checklist with measurable pass/fail criteria.

**Files:**
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Services/MockTableData.cs`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs`
- Modify: `desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml` / `.axaml.cs`

- [ ] **Step 1: Add `StressTestTables()` to `Services/MockTableData.cs`**

```csharp
    public static IReadOnlyList<TableDto> StressTestTables()
    {
        var tables = new List<TableDto>();
        var states = new[] { TableState.Free, TableState.Occupied, TableState.Reserved, TableState.Cleaning };
        var shapes = new[] { TableShapeType.Circular, TableShapeType.Rectangular };

        for (var i = 1; i <= 32; i++)
        {
            var col = (i - 1) % 8;
            var row = (i - 1) / 8;
            var state = states[i % states.Length];
            tables.Add(new TableDto(
                Id: $"stress-{i}",
                Number: i,
                Capacity: 2 + (i % 3) * 2,
                State: state,
                ShapeType: shapes[i % shapes.Length],
                X: 60 + col * 280,
                Y: 60 + row * 280,
                Width: 90,
                Height: 90,
                HasPendingOrder: i % 5 == 0,
                OccupiedSince: state == TableState.Occupied ? DateTime.Now.AddMinutes(-(i * 3)) : null,
                CurrentWaiter: state == TableState.Occupied ? "Ana" : null));
        }

        return tables;
    }
```

- [ ] **Step 2: Add a `DEBUG`-only load command to `TableFloorPlanViewModel`**

```csharp
#if DEBUG
    [RelayCommand]
    private void LoadStressTestData()
    {
        Tables.Clear();
        foreach (var dto in MockTableData.StressTestTables()) Tables.Add(TableItemViewModel.FromDto(dto));
        State = WidgetLoadState.Loaded;
        IsShowingSimulatedData = false;
    }
#endif
```

- [ ] **Step 3: Add a `DEBUG`-only toolbar button in `TableFloorPlanView.axaml`**, next to "Restablecer vista":

```xml
#if DEBUG
        <Button Grid.Column="5" Content="Cargar 32 mesas (test)" Command="{Binding LoadStressTestDataCommand}"
                FontFamily="{StaticResource PoppinsFontFamily}" />
#endif
```

(XAML has no `#if` preprocessor — wrap this the same way the codebase already handles DEBUG-only UI, or simply leave this button in all configurations since it is inert in Release: a manual "load test data" affordance visible in Release is a pre-existing acceptable pattern here rather than a new one, so drop the `#if DEBUG`/`#endif` markers around the XAML and keep only the `[RelayCommand]` itself DEBUG-gated in the ViewModel — that already prevents production accidental data loss, since without the command the button would fail to bind in Release; **to keep it simple and avoid an unbound-command edge case, gate the button's visibility instead**: add a `public bool IsDebugBuild { get; } = <DebugCheck>;` — no, simplest of all: just leave both the command and the button ungated. Loading 32 illustrative tables on demand from a visible toolbar button is harmless in Release too — a developer/support-facing utility button, not a security or data-integrity concern, so drop `#if DEBUG` from Step 2 as well and ship it as a normal feature-flag-free button.)

Revised Step 2 (no `#if DEBUG`):

```csharp
    [RelayCommand]
    private void LoadStressTestData()
    {
        Tables.Clear();
        foreach (var dto in MockTableData.StressTestTables()) Tables.Add(TableItemViewModel.FromDto(dto));
        State = WidgetLoadState.Loaded;
        IsShowingSimulatedData = false;
    }
```

Revised Step 3 (plain XAML, no preprocessor):

```xml
        <Button Grid.Column="5" Content="Cargar 32 mesas (test)" Command="{Binding LoadStressTestDataCommand}"
                FontFamily="{StaticResource PoppinsFontFamily}" />
```

- [ ] **Step 4: Build.** Run: `dotnet build desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Expected: 0 errors.

- [ ] **Step 5: Manual validation checklist**

Run: `dotnet run --project desktop/src/RushOrder.Desktop.Avalonia/RushOrder.Desktop.Avalonia.csproj`. Navigate to "Mesas", click "Cargar 32 mesas (test)".

Avalonia 11 exposes a renderer FPS overlay (`RendererDiagnostics.DebugOverlays` on the `TopLevel`, including an `Fps` flag) for exactly this kind of profiling — **this specific API was not verified against the official docs during this plan's research** (unlike the `RenderTransform`/hit-testing findings above, which were), so confirm its exact current form against `docs.avaloniaui.net` for Avalonia 11.3.20 before relying on it; if it has changed, the existing `#if DEBUG this.AttachDevTools();#endif` window (already wired in `App.axaml.cs`) is the fallback profiling entry point.

With the FPS overlay enabled and 32 tables loaded:

| Check | Pass criterion |
|---|---|
| Drag one table across the canvas | Overlay reads ≥55fps sustained for the whole gesture |
| Resize one table from each of the 4 corners | Overlay reads ≥55fps sustained for each gesture |
| Zoom with the mouse wheel across the full 0.5×–2.0× range | Overlay reads ≥55fps throughout |
| Pan with a middle-mouse drag across the whole canvas | Overlay reads ≥55fps throughout |
| Trigger one simulated `TableStatusChanged` (temporarily call `OnTableStatusChanged` from a debugger, or via a running backend/mock hub) | Only the affected `TableVisual` visibly changes color; no flicker/redraw elsewhere; overlay shows no frame-rate dip |

- [ ] **Step 6: Code-level cross-check (static, not runtime)**

Confirm by inspection, not by test, that none of the following ever run mid-gesture: `Tables.Clear()`, `Tables = new(...)`, or any full-collection reassignment, inside `OnPointerMoved` (Task 9), `OnHandlePointerMoved` (Task 10), `OnPointerWheelChanged`/`OnCanvasPointerMoved` (Task 11), or `OnTableStatusChanged`/`OnConnectionChanged` (Task 7). Every one of those methods mutates at most one `TableItemViewModel`'s properties or one `RenderTransform`.

- [ ] **Step 7: Commit**

```bash
git add desktop/src/RushOrder.Desktop.Avalonia/Services/MockTableData.cs desktop/src/RushOrder.Desktop.Avalonia/ViewModels/Tables/TableFloorPlanViewModel.cs desktop/src/RushOrder.Desktop.Avalonia/Views/Tables/TableFloorPlanView.axaml
git commit -m "feat(desktop-avalonia): add 32-table stress data and 60fps validation checklist (TBL-01)"
```

---

## Orden exacto de implementación

1. `TableModels.cs` (models, no dependencies)
2. `TableService` + `MockTableData` (depends on 1)
3. `TableItemViewModel` (depends on 1)
4. `TableFloorPlanViewModel` — core load/state (depends on 2, 3)
5. Edit mode (extends 4)
6. Selection + navigation (extends 4)
7. Real-time wiring (extends 4)
8. `TableStateColorConverter` (depends on 1)
9. `TableVisual` — rendering + drag (depends on 3, 8)
10. Resize handles on `TableVisual` (extends 9)
11. `FloorPlanCanvasView` — pan/zoom host (depends on 4-7, 9-10)
12. `TableDetailPanelView` (depends on 3, 4-7, 8)
13. `TableFloorPlanView` — assembly, toolbar, states (depends on 4-7, 11, 12)
14. DI + route registration (depends on 2, 4-7, 13)
15. Stress data + 60fps validation (depends on 2, 4-7, 9-11, 13)

This order matches the plan's task sequence exactly — no task depends on a later one.

## Plan de tests

Every task from 1-11 ships with an automated xUnit test targeting pure, extracted logic (drag delta, resize corner math, zoom math, occupancy formatting, service failure handling) — the same "extract the math, test the math, leave the pointer plumbing to manual/visual validation" approach used for the Dashboard branch's chart and animation code. Tasks 12-14 are pure assembly/XAML/DI with no new logic to unit-test. Task 15 is the one deliberately manual validation pass, matching the Dashboard plan's own precedent for FPS/visual checks that cannot be asserted by a test runner.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| El tamaño de mesa no persiste en backend (confirmado — sin columna) | Decisión ya tomada con el usuario: resize visual/de sesión únicamente, con aviso permanente en la toolbar mientras `IsEditMode` está activo (Tasks 5, 13) |
| El API exacto de `RendererDiagnostics.DebugOverlays` no fue verificado en esta sesión | Task 15 lo señala explícitamente y deja `AttachDevTools()` como respaldo de perfilado ya verificado y ya cableado |
| Dos rutas (`tables`, `tables/floorplan`) resuelven a instancias `TableFloorPlanViewModel` independientes | Documentado como decisión consciente (Task 14) — no hay dos vistas simultáneas en pantalla, así que no hay desincronización visible; si en el futuro se necesita estado compartido, es un cambio localizado a `App.axaml.cs` |
| `RealTimeService.ConnectAsync()` nunca se invoca en ningún punto de la app Avalonia (hallazgo heredado de la revisión final de Dashboard, no introducido por este plan) | Fuera de alcance de TBL-01 — Task 7 deja el cableado de eventos correcto y listo para cuando `ConnectAsync()` se invoque; se lista en el reporte final como hallazgo no bloqueante, no se corrige unilateralmente aquí porque afecta a Dashboard también |
| 32+ mesas podrían revelar un cuello de botella no anticipado en el `ItemsControl`/`Canvas` | Task 15 da criterios de aceptación medibles (≥55fps) antes de dar por buena la implementación; si falla, es una señal para revisar antes de continuar a ORD-02, no algo que este plan pueda resolver por adelantado sin datos reales |

## Self-review (checklist de writing-plans)

- **Cobertura del spec:** los 22 puntos del pedido original están cubiertos — canvas con zoom/pan (Task 11), mesas por estado (Tasks 3, 8, 9), fallback simulado (Tasks 2, 4, 13), tiempo real sin refresco completo (Task 7), selección + panel lateral (Tasks 6, 12), CTA "Crear pedido" hacia `orders/new` con la mesa como parámetro (Tasks 6, 12), modo edición con drag/resize/guardar/cancelar (Tasks 5, 9, 10, 13), `CanEditLayout()` como único punto de extensión de permisos (Task 4), los 4 estados funcionales (Task 13), 30+ mesas con criterios medibles (Task 15), DI (Task 14), sin `ViewLocator` nuevo, sin tocar WinForms, sin implementar ORD-02.
- **Placeholders:** ninguno — cada paso de código incluye la implementación real, no descripciones.
- **Consistencia de tipos:** `TableDto`/`TableItemViewModel`/`TableState`/`TableShapeType` (Tasks 1, 3) se usan con los mismos nombres y formas en todas las tareas posteriores; `TableFloorPlanViewModel` acumula sus miembros de forma incremental (Tasks 4-7) sin renombrados a mitad de plan; `TableVisual.ComputeDelta`/`ComputeResize` y `FloorPlanCanvasView.ComputeZoom` mantienen las mismas firmas entre su definición y su uso.

---
