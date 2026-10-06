# RushOrder.Desktop Folder Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize `desktop/src/RushOrder.Desktop/` into a module-grouped folder structure (Views by module, shared `Controls/`, `Services/`, `Styles/`, `Animations/`) without changing any business logic, using `git mv` to preserve history.

**Architecture:** This is a WinForms (net8.0-windows, code-only, no XAML/MVVM) project. The originally requested target structure assumed Avalonia/WPF (`App.axaml`, `ViewModels/`) — the user confirmed adapting the same grouping *philosophy* to WinForms instead: no `App.axaml`, no `ViewModels/`. Non-view utility folders not covered by the target list (`Models/`, `Data/`, `Helpers/`, `State/`, `Navigation/`) stay at the project root, untouched, per explicit user decision.

**Tech Stack:** .NET 8, C# (WinForms), SDK-style csproj with implicit globbing (no explicit `<Compile Include>` list to update).

## Global Constraints

- No business-logic or content changes — this is a pure move + namespace/using update.
- Every file move uses `git mv`, never delete+recreate.
- Namespaces follow folder structure exactly today (verified: `RushOrder.Desktop.<FolderPath>`) — this convention must be preserved after the move.
- Each task must leave the solution building with 0 new errors/warnings before moving to the next task.
- Final validation: full solution build clean, then run the app and confirm the Shell (login/main window) and at least one moved module render correctly (catches broken relative resource paths).
- If anything doesn't fit the plan cleanly during execution, stop and ask — don't improvise a new mapping.

## Confirmed namespace remapping (folder move ⇒ exact string replace)

Each row's **old string** does not appear anywhere else in the codebase except in the files being moved and their referencers (verified via grep) — a project-wide literal replace of old→new is safe and self-contained, and simultaneously fixes the moved files' own `namespace` declarations and every external `using`/fully-qualified reference. Apply the "Widgets"/"Controls" (more specific) rows **before** their parent row in each group so the shorter string's replace doesn't clobber the longer one.

| Group | Old namespace | New namespace | Files moving |
|---|---|---|---|
| Controls (a) | `RushOrder.Desktop.Forms.Controls` | `RushOrder.Desktop.Controls` | `Forms/Controls/NavButton.cs`, `PillButton.cs`, `PillComboBox.cs`, `ToggleSwitch.cs` |
| Controls (b) | `RushOrder.Desktop.Notifications` | `RushOrder.Desktop.Controls` | `Notifications/ToastForm.cs`, `ToastNotificationManager.cs` |
| Shell | `RushOrder.Desktop.Forms` | `RushOrder.Desktop.Views.Shell` | `Forms/MainForm.cs`, `Forms/LoginForm.cs` |
| Styles | `RushOrder.Desktop.Theme` | `RushOrder.Desktop.Styles` | `Theme/ThemeManager.cs`, `PoppinsFont.cs`, `GdiExtensions.cs` |
| Tables | `RushOrder.Desktop.Views.FloorPlan` | `RushOrder.Desktop.Views.Tables` | `Views/FloorPlan/FloorPlanView.cs`, `TableDetailPanel.cs`, `TableFloorPlanControl.cs`, `TableShape.cs` |
| Dashboard/AiDashboard (a, widgets) | `RushOrder.Desktop.Views.AiDashboard.Widgets` | `RushOrder.Desktop.Views.Dashboard.AiDashboard.Widgets` | `Views/AiDashboard/Widgets/KitchenEtaWidget.cs`, `SuggestionOfTheDayWidget.cs`, `TodayForecastWidget.cs` |
| Dashboard/AiDashboard (b) | `RushOrder.Desktop.Views.AiDashboard` | `RushOrder.Desktop.Views.Dashboard.AiDashboard` | `Views/AiDashboard/AiDashboardView.cs` |
| Statistics/Forecast | `RushOrder.Desktop.Views.Forecast` | `RushOrder.Desktop.Views.Statistics.Forecast` | `Views/Forecast/DemandForecastControl.cs` |
| Orders/Print | `RushOrder.Desktop.Views.Print` | `RushOrder.Desktop.Views.Orders.Print` | `Views/Print/PrinterConfigDialog.cs` |

