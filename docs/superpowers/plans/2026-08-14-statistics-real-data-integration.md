# Statistics Real-Data Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the desktop Statistics screen (`StatisticsView`) show exclusively real data from `/api/v1/analytics/*`, removing the silent mock-data fallback in `StatisticsDataService`, and add proper loading/error/empty states.

**Architecture:** `StatisticsDataService` already calls the three real endpoints (`/sales`, `/products/performance`, `/waiters/performance`) and maps their DTOs — but on *any* exception it silently returns fully-fabricated mock data (`BuildMock`), which violates "real data only". The fix: extract the pure JSON→`StatisticsDto` mapping into a new testable `StatisticsMapper` class, delete `BuildMock` and the swallow-and-fallback `catch`, and let failures propagate as exceptions. `StatisticsView.RefreshAsync` gets a `catch` that shows a new `_lblError` label (styled like `LoginForm`'s `_lblError`, using `ThemeManager.Colors.Error`) instead of crashing or silently showing mock numbers. Legitimate empty periods (200 OK, zero rows) are distinguished from failures and get a friendly "no data" summary line instead of an error.

**Tech Stack:** .NET 8 (net8.0-windows, WinForms), Newtonsoft.Json, xunit + FluentAssertions (new `RushOrder.Desktop.Tests` project, matching the backend test stack already pinned in `Directory.Packages.props`).

## Global Constraints

- Keep `StatisticsDataService.GetStatisticsAsync(DateOnly from, DateOnly to, CancellationToken ct = default) : Task<StatisticsDto>` signature unchanged — `StatisticsView` must not need unrelated changes to call it.
- Do not touch backend code — `/api/v1/analytics/sales`, `/products/performance`, `/waiters/performance` already return DTOs that match the desktop's `Backend*Dto` records exactly (verified against `RushOrder.Application/Analytics/DTOs/*.cs`).
- `PaymentMethodPoint` stays an empty list from the real path — there genuinely is no backend endpoint for payment-method breakdown (documented in the existing code comment). This is not a mock to remove; it's an honest reflection of missing backend capability. Do not fabricate payment data.
- Only remove the mocks being replaced in `StatisticsDataService` (`BuildMock` and its literal data). Do not touch mock fallbacks in other services (`DashboardDataService`, `ForecastDataService`) — out of scope.
- No `any`/untyped casts (this is C#, so: no raw `dynamic`, no unchecked `object` casts) — keep the existing strongly-typed `Backend*Dto` records.
- After implementation, relaunch `RushOrder.Desktop.exe` so the change is visible live (per project convention).

---

### Task 1: Extract pure mapping logic into `StatisticsMapper`

**Files:**
- Create: `desktop/src/RushOrder.Desktop/Services/StatisticsMapper.cs`
- Modify: `desktop/src/RushOrder.Desktop/Services/StatisticsDataService.cs`

**Interfaces:**
- Produces: `internal static class StatisticsMapper { internal static StatisticsDto Map(DateOnly from, DateOnly to, BackendSalesDto sales, IReadOnlyList<BackendProductPerformanceDto> products, IReadOnlyList<BackendWaiterPerformanceDto> waiters); }`
- Consumes (moved as-is from current `StatisticsDataService`): `BackendSalesDto`, `BackendSalesSeriesPoint`, `BackendSalesTotals`, `BackendProductPerformanceDto`, `BackendWaiterPerformanceDto` records; `HourlyRevenuePoint`, `TopProductPoint`, `WaiterStatsRow`, `StatisticsDto` from `RushOrder.Desktop.Models`.

This task only moves code (no behavior change yet) so it can be committed and tested in isolation before Task 2 rewires the HTTP path.

- [ ] **Step 1: Create `StatisticsMapper.cs` with the extracted mapping**

```csharp
using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Services;

internal static class StatisticsMapper
{
    internal static StatisticsDto Map(
        DateOnly from, DateOnly to,
        BackendSalesDto sales,
        IReadOnlyList<BackendProductPerformanceDto> products,
        IReadOnlyList<BackendWaiterPerformanceDto> waiters)
    {
        var hourly = sales.Series
            .GroupBy(s => s.Date.Hour)
            .Select(g => new HourlyRevenuePoint(g.Key, g.Sum(s => s.Revenue)))
            .OrderBy(h => h.Hour)
            .ToList();

        var top = products
            .OrderByDescending(p => p.Revenue)
            .Take(10)
            .Select(p => new TopProductPoint(p.Name, p.QuantitySold, p.Revenue))
            .ToList();

        // Backend's WaiterPerformanceDto has no avg-service-time field, so
        // WaiterStatsRow.AvgMinutes is always 0 from this path.
        var waiterRows = waiters
            .Select(w => new WaiterStatsRow(w.Name, w.OrdersServed, w.Revenue, 0, w.AvgTicket))
            .ToList();

        return new StatisticsDto(
            from, to, hourly, top,
            PaymentMethods: [], // no backend endpoint for payment-method breakdown yet
            waiterRows,
            TotalRevenue: sales.Totals.Revenue,
            TotalOrders: sales.Totals.Orders);
    }
}
```

- [ ] **Step 2: Move the `Backend*Dto` records into `StatisticsMapper.cs` and delete them (plus `BuildMock`) from `StatisticsDataService.cs`**

Cut these four records from the bottom of `StatisticsDataService.cs` and paste them at the bottom of `StatisticsMapper.cs` (same namespace, so no using changes needed elsewhere):

```csharp
// Matches backend's SalesDto (Analytics/DTOs/SalesDto.cs).
internal sealed record BackendSalesDto(IReadOnlyList<BackendSalesSeriesPoint> Series, BackendSalesTotals Totals);
internal sealed record BackendSalesSeriesPoint(DateTimeOffset Date, decimal Revenue, int Orders, int Covers);
internal sealed record BackendSalesTotals(decimal Revenue, int Orders, decimal AvgTicket, DateTimeOffset? BestDay, DateTimeOffset? WorstDay);

// Matches backend's ProductPerformanceDto (Analytics/DTOs/ProductPerformanceDto.cs).
internal sealed record BackendProductPerformanceDto(
    Guid ProductId, string Name, string Category, int QuantitySold, decimal Revenue,
    decimal? AvgRating, string Trend, decimal? MarginEstimate);

// Matches backend's WaiterPerformanceDto (Analytics/DTOs/WaiterPerformanceDto.cs) —
// no avg-service-time field, so WaiterStatsRow.AvgMinutes is always 0 from this path.
internal sealed record BackendWaiterPerformanceDto(
    Guid WaiterId, string Name, int OrdersServed, decimal? AvgRating, decimal Revenue, decimal AvgTicket);
```

Leave `StatisticsDataService.cs`'s HTTP-fetching code as-is for this step (still calling the old inline mapping) — Task 2 rewires it to call `StatisticsMapper.Map`.

- [ ] **Step 3: Build to confirm the move compiles**

Run: `dotnet build desktop/src/RushOrder.Desktop/RushOrder.Desktop.csproj`
Expected: build succeeds (the inline mapping code in `StatisticsDataService.GetStatisticsAsync` still references the now-moved records, which resolve fine since both files share the `RushOrder.Desktop.Services` namespace).

- [ ] **Step 4: Commit**

```bash
git add desktop/src/RushOrder.Desktop/Services/StatisticsMapper.cs desktop/src/RushOrder.Desktop/Services/StatisticsDataService.cs
git commit -m "refactor(desktop): extract StatisticsMapper from StatisticsDataService"
```

---

### Task 2: Remove the mock fallback; propagate real errors

**Files:**
- Modify: `desktop/src/RushOrder.Desktop/Services/StatisticsDataService.cs`

**Interfaces:**
- Consumes: `StatisticsMapper.Map(...)` from Task 1.
- Produces: `StatisticsDataService.GetStatisticsAsync(DateOnly, DateOnly, CancellationToken) : Task<StatisticsDto>` — same signature, but now throws on failure instead of returning mock data.

- [ ] **Step 1: Rewrite `GetStatisticsAsync` to call the mapper and stop swallowing exceptions**

Replace the full method body (and delete `BuildMock` entirely) with:

```csharp
public async Task<StatisticsDto> GetStatisticsAsync(
    DateOnly from, DateOnly to, CancellationToken ct = default)
{
    try
    {
        ApplyAuth();

        var rid = _state.CurrentRestaurant?.Id;
        var f   = Uri.EscapeDataString(from.ToDateTime(TimeOnly.MinValue).ToString("O"));
        var t2  = Uri.EscapeDataString(to.ToDateTime(TimeOnly.MaxValue).ToString("O"));
        var baseUrl = "http://localhost:5143/api/v1/analytics";

        var salesTask   = _http.GetStringAsync($"{baseUrl}/sales?restaurantId={rid}&from={f}&to={t2}&groupBy=hour", ct);
        var productTask = _http.GetStringAsync($"{baseUrl}/products/performance?restaurantId={rid}&from={f}&to={t2}", ct);
        var waiterTask  = _http.GetStringAsync($"{baseUrl}/waiters/performance?restaurantId={rid}&from={f}&to={t2}", ct);
        await Task.WhenAll(salesTask, productTask, waiterTask);

        var sales = JsonConvert.DeserializeObject<ApiEnvelope<BackendSalesDto>>(salesTask.Result)?.Data
            ?? throw new InvalidOperationException("La API de analíticas devolvió una respuesta de ventas vacía o inválida.");
        var products = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendProductPerformanceDto>>>(productTask.Result)?.Data ?? [];
        var waiters  = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendWaiterPerformanceDto>>>(waiterTask.Result)?.Data ?? [];

        return StatisticsMapper.Map(from, to, sales, products, waiters);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "No se pudieron obtener las estadísticas de la API para {From}-{To}", from, to);
        throw;
    }
}

private void ApplyAuth()
{
    _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
        ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
        : null;
}
```

Delete the `BuildMock` method and its mock literal data (the `hourly`/`products`/`payments`/`waiters` mock lists) — this is the mock this task replaces.

- [ ] **Step 2: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop/RushOrder.Desktop.csproj`
Expected: build succeeds. `StatisticsDataService.cs` should now contain no mock data and no `Backend*Dto` record definitions (moved to `StatisticsMapper.cs` in Task 1).

- [ ] **Step 3: Commit**

```bash
git add desktop/src/RushOrder.Desktop/Services/StatisticsDataService.cs
git commit -m "fix(desktop): stop silently falling back to mock statistics data"
```

---

### Task 3: Add loading/error/empty states to `StatisticsView`

**Files:**
- Modify: `desktop/src/RushOrder.Desktop/Views/Statistics/StatisticsView.cs`

**Interfaces:**
- Consumes: `StatisticsDataService.GetStatisticsAsync` (now throws on failure, per Task 2); `ThemeManager.Colors.Error` (existing, `Theme/ThemeManager.cs:100`).

- [ ] **Step 1: Add an `_lblError` field and control, styled like `LoginForm`'s error label**

In the field declarations block, add:

```csharp
private Label          _lblError   = null!;
```

In `Build()`, right after the `_lblLoading` label is constructed (before `summaryBar.Controls.AddRange(...)`), add:

```csharp
_lblError = new Label
{
    Text      = "",
    Font      = _theme.Fonts.Regular,
    ForeColor = _theme.Colors.Error,
    AutoSize  = true,
    Location  = new Point(16, 8),
    Visible   = false,
};
```

Update the `AddRange` call to include it:

```csharp
summaryBar.Controls.AddRange([_lblSummary, _lblLoading, _lblError]);
```

- [ ] **Step 2: Wire loading/error state transitions**

Replace `SetLoading` with:

```csharp
private void SetLoading(bool loading)
{
    if (InvokeRequired) { Invoke(() => SetLoading(loading)); return; }
    _lblLoading.Visible = loading;
    if (loading) _lblError.Visible = false;
    _lblSummary.Visible = !loading && !_lblError.Visible;
}

private void ShowError(string message)
{
    if (InvokeRequired) { Invoke(() => ShowError(message)); return; }
    _lblError.Text    = message;
    _lblError.Visible = true;
    _lblSummary.Visible = false;
}
```

- [ ] **Step 3: Catch failures in `RefreshAsync` and clear stale chart data on error**

Replace `RefreshAsync` with:

```csharp
private async Task RefreshAsync()
{
    SetLoading(true);
    try
    {
        var from = DateOnly.FromDateTime(_dtFrom.Value.Date);
        var to   = DateOnly.FromDateTime(_dtTo.Value.Date);
        _currentData = await _data.GetStatisticsAsync(from, to);
        UpdateCharts(_currentData);
    }
    catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
    {
        ShowError("Tiempo de espera agotado al consultar las estadísticas. Inténtalo de nuevo.");
    }
    catch (Exception)
    {
        ShowError("No se pudieron cargar las estadísticas. Verifica tu conexión e inténtalo de nuevo.");
    }
    finally
    {
        SetLoading(false);
    }
}
```

(`_currentData` intentionally keeps its last successfully-loaded value on error, so a transient refresh failure doesn't wipe previously-shown data or break the Excel/PDF export buttons, which already guard on `_currentData is null`.)

- [ ] **Step 4: Friendly empty-period summary text**

In `UpdateCharts`, replace the summary-text assignment:

```csharp
_lblSummary.Text =
    $"Período: {dto.From:dd/MM/yyyy} — {dto.To:dd/MM/yyyy}  ·  " +
    $"Total: €{dto.TotalRevenue:N2}  ·  Pedidos: {dto.TotalOrders:N0}";
```

with:

```csharp
_lblSummary.Text = dto.TotalOrders == 0
    ? $"Período: {dto.From:dd/MM/yyyy} — {dto.To:dd/MM/yyyy}  ·  Sin datos de ventas para este período."
    : $"Período: {dto.From:dd/MM/yyyy} — {dto.To:dd/MM/yyyy}  ·  " +
      $"Total: €{dto.TotalRevenue:N2}  ·  Pedidos: {dto.TotalOrders:N0}";
```

- [ ] **Step 5: Build**

Run: `dotnet build desktop/src/RushOrder.Desktop/RushOrder.Desktop.csproj`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/RushOrder.Desktop/Views/Statistics/StatisticsView.cs
git commit -m "feat(desktop): add loading/error/empty states to Statistics view"
```

---

### Task 4: Unit tests for `StatisticsMapper`

**Files:**
- Create: `desktop/tests/RushOrder.Desktop.Tests/RushOrder.Desktop.Tests.csproj`
- Create: `desktop/tests/RushOrder.Desktop.Tests/StatisticsMapperTests.cs`
- Create: `desktop/src/RushOrder.Desktop/AssemblyInfo.cs`
- Modify: `rush-order.sln`

**Interfaces:**
- Consumes: `StatisticsMapper.Map(...)`, `BackendSalesDto`, `BackendSalesSeriesPoint`, `BackendSalesTotals`, `BackendProductPerformanceDto`, `BackendWaiterPerformanceDto` (all `internal`, from Task 1) via `InternalsVisibleTo`.

This is the only piece of `StatisticsDataService`'s logic that's practically unit-testable without a fake `HttpMessageHandler` (the class news up its own `HttpClient`, matching every other desktop data service — not changing that wiring is out of scope). `StatisticsMapper` carries all the actual transformation logic (grouping, sorting, truncation, field mapping), so it's the right test surface.

- [ ] **Step 1: Allow the test project to see `internal` types**

Create `desktop/src/RushOrder.Desktop/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RushOrder.Desktop.Tests")]
```

- [ ] **Step 2: Create the test project**

Create `desktop/tests/RushOrder.Desktop.Tests/RushOrder.Desktop.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\RushOrder.Desktop\RushOrder.Desktop.csproj" />
  </ItemGroup>

</Project>
```

(`net8.0-windows` matches the referenced project's TFM — a test project referencing a `net8.0-windows` project must target a compatible TFM. No `UseWindowsForms` needed since the tests only touch plain records/static methods, not WinForms controls.)

- [ ] **Step 3: Write the failing tests**

Create `desktop/tests/RushOrder.Desktop.Tests/StatisticsMapperTests.cs`:

```csharp
using FluentAssertions;
using RushOrder.Desktop.Services;

namespace RushOrder.Desktop.Tests;

public class StatisticsMapperTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To   = new(2026, 8, 7);

    [Fact]
    public void Map_GroupsSalesSeriesByHour_SummingRevenueAndSortingAscending()
    {
        var sales = new BackendSalesDto(
            Series:
            [
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 1, 14, 0, 0, TimeSpan.Zero), 30m, 2, 4),
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.Zero), 10m, 1, 1),
                new BackendSalesSeriesPoint(new DateTimeOffset(2026, 8, 3, 14, 0, 0, TimeSpan.Zero), 20m, 1, 2),
            ],
            Totals: new BackendSalesTotals(60m, 4, 15m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.HourlyRevenue.Should().Equal(
            new RushOrder.Desktop.Models.HourlyRevenuePoint(9, 10m),
            new RushOrder.Desktop.Models.HourlyRevenuePoint(14, 50m));
        result.TotalRevenue.Should().Be(60m);
        result.TotalOrders.Should().Be(4);
    }

    [Fact]
    public void Map_TakesTop10ProductsByRevenueDescending()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));
        var products = Enumerable.Range(1, 12)
            .Select(i => new BackendProductPerformanceDto(
                Guid.NewGuid(), $"Producto {i}", "Cat", i, i * 10m, null, "flat", null))
            .ToList();

        var result = StatisticsMapper.Map(From, To, sales, products, []);

        result.TopProducts.Should().HaveCount(10);
        result.TopProducts.Select(p => p.Name).First().Should().Be("Producto 12");
        result.TopProducts.Select(p => p.Name).Last().Should().Be("Producto 3");
    }

    [Fact]
    public void Map_MapsWaiters_WithAvgMinutesAlwaysZero()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));
        var waiters = new List<BackendWaiterPerformanceDto>
        {
            new(Guid.NewGuid(), "Ana García", 12, 4.5m, 320.50m, 26.70m),
        };

        var result = StatisticsMapper.Map(From, To, sales, [], waiters);

        result.WaiterStats.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new RushOrder.Desktop.Models.WaiterStatsRow("Ana García", 12, 320.50m, 0, 26.70m));
    }

    [Fact]
    public void Map_AlwaysReturnsEmptyPaymentMethods_NoBackendEndpointExistsYet()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.PaymentMethods.Should().BeEmpty();
    }

    [Fact]
    public void Map_WithEmptySeriesProductsAndWaiters_ReturnsEmptyListsNotAnException()
    {
        var sales = new BackendSalesDto([], new BackendSalesTotals(0m, 0, 0m, null, null));

        var result = StatisticsMapper.Map(From, To, sales, [], []);

        result.HourlyRevenue.Should().BeEmpty();
        result.TopProducts.Should().BeEmpty();
        result.WaiterStats.Should().BeEmpty();
        result.TotalOrders.Should().Be(0);
        result.From.Should().Be(From);
        result.To.Should().Be(To);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail (project/type not found)**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Tests/RushOrder.Desktop.Tests.csproj`
Expected: FAIL to build — `StatisticsMapper` doesn't exist yet if Task 1 hasn't run, or (if Task 1 already ran) the tests should actually compile and pass immediately since `StatisticsMapper.Map` already exists from Task 1. In that case this step confirms PASS instead — that's fine, Task 1 already did the TDD red step implicitly by not existing before this plan started. Note the discrepancy and proceed to Step 5 regardless.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test desktop/tests/RushOrder.Desktop.Tests/RushOrder.Desktop.Tests.csproj`
Expected: PASS (5 tests).

- [ ] **Step 6: Register the test project in `rush-order.sln`**

Add a `desktop\tests` solution folder (mirroring `backend\tests`) and the new project, nested under it. Insert after the `RushOrder.Desktop` project block (before `Global`):

```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{A1B2C3D4-1111-4444-9999-000000000001}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "RushOrder.Desktop.Tests", "desktop\tests\RushOrder.Desktop.Tests\RushOrder.Desktop.Tests.csproj", "{A1B2C3D4-1111-4444-9999-000000000002}"
EndProject
```

Add to `GlobalSection(ProjectConfigurationPlatforms)`:

```
		{A1B2C3D4-1111-4444-9999-000000000002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A1B2C3D4-1111-4444-9999-000000000002}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A1B2C3D4-1111-4444-9999-000000000002}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A1B2C3D4-1111-4444-9999-000000000002}.Release|Any CPU.Build.0 = Release|Any CPU
```

Add to `GlobalSection(NestedProjects)`:

```
		{A1B2C3D4-1111-4444-9999-000000000001} = {8A8225F7-0B8A-4CA7-A36E-E5474498F448}
		{A1B2C3D4-1111-4444-9999-000000000002} = {A1B2C3D4-1111-4444-9999-000000000001}
```

(`{8A8225F7-0B8A-4CA7-A36E-E5474498F448}` is the existing `desktop` solution folder GUID from `rush-order.sln:26`.) Use freshly generated GUIDs in place of the placeholders above (e.g. via `[guid]::NewGuid()` in PowerShell) rather than reusing these literal example values.

- [ ] **Step 7: Build the full solution to confirm nothing else broke**

Run: `dotnet build rush-order.sln`
Expected: build succeeds for every project, including the pre-existing backend projects and `RushOrder.Desktop`.

- [ ] **Step 8: Commit**

```bash
git add desktop/tests/RushOrder.Desktop.Tests desktop/src/RushOrder.Desktop/AssemblyInfo.cs rush-order.sln
git commit -m "test(desktop): add StatisticsMapper unit tests"
```

---

### Task 5: Manual verification against the running app

**Files:** none (verification only)

- [ ] **Step 1: Start the backend API**

Run (background): `dotnet run --project backend/src/RushOrder.API/RushOrder.API.csproj`

- [ ] **Step 2: Build and relaunch the desktop app**

```bash
dotnet build desktop/src/RushOrder.Desktop/RushOrder.Desktop.csproj
```

Kill any running `RushOrder.Desktop.exe` process and start it again (per project convention: always relaunch after desktop changes so the change is visible live), then log in and open the Estadísticas screen.

- [ ] **Step 3: Verify the three states manually**

- With the backend running and seeded/real order data for the selected date range: confirm the charts, summary line, and waiters grid show real numbers (not the old mock values — `Menú del día`, `Ana García 198 pedidos`, etc. from the deleted `BuildMock` should never appear again).
- Change the date range to a period with zero orders: confirm the summary line reads "Sin datos de ventas para este período." and the charts render empty without throwing.
- Stop the backend API and click "↺ Actualizar": confirm the red error label appears with a connection-failure message and the app does not crash.

- [ ] **Step 4: Report results to the user**

No commit for this task — it's manual confirmation only.
