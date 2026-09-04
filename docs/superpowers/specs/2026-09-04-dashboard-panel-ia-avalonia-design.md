# Dashboard (DASH-01) + Panel IA (DASH-02) — Avalonia UI Design

**Fecha:** 2026-09-04
**Fase:** 1 de la migración WinForms → Avalonia UI del panel de escritorio Rush Order.
**Precede a:** Tables/TBL-01, luego Orders/ORD-01–04 (mismo orden en que fueron pedidos).

## Contexto y decisiones previas

- El pedido original asumía un Shell-01 ya implementado en Avalonia. No existe — solo hay un shell WinForms (`MainForm`/`LoginForm`, `desktop/src/RushOrder.Desktop/Views/Shell/`). Esta fase construye Shell-01 como prerequisito mínimo.
- Dashboard y Panel IA **ya están implementados y funcionando en WinForms** (`Views/Dashboard/DashboardView.cs`, `Views/Dashboard/AiDashboard/AiDashboardView.cs`) — esta fase es un puerto a Avalonia+MVVM, no una funcionalidad nueva, salvo donde se indica explícitamente.
- Alcance confirmado con el usuario: proyecto nuevo en paralelo (`RushOrder.Desktop.Avalonia`), sin tocar el WinForms existente. .NET 8 (el SDK 9 no está instalado; 8 coincide con el resto del stack).
- Fuente de los design tokens: `docs/design-system.md` (colores, tipografía, espaciado, radios — ya sincronizado desde Claude Design, tomado de `ThemeManager.cs`/`tailwind.config.ts`).

## 1. Proyecto y estructura

Nuevo proyecto `desktop/src/RushOrder.Desktop.Avalonia/` (net8.0, Avalonia 11 + `CommunityToolkit.Mvvm`), agregado a `rush-order.sln`. `ProjectReference` a `RushOrder.Desktop.Core` para reutilizar `RestaurantHubClient` (cliente SignalR) sin reescribirlo.

```
RushOrder.Desktop.Avalonia/
  App.axaml (+ .cs)                          — DI host (Microsoft.Extensions.Hosting), arranque
  Views/
    Shell/MainWindow.axaml                   — Shell-01: ventana + sidebar + área de contenido
    Dashboard/DashboardView.axaml             — DASH-01
    Dashboard/AiDashboard/AiDashboardView.axaml — DASH-02
  ViewModels/
    Shell/MainWindowViewModel.cs
    Dashboard/DashboardViewModel.cs + 6 *WidgetViewModel.cs
    Dashboard/AiDashboard/AiDashboardViewModel.cs + 4 *WidgetViewModel.cs
  Controls/
    KpiCardBase.axaml(.cs)                   — card genérica: header, estado (loading/empty/error/data), retry
    SparklineControl.cs / OccupancyArcControl.cs — puerto vectorial (DrawingContext) del GDI+ actual
  Animations/
    KpiValueTransition.cs                    — easing/duración compartidos para el contador animado
    CardStateTransition.axaml                — cross-fade reutilizable entre estados de la card
    RefreshPulse.cs                           — pulso al recibir un patch de RealTimeService
  Services/    — DashboardDataService, ForecastDataService, RealTimeService (puerto async, mismos endpoints/DTOs)
  Models/      — mismos records que hoy (DashboardKpi, AlertDto, DemandForecastResult, Result<T>, etc.)
  Navigation/  — INavigationService.cs + PlaceholderView.axaml
  Styles/DesignTokens.axaml                  — ResourceDictionary desde docs/design-system.md
```

No se comparte `Models`/`Services` con el proyecto WinForms — duplicar el contrato REST (~150 líneas por servicio) aísla el riesgo (cero blast radius sobre la app en producción) y evita acoplar dos frameworks de UI a un mismo tipo. Si en fases futuras duele la duplicación, se extrae un classlib compartido entonces (YAGNI ahora).

**DASH-02 mantiene el widget de Alertas** (paridad con el WinForms actual: grid 2×2 — Previsión, Alertas, Sugerencia, ETA cocina), aunque el prompt original solo mencionara 3 widgets — decisión confirmada con el usuario.

## 2. Estados por widget y data layer

Carga independiente por widget — no hay loader global.

```csharp
public enum WidgetLoadState { Loading, Loaded, Empty, Error }

public abstract partial class WidgetViewModelBase : ObservableObject
{
    [ObservableProperty] private WidgetLoadState _state = WidgetLoadState.Loading;
    [ObservableProperty] private bool _isShowingSimulatedData;

    [RelayCommand] private async Task Retry() => await LoadAsync();
    protected abstract Task LoadAsync();
}
```

**`WidgetLoadState` y `IsShowingSimulatedData` son dos dimensiones independientes:**

- `WidgetLoadState` representa **exclusivamente** el resultado del fetch a la fuente real: `Loading` (en curso), `Loaded` (respuesta real exitosa con datos), `Empty` (respuesta real exitosa sin datos), `Error` (el fetch real falló).
- `IsShowingSimulatedData` representa **exclusivamente** el origen de lo que se está pintando ahora mismo en la UI.