**Unchanged (stay in place, no move, no namespace change):** `Views/Dashboard/**` (top-level + `Widgets/`), `Views/Orders/*.cs` + `Dialogs/`, `Views/Kitchen/**`, `Views/Menu/**`, `Views/Statistics/StatisticsView.cs`, `Views/Sync/**`, `Services/**`, `Models/**`, `Helpers/**`, `Data/**`, `State/**`, `Navigation/**`, `Program.cs`, `AssemblyInfo.cs`, `Assets/**`.

**`Animations/`**: no existing files map to this folder (no animation/behavior files found in the current inventory). Create it empty with a `.gitkeep` placeholder to match the requested skeleton; note in the final summary that it's unpopulated.

---

### Task 1: Controls/ (Forms/Controls + Notifications)

**Files:**
- Move: `Forms/Controls/{NavButton,PillButton,PillComboBox,ToggleSwitch}.cs` → `Controls/`
- Move: `Notifications/{ToastForm,ToastNotificationManager}.cs` → `Controls/`
- Delete (now empty): `Forms/Controls/`, `Notifications/`

- [ ] **Step 1:** `git mv` each of the 6 files into `Controls/` (create the folder implicitly via the first `git mv`).
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Forms.Controls` → `RushOrder.Desktop.Controls` across `desktop/src/RushOrder.Desktop/**/*.cs` (excluding `obj/`, `bin/`).
- [ ] **Step 3:** Project-wide literal replace `RushOrder.Desktop.Notifications` → `RushOrder.Desktop.Controls` across the same scope.
- [ ] **Step 4:** `rmdir` the now-empty `Forms/Controls/` and `Notifications/` directories.
- [ ] **Step 5:** Build `RushOrder.Desktop.csproj`. Expected: 0 errors.
- [ ] **Step 6:** Commit: `git commit -m "refactor(desktop): move controls to Controls/"`.

### Task 2: Views/Shell/ (Forms/MainForm, LoginForm)

**Files:**
- Move: `Forms/MainForm.cs`, `Forms/LoginForm.cs` → `Views/Shell/`
- Delete (now empty): `Forms/`

- [ ] **Step 1:** `git mv Forms/MainForm.cs Views/Shell/MainForm.cs` and `git mv Forms/LoginForm.cs Views/Shell/LoginForm.cs`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Forms` → `RushOrder.Desktop.Views.Shell` across `desktop/src/RushOrder.Desktop/**/*.cs` (excluding `obj/`, `bin/`). (Safe: Task 1 already removed every `RushOrder.Desktop.Forms.Controls` occurrence, so this match is unambiguous.)
- [ ] **Step 3:** `rmdir` the now-empty `Forms/` directory.
- [ ] **Step 4:** Build. Expected: 0 errors.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): move shell forms to Views/Shell/"`.

### Task 3: Styles/ (Theme/*)

**Files:**
- Move: `Theme/{ThemeManager,PoppinsFont,GdiExtensions}.cs` → `Styles/`
- Delete (now empty): `Theme/`

- [ ] **Step 1:** `git mv` each of the 3 files into `Styles/`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Theme` → `RushOrder.Desktop.Styles` (this namespace is referenced by ~35 files — expect a wide diff, all mechanical).
- [ ] **Step 3:** `rmdir Theme/`.
- [ ] **Step 4:** Build. Expected: 0 errors.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): move theme/design tokens to Styles/"`.

### Task 4: Views/Tables/ (Views/FloorPlan/*)

**Files:**
- Move: `Views/FloorPlan/{FloorPlanView,TableDetailPanel,TableFloorPlanControl,TableShape}.cs` → `Views/Tables/`
- Delete (now empty): `Views/FloorPlan/`

- [ ] **Step 1:** `git mv` each of the 4 files into `Views/Tables/`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Views.FloorPlan` → `RushOrder.Desktop.Views.Tables`.
- [ ] **Step 3:** `rmdir Views/FloorPlan/`.
- [ ] **Step 4:** Build. Expected: 0 errors.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): move floor plan views to Views/Tables/"`.

### Task 5: Views/Dashboard/AiDashboard/ (Views/AiDashboard/*)

**Files:**
- Move: `Views/AiDashboard/AiDashboardView.cs` → `Views/Dashboard/AiDashboard/AiDashboardView.cs`
- Move: `Views/AiDashboard/Widgets/{KitchenEtaWidget,SuggestionOfTheDayWidget,TodayForecastWidget}.cs` → `Views/Dashboard/AiDashboard/Widgets/`
- Delete (now empty): `Views/AiDashboard/`

- [ ] **Step 1:** `git mv` the 3 widget files into `Views/Dashboard/AiDashboard/Widgets/`, then `git mv Views/AiDashboard/AiDashboardView.cs Views/Dashboard/AiDashboard/AiDashboardView.cs`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Views.AiDashboard.Widgets` → `RushOrder.Desktop.Views.Dashboard.AiDashboard.Widgets` (do this BEFORE step 3).
- [ ] **Step 3:** Project-wide literal replace `RushOrder.Desktop.Views.AiDashboard` → `RushOrder.Desktop.Views.Dashboard.AiDashboard`.
- [ ] **Step 4:** `rmdir` the now-empty `Views/AiDashboard/Widgets/` and `Views/AiDashboard/`.
- [ ] **Step 5:** Build. Expected: 0 errors.
- [ ] **Step 6:** Commit: `git commit -m "refactor(desktop): nest AI dashboard under Views/Dashboard/"`.

### Task 6: Views/Statistics/Forecast/ (Views/Forecast/*)

**Files:**
- Move: `Views/Forecast/DemandForecastControl.cs` → `Views/Statistics/Forecast/DemandForecastControl.cs`
- Delete (now empty): `Views/Forecast/`

- [ ] **Step 1:** `git mv Views/Forecast/DemandForecastControl.cs Views/Statistics/Forecast/DemandForecastControl.cs`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Views.Forecast` → `RushOrder.Desktop.Views.Statistics.Forecast`.
- [ ] **Step 3:** `rmdir Views/Forecast/`.
- [ ] **Step 4:** Build. Expected: 0 errors.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): nest forecast control under Views/Statistics/"`.

### Task 7: Views/Orders/Print/ (Views/Print/*)

**Files:**
- Move: `Views/Print/PrinterConfigDialog.cs` → `Views/Orders/Print/PrinterConfigDialog.cs`
- Delete (now empty): `Views/Print/`

- [ ] **Step 1:** `git mv Views/Print/PrinterConfigDialog.cs Views/Orders/Print/PrinterConfigDialog.cs`.
- [ ] **Step 2:** Project-wide literal replace `RushOrder.Desktop.Views.Print` → `RushOrder.Desktop.Views.Orders.Print`.
- [ ] **Step 3:** `rmdir Views/Print/`.
- [ ] **Step 4:** Build. Expected: 0 errors.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): nest print dialog under Views/Orders/"`.

### Task 8: Animations/ scaffold + final validation

**Files:**
- Create: `Animations/.gitkeep`

- [ ] **Step 1:** Create empty `Animations/` folder with a `.gitkeep` file (no source files exist to migrate here yet).
- [ ] **Step 2:** Full solution build (`rush-order.sln` or at least `RushOrder.Desktop.csproj` + `RushOrder.Desktop.Tests.csproj`). Expected: 0 errors, no new warnings vs. pre-reorg baseline.
- [ ] **Step 3:** Run `RushOrder.Desktop.exe` and confirm: login/Shell window opens, and at least one moved module (e.g. Views/Tables or Views/Shell-launched Dashboard) renders without missing-resource errors (catches broken relative asset paths, e.g. `Assets/Fonts` embedded resource references, which are untouched but should be re-verified).
- [ ] **Step 4:** Diff `git status` / `git log --stat` since the start of this plan to produce the final per-folder move count summary.
- [ ] **Step 5:** Commit: `git commit -m "refactor(desktop): scaffold empty Animations/ folder"`.

---

## Self-Review Notes

- **Spec coverage:** every named target folder (Views/Shell, Tables, Orders, Kitchen, Menu, Statistics, Sync, Pending, Controls, Styles, Animations) is addressed. `Pending/` has no source files to move (per memory: the Camareros/Reservas/Facturación sidebar entries are stubs inside `MainForm.cs`, not separate files) — it will not be created empty since the user didn't ask for it explicitly like Animations; flag this in the final summary rather than guessing.
- **Placeholder scan:** none — every step names exact files and exact namespace strings.
- **Ordering safety:** longer/more-specific namespace strings (`.Controls`, `.Widgets`) are always replaced before their shorter parent string within the same task, and each task's target string is verified (via grep during planning) not to collide with any *other* task's old/new string.