**Regla:** los datos simulados nunca provocan `State = Loaded`. `Loaded` significa siempre "la fuente real respondió con éxito". Los datos simulados son datos de contingencia, siempre asociados a `State = Error`.

| Resultado fetch real | State | Simulados | UI |
|---|---|---|---|
| En curso | `Loading` | No | Loading/skeleton |
| OK + datos | `Loaded` | No | Datos reales |
| OK + vacío | `Empty` | No | Empty state |
| Error | `Error` | Sí | Datos simulados + banner no bloqueante + Retry |
| Retry OK | `Loaded` | No | Datos reales |

**Responsabilidad por capa:**

- El servicio (`DashboardDataService`, `ForecastDataService`) solo ejecuta el fetch real y devuelve `Result<T>` — `Ok(data)` si la API respondió, `Fail(exception)` si falló. Nunca oculta una excepción devolviendo datos mock; nunca convierte silenciosamente un error de red en `Ok(mockData)` (a diferencia del `DashboardDataService` WinForms actual, que sí lo hace en el `catch`).
- El ViewModel interpreta el `Result<T>`: `Ok` con contenido → `Loaded`; `Ok` vacío → `Empty`; `Fail` → `Error` + `IsShowingSimulatedData = true` + carga el fallback simulado.
- `RetryCommand` reintenta exclusivamente el fetch real. Éxito → `Loaded` + `IsShowingSimulatedData = false`. Fallo de nuevo → `Error` + `IsShowingSimulatedData = true`.

## 3. Real-time dirigido, animaciones y navegación

**Real-time por widget** (mejora sobre el WinForms actual, que hoy dispara un `LoadKpiAsync()` completo en `OrderStatusUpdated`/`TableStatusChanged`):

| Evento `RealTimeService` | Widget afectado | Acción |
|---|---|---|
| `OrderReceived` | ActiveOrdersWidgetVM | incrementa `OrdersWaiting` localmente (optimista) |
| `OrderStatusUpdated(id, status, ts)` | ActiveOrdersWidgetVM | mueve el conteo entre waiting/preparing/ready, sin refetch |
| `TableStatusChanged(id, status)` | TablesWidgetVM | ajusta `TablesOccupied` localmente |
| `KitchenAlert` / `MiseEnPlaceAlert` | AlertsWidgetVM | prepend/update |

Cada patch dispara `RefreshPulse` (`Animations/`) solo sobre esa card; el resto del grid no se re-mide ni se re-renderiza.

**Animaciones en hilo de composición:** valores numéricos bindeados a un `double` intermedio (`AnimatedValue`) con `DoubleTransition` de Avalonia (corre en el compositor, no en el hilo de UI); un converter formatea `AnimatedValue` a moneda/entero para el `TextBlock`. Transiciones entre estados de card vía `CardStateTransition` (cross-fade declarativo). Sin `DispatcherTimer` + `Invalidate()` manual (patrón presente como hack de debug en el `DashboardView.cs` WinForms actual — evitarlo es intencional).

**Sin red en el hilo de UI:** todo `Load*Async` es `async Task`, invocado desde `AttachedToVisualTree`/`AsyncRelayCommand`; cero `.Result`/`.Wait()`.

**Navegación:** `INavigationService.NavigateTo(routeKey, param?)`. Pedidos activos → `"orders/kanban"`; mesas → `"tables/floorplan"`. Ambas rutas resuelven hoy a `PlaceholderView` ("Próximamente") hasta que Tables/Orders existan en fases siguientes — mismo patrón que se reutilizará para el CTA "Crear pedido" de TBL-01 → `"orders/new"`.

**Verificación 60fps:** `AttachDevTools()` en builds Debug (F12 abre el overlay de FPS de Avalonia.Diagnostics). Validación manual al terminar: lanzar la app, disparar varios refresh de widgets (timer + evento simulado), observar el contador de FPS durante la animación del contador y el cross-fade de estado.

## Fuera de alcance (fases posteriores)

- Tables/TBL-01, Orders/ORD-01–04: specs propios, siguientes en la cola.
- Extracción de un classlib `Models`/`Services` compartido con WinForms, si la duplicación empieza a doler.
- Restricción de rol sobre cualquier acción (no pedida en esta fase).

## Self-review

- Sin placeholders (`TBD`/`TODO`) — todas las decisiones fueron confirmadas con el usuario o son continuación directa de un patrón ya validado en el WinForms actual.
- Consistencia interna verificada: la matriz de estados de la Sección 2 no contradice el flujo real-time de la Sección 3 (un patch real-time siempre implica `State = Loaded` porque proviene de datos ya confirmados por el backend vía push, nunca del fallback simulado).
- Alcance: un solo módulo (Dashboard + Panel IA, ya tratado como una unidad en el WinForms actual) — no requiere descomponerse más.
