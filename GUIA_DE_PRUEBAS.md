# Guía de pruebas — Rush Order (de 0 a 100)

Guía paso a paso para levantar y probar el proyecto completo: infraestructura, backend
(.NET 8), PWA de cliente (React), app de escritorio (WinForms), y toda la batería de tests
automatizados. Está escrita a partir del estado real del código a día de hoy — incluye una
sección de **problemas conocidos** al final con los huecos reales que existen entre módulos,
para que no pierdas tiempo pensando que algo que falla es culpa tuya.

---

## 0. Requisitos previos

| Herramienta | Versión | Para qué |
|---|---|---|
| .NET SDK | 8.0 | Backend, desktop, tests |
| Node.js | 20 LTS | PWA, E2E |
| npm | 10+ | PWA, E2E |
| Docker + Docker Compose | 24+ / 2.x | Postgres, Redis, pgAdmin, MailHog, integration tests |
| Git | — | — |
| k6 | opcional | Load testing (Fase 7) |
| Visual Studio 2022 o `dotnet` CLI | — | Compilar/depurar la app de escritorio (WinForms, requiere Windows) |

Herramienta de EF Core (para migraciones manuales, normalmente no hace falta — ver §2):

```bash
dotnet tool install --global dotnet-ef
```

---

## 1. Infraestructura (Postgres, Redis, pgAdmin, MailHog)

```bash
cp .env.example .env
bash infrastructure/docker/start-dev.sh
```

Esto levanta **todos** los servicios definidos en `infrastructure/docker/docker-compose.yml`,
incluido un contenedor `api` dockerizado. Para desarrollo normal es más cómodo correr el
backend directamente con `dotnet run` (hot reload, debugging) en vez del contenedor — si
haces eso, para el contenedor `api` para evitar que ambos compitan por el puerto 5000:

```bash
docker stop rushorder_api
```

Verifica que están arriba:

| Servicio | URL |
|---|---|
| PostgreSQL | `localhost:5432` (`rushorder` / `rushorder_dev_pass`) |
| Redis | `localhost:6379` |
| pgAdmin | http://localhost:5050 (`admin@rushorder.local` / `admin`) |
| MailHog (UI de correos capturados) | http://localhost:8025 |

Para reiniciar todo desde cero (borra volúmenes):

```bash
bash infrastructure/docker/reset-dev.sh
```

---

## 2. Backend (.NET 8 API)

```bash
cd backend/src/RushOrder.API
dotnet run
```

**La primera vez que arranca, el backend hace TODO solo** — no hace falta correr
`dotnet ef database update` a mano. `DatabaseInitializer` (un `IHostedService`) al arrancar:

1. Aplica todas las migraciones pendientes (`Database.MigrateAsync`).
2. Siembra datos de desarrollo (`DatabaseSeeder.SeedDevelopmentDataAsync`) — un restaurante
   demo completo, usuarios, mesas, productos y pedidos de ejemplo.

Mira la consola: deberías ver `Applying N pending migration(s)` seguido de
`Development seed complete`. Si el arranque falla aquí, revisa que Postgres esté accesible
(`docker ps`, `.env`) — el hosted service relanza la excepción a propósito para no dejar
arrancar la app con un esquema roto.

> ⚠️ Dos de las migraciones (`AddRecommendationsAndExperiments` y
> `AddDemandForecastingAndKitchenTracking`) se escribieron a mano porque `dotnet ef migrations
> add` no pudo ejecutarse en el entorno donde se generaron (bloqueo de Windows al cargar por
> reflexión el DLL recién compilado). El primer arranque en tu máquina es la primera
> verificación real de que aplican sin errores — si algo falla ahí, es lo primero a mirar.

Verifica que el API responde:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/health/detailed   # Postgres, Redis, Service Bus (si está configurado)
```

Swagger: http://localhost:5000/swagger

### Credenciales sembradas (restaurante demo "El Rincón del Chef")

| Rol | Email | Password |
|---|---|---|
| Owner | `owner@demo.com` | `Demo1234!` |
| Manager | `manager@demo.com` | `Demo1234!` |
| Waiter | `waiter@demo.com` | `Demo1234!` |
| Kitchen | `kitchen@demo.com` | `Demo1234!` |
| Admin de plataforma (tenant "Rush Order System") | `admin@rushorder.app` | `Admin1234!` |

El seed también crea 15 mesas (3 zonas × 5 mesas), 20 productos en 4 categorías, y 5 pedidos
de ejemplo en distintos estados (Pending/Confirmed/Preparing/Ready/Paid).

### Obtener el `restaurantId` y un QR de mesa reales

Ni el `restaurantId` ni el código QR de cada mesa son fijos — se generan como GUID/código
aleatorio en el momento del seed. Para obtenerlos:

1. Inicia sesión como owner:
   ```bash
   curl -X POST http://localhost:5000/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"owner@demo.com","password":"Demo1234!"}'
   ```
   Copia el `accessToken` de la respuesta (si `requiresMfa` es `false`, que lo será por defecto).

2. Con ese token, pide el listado de restaurantes/mesas del tenant (o consulta directamente
   en pgAdmin → `rushorder_dev` → tabla `restaurants` / tabla `tables`, columnas `Id` /
   `QrCode`). La vía más rápida en desarrollo es SQL directo en pgAdmin:
   ```sql
   SELECT r."Id" AS restaurant_id, t."Name", t."QrCode"
   FROM restaurants r JOIN tables t ON t."RestaurantId" = r."Id";
   ```

Guarda un `restaurant_id` y un `QrCode` — los necesitas para la PWA (§3) y para probar
endpoints como `/api/v1/menu/public/{qrCode}` o `/api/v1/analytics/demand-forecast?restaurantId=...`.

### Configuración opcional (se degradan solas si no las rellenas)

Estas integraciones externas no bloquean el arranque — si faltan, el servicio correspondiente
simplemente no hace nada (con un warning en el log):

- **Stripe** (`appsettings.Development.json` → `Stripe:SecretKey`): pon tus claves de test
  (`sk_test_...`) si vas a probar pagos.
- **SendGrid** (`SendGrid:ApiKey`, no está en `appsettings.Development.json` por defecto —
  añádela tú si quieres probar el email semanal de insights, ver §6.7): sin clave, el envío
  se salta con un warning en el log.
- **Correo transaccional (reset de contraseña, recibos, etc.)**: usa MailHog por SMTP en
  desarrollo (ya configurado) — ábrelo en http://localhost:8025 para ver los correos.
- **date.nager.at** (festivos para la previsión de demanda): API pública sin clave, requiere
  acceso a internet saliente; si no hay red, el motor de previsión simplemente no aplica el
  multiplicador de festivos.

---

## 3. PWA de cliente (React + Vite)

```bash
cd pwa
cp .env.example .env
npm install
```

Edita `.env`:

```
VITE_API_URL=http://localhost:5000/api
VITE_RESTAURANT_ID=<el QrCode de una mesa que sacaste en el paso 2 — ver "Problemas conocidos" §7.1>
VITE_STRIPE_PUBLISHABLE_KEY=pk_test_...   # opcional, solo si vas a probar pagos
```

```bash
npm run dev
```

Abre http://localhost:5173 — esto carga la ruta `/` (`MenuPage`, el menú de fallback), que usa
`VITE_RESTAURANT_ID` como si fuera un código QR de mesa. **Por eso el valor correcto ahí es un
`QrCode` de mesa, no el `restaurantId`** — ver §7.1.

---

## 4. App de escritorio (WinForms — requiere Windows)

```bash
cd desktop/src/RushOrder.Desktop
dotnet run
```

O ábrela en Visual Studio (`rush-order.sln`, proyecto `RushOrder.Desktop`) y F5.

No tiene archivo de configuración — apunta a `http://localhost:5000` (hardcodeado en los
servicios de datos), así que el backend debe estar corriendo ahí antes de abrir el escritorio.

Inicia sesión con cualquiera de las credenciales sembradas de la tabla de §2 (usa
`owner@demo.com` para ver todo, incluida la sección de administración/facturación si la hay).

Recorrido de pantallas a probar: **Dashboard** → **Mesas** (plano interactivo) → **Pedidos**
(kanban) → **Cocina** (KDS, pantalla secundaria) → **Menú** (gestión de productos) →
**Estadísticas** → **Previsión** (pronóstico de demanda, nuevo) → **Panel IA** (nuevo).

---

## 5. Tests automatizados

### 5.1 Backend — unitarios (no requieren Docker)

```bash
cd backend/tests/RushOrder.Domain.Tests      && dotnet test
cd backend/tests/RushOrder.Application.Tests && dotnet test
```

### 5.2 Backend — integración (requiere Docker — usa Testcontainers)

```bash
cd backend/tests/RushOrder.API.IntegrationTests
dotnet test
```

Esto levanta contenedores Postgres/Redis efímeros por sesión de test (Testcontainers) y los
resetea entre tests con Respawn — no toca tu Postgres de desarrollo del §1. **Actualmente esto
no funciona así en la práctica — ver §7.8.**

### 5.3 PWA — unitarios (Vitest)

```bash
cd pwa
npm run test:ci
```

### 5.4 E2E (Playwright)

Hay dos suites (ver nota en §7.4 sobre por qué hay dos):

```bash
# Suite principal
cd e2e
npm install
npm run install:browsers
npm run test

# Smoke suite (la que corre en CI)
cd pwa
npx playwright install --with-deps chromium
npx playwright test tests/e2e/smoke.spec.ts
```

Necesitan el backend y la PWA corriendo (`dotnet run` + `npm run dev` en paralelo), o pásales
`BASE_URL`/variables de entorno apuntando a un entorno desplegado.

### 5.5 Load testing (k6)

```bash
cd load-testing
./run-load-tests.sh local menu-load
```

Requiere `k6` instalado y el backend corriendo en `http://localhost:5000`. Antes de correr
contra un entorno real (staging/producción), lee el aviso sobre el rate limiter en
`load-testing/run-load-tests.sh` — con concurrencia alta necesitas `DisableRateLimit=true` en
el backend o vas a medir el limitador, no la capacidad real de la app.

---

## 6. Recorrido manual guiado (lo importante — probar que la app funciona de verdad)

### 6.1 Flujo del cliente (PWA)

1. Con la PWA abierta en el `QrCode` correcto (§3), deberías ver el menú de "El Rincón del
   Chef" con sus 4 categorías y 20 productos.
2. Abre el detalle de un producto (p. ej. "Croquetas de jamón ibérico") → deberías ver la
   sección **"También te puede gustar"** con sugerencias.
3. Añade 2-3 productos al carrito. Abre el carrito → deberías ver **"¿Añadirías algo más?"**
   (solo aparece si te tocó la variante B del experimento A/B — es 50/50, prueba con otro
   fingerprint de dispositivo/navegador en incógnito si no te sale).
4. Avanza a "Revisar pedido" → si el carrito no tiene postre/bebida, deberías ver el banner de
   upselling ("¿Olvidaste el postre?" / "¿Algo para beber?").
5. Completa el pedido (pago en efectivo si no configuraste Stripe).
6. Deberías caer en la pantalla de tracking (`/tracking/:orderId`) con el estado en vivo.
7. Espera ~30s tras marcar el pedido como servido (desde el escritorio, ver 6.2) para que
   aparezca el `RatingSheet` y valóralo.

### 6.2 Flujo de sala/cocina (Desktop)

1. Inicia sesión como `waiter@demo.com` → **Pedidos** → deberías ver el pedido que acabas de
   crear desde la PWA aparecer en la columna "Pendiente" en tiempo real (SignalR).
2. Confírmalo, muévelo por el kanban (Confirmado → Preparando → Listo → Servido). Cada
   transición debería reflejarse también en la pantalla de tracking de la PWA en tiempo real.
3. Abre **Cocina** (KDS) en una segunda ventana — el mismo pedido debería aparecer ahí también.
4. Vuelve a **Dashboard** — el widget de alertas debería reflejar cualquier anomalía activa
   (pedidos pendientes hace tiempo, stock bajo, etc.).

### 6.3 Predicción de demanda y panel de IA

1. **Previsión** → selecciona "Hoy". Con solo 5 pedidos de seed no vas a tener histórico de
   4 semanas, así que espera ver **confianza baja (🔴)** en casi todo — es el comportamiento
   correcto (fase cold-start), no un bug.
2. El job `DemandForecastJob` solo corre automáticamente a las 06:00 UTC. Para ver datos sin
   esperar a que sea esa hora, tendrías que insertar pedidos históricos de prueba tú mismo
   (o simplemente verificar que la pantalla carga y no revienta con "sin datos").
3. **Panel IA** → los 4 widgets deberían cargar: previsión de hoy (gráfico), sugerencia del
   día, alertas de IA (reutiliza el mismo feed que el Dashboard), y ETA medio de cocina (vacío
   hasta que haya pedidos completados con histórico de `order_status_history`).

### 6.4 Recomendaciones — reglas manuales de maridaje

Estas se gestionan solo por API (no hay pantalla de admin todavía):

```bash
curl -X POST http://localhost:5000/api/v1/recommendations/pairing-rules \
  -H "Authorization: Bearer <token de owner/manager>" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":"<restaurantId>","sourceProductId":"<id croquetas>","targetProductId":"<id cerveza>"}'
```

Vuelve a abrir el detalle de "Croquetas" en la PWA → debería aparecer la cerveza como
sugerencia con motivo "Perfecto con tu selección".

### 6.5 Multi-tenancy / suscripciones / admin de plataforma

Inicia sesión con `admin@rushorder.app` para el panel de administración de la plataforma
(gestión de tenants, suscripciones). Prueba también el flujo de onboarding
(`POST /api/v1/onboarding/register`) para crear un tenant/restaurante nuevo desde cero y
confirmar el aislamiento multi-tenant (los datos de un tenant no deben verse desde otro).

### 6.6 Backups y seguridad

Los scripts de `scripts/backup-postgresql.sh` y `scripts/rotate-secrets.sh` son para
staging/producción (Azure) — no hace falta correrlos en local. Repasa
`docs/security/pentest-checklist.md` si quieres validar cabeceras de seguridad
(`SecurityHeadersMiddleware`) con curl:

```bash
curl -I http://localhost:5000/api/v1/menu/public/<qrCode>
```

### 6.7 Insights semanales por email

Solo corre los lunes a las 09:00 UTC. Para probarlo sin esperar, la forma más simple es
comentar temporalmente la condición de día/hora en `WeeklyInsightsJob.ExecuteAsync` y
reiniciar el backend — con `SendGrid:ApiKey` sin configurar, el envío se registra en el log
pero no sale nada (recuerda revertir el cambio después).

---

## 7. Problemas conocidos (para no perder tiempo pensando que es cosa tuya)

Estos son huecos reales detectados durante el desarrollo — no son errores de tu setup.

### 7.1 El flujo de entrada por QR de la PWA no llega al backend real

`LandingPage.tsx` (la ruta `/menu/:qrToken`, pensada como entrada principal) llama a
`GET /v1/qr/:token`, que **no existe en ningún controller del backend**. La ruta de fallback
(`/`, `MenuPage.tsx`) sí funciona, pero solo si le pasas como `VITE_RESTAURANT_ID` el `QrCode`
de una **mesa** (no el `restaurantId` del restaurante) — es lo que se indica en §3.

### 7.2 Promociones: diseño mínimo (texto libre, sin segmentación)

`Promotion` (dominio + `PromotionDto`) es solo `Name` + `Description` — sin asociación a
productos/categorías, sin cupones ni reglas de descuento. Es una decisión de diseño deliberada
para la primera versión, a revisar más adelante si el negocio lo pide. `GetPublicMenuQueryHandler`
puebla `ActivePromotions` consultando `IPromotionRepository.GetActiveByRestaurantAsync`
(filtra por `IsActive` y por fecha actual dentro de `[StartDate, EndDate]`).

(Nota histórica: esta sección documentaba un supuesto bug de desenvuelto de envelope en
`useMenu.ts`/`usePromotions.ts` — se confirmó que el interceptor de axios ya desenvuelve
`{ status, data }` correctamente y no hacía falta ningún cambio ahí.)

### 7.3 `StatisticsDataService` (escritorio) siempre usa datos mock

La pantalla **Estadísticas** del escritorio nunca llega a parsear la respuesta real de
`/api/v1/analytics/*` — el propio código tiene un comentario admitiéndolo. Vas a ver siempre
los mismos números de ejemplo ahí, sin importar qué pedidos hayas creado. **Previsión** y
**Panel IA** (los módulos nuevos) sí parsean la respuesta real — compáralos si necesitas el
patrón correcto.

### 7.4 Dos suites de Playwright redundantes

`e2e/` (raíz) y `pwa/tests/e2e/` tienen configuraciones de Playwright independientes que se
solapan parcialmente. No se consolidaron — corre la que necesites según §5.4.

### 7.5 Migraciones escritas a mano

`AddRecommendationsAndExperiments` y `AddDemandForecastingAndKitchenTracking` se escribieron
sin poder ejecutar `dotnet ef migrations add` (ver nota en §2). Si el backend no arranca la
primera vez por un error de migración, es el primer sitio a revisar — probablemente un tipo de
columna o un nombre de índice mal escrito a mano.

### 7.6 SignalR en vez de Web Push real para el aviso de mise en place

El aviso diario de las 08:00 ("hoy se espera vender...") se especificó como notificación push
del navegador a una PWA de encargado — esa PWA no existe en este proyecto (solo hay PWA de
cliente). Se implementó en su lugar como evento SignalR al escritorio (donde el encargado
realmente trabaja), visible en el widget de alertas del Dashboard.

### 7.7 Confianza baja por defecto en la previsión de demanda

No es un bug: con pocos pedidos históricos (recién sembrado el proyecto), la previsión de
demanda va a mostrar confianza baja (🔴) en casi todos los productos, porque el algoritmo
necesita hasta 4 semanas de historial por franja horaria para tener confianza alta. Genera
pedidos de prueba repetidamente durante varios "días" (o edita `CreatedAt` directamente en la
tabla `orders` vía SQL) si quieres ver confianza media/alta.

### 7.8 `RushOrder.API.IntegrationTests` conecta a tu Postgres de desarrollo, no al contenedor

`dotnet test` en `API.IntegrationTests` falla hoy con `"No tables found... Consider
initializing the database and/or running migrations"` en cuanto tu Postgres de desarrollo del
§1 ya tiene las migraciones aplicadas (el caso normal tras seguir esta guía una vez).

Causa: `RushOrder.Infrastructure/DependencyInjection.cs` lee `Database:ConnectionString` de
forma **eager** (`var connectionString = configuration.GetSection(...)[...]`) en el momento en
que `AddInfrastructure` se registra en el contenedor de DI, en vez de leerlo de forma perezosa
dentro del lambda de `UseNpgsql`. `ApiFactory` (el `WebApplicationFactory` de los tests) inyecta
la cadena de conexión del contenedor Testcontainers vía `ConfigureAppConfiguration`, pero esa
fuente de configuración se añade **después** de que `Program.cs` ya llamó a `AddInfrastructure`
y capturó la cadena de conexión de `appsettings.Development.json` (tu Postgres real). El
`AppDbContext` de los tests termina apuntando a tu base de datos de desarrollo, no al contenedor
efímero — que por eso "ya tiene todo aplicado" y Respawn no encuentra nada que resetear en el
contenedor real.

No es un problema de versiones de paquetes NuGet (se investigó a fondo en la migración a
Central Package Management — ver `Directory.Packages.props` — que sí arregló un conflicto real
de ensamblados de EF Core, pero es un problema distinto). El fix es cambiar la lectura de la
cadena de conexión en `DependencyInjection.cs` a perezosa (leerla dentro del lambda de
`options.UseNpgsql(...)`, con acceso al `IConfiguration` inyectado en vez de una variable
capturada). No se aplicó aquí por quedar fuera del alcance de esa tarea (solo configuración de
paquetes, no lógica de negocio).

### 7.9 `pwa-installable.spec.ts` — el Service Worker no llegaba a registrarse contra el build de producción — **resuelto (2026-08-14)**

**Causa original y arreglo.** La página lanzaba un error de JavaScript no capturado que
interrumpía la ejecución del bundle antes de que `registerSW()` (`pwa/src/app/main.tsx:24`)
llegara a completar el registro:

```
PAGEERROR: Cannot read properties of undefined (reading 'useState')
    at http://localhost:5173/assets/vendor-misc-*.js:25:133
```

Resolviendo el sourcemap de ese chunk, la posición caía dentro de
`node_modules/use-sync-external-store/cjs/use-sync-external-store-shim.production.js:17` — el
shim que usa `zustand` para su hook de suscripción a store externo en React 18. Causa: el
`manualChunks` de `pwa/vite.config.ts` mandaba `zustand` al chunk `vendor-misc` mientras que
`react`/`react-dom` iban a `vendor-react`; `use-sync-external-store` necesita una referencia
viva a `React`, y al quedar en un chunk distinto esa referencia llegaba `undefined` al evaluarse
el chunk.

Arreglado moviendo `zustand` (y explícitamente `use-sync-external-store`) al chunk
`vendor-react` en `manualChunks`. Verificado con `npm run build` (el resto de chunks queda
exactamente igual en tamaño — solo `vendor-misc` baja ~7.3 kB y `vendor-react` sube lo mismo) y
con:

```bash
cd pwa
npm run build
npx vite preview --port 5173
npx playwright test --config=tests/e2e/playwright.config.ts pwa-installable.spec.ts --project=chromium
```

El `PAGEERROR` ya no aparece y el Service Worker registra correctamente.

**Dos problemas nuevos, distintos y sin relación con el bundling, quedaron al descubierto al
arreglar lo anterior** — antes ni siquiera se llegaba a ejecutar el código que los dispara,
porque la página se rompía antes. Ambos eran bugs en las aserciones del propio test, no del
producto, y también están **arreglados (2026-08-14)**:

- `Service Worker registers and becomes active` fallaba porque
  `expect(['activated', 'activating', 'installed']).toContain(swState.state)` recibía
  `swState.state === 'installing'` — un estado real y válido de Service Worker, capturado
  demasiado pronto: con un precache de 852 KiB es normal que el worker siga instalando justo
  tras `networkidle`. Arreglado con un sondeo (poll cada 250ms, hasta 15s) dentro del propio
  `page.evaluate` que espera a que el estado deje de ser `installing`/`unknown` antes de leerlo,
  en vez de ampliar la lista de estados aceptados.
- `beforeinstallprompt event is dispatchable` asumía que la app no debía suprimir el evento,
  pero `pwa/src/shared/hooks/usePwaInstall.ts:28` llama a `e.preventDefault()` **a propósito**,
  para diferir el prompt nativo y mostrar su propia UI (`PwaInstallBanner`, gateada por
  `usesCount >= REQUIRED_USES` y el cooldown de descarte). Renombrado a
  `beforeinstallprompt is intercepted to show the custom install banner` y reescrito para
  validar el comportamiento real: siembra `localStorage['pwa-uses'] = '1'` antes de navegar
  (para que este único mount cumpla `usesCount >= REQUIRED_USES`), confirma que el evento
  síntetico llega interceptado (`prevented === true`) y que `PwaInstallBanner` (`div[role=banner]`
  — se desambigua del `<header>` de la página, que también es un landmark `banner`) se hace
  visible.

Verificado con `npx playwright test --config=tests/e2e/playwright.config.ts
pwa-installable.spec.ts --project=chromium` en 4 ejecuciones consecutivas: **3/3 en las
cuatro**. No quedan issues pendientes en este spec.

### 7.10 `api-health.spec.ts` — el contenedor `rushorder_api` sigue sin arrancar (`ECONNRESET`)

Verificado que el contenedor **sigue roto de la misma forma** que durante la consolidación de
e2e (commit `3f49698`) — no se arregló en ningún otro punto de por medio. El contenedor
`rushorder_api` (definido en `infrastructure/docker/docker-compose.yml`, arranca con
`dotnet watch`) ya estaba levantado; su log real (`docker logs rushorder_api --tail 5`) es:

```
/app/src/RushOrder.Domain/Common/IDomainEvent.cs(5,33): error CS0246: The type or namespace name 'INotification' could not be found (are you missing a using directive or an assembly reference?) [/app/src/RushOrder.Domain/RushOrder.Domain.csproj]
dotnet watch ⏳ Waiting for a file to change before restarting dotnet...
```

Con el contenedor en ese estado, `npx playwright test --config=tests/e2e/playwright.config.ts
api-health.spec.ts` falla las 2 pruebas con `apiRequestContext.get: read ECONNRESET` contra
`http://localhost:5000` — el proceso dentro del contenedor nunca llega a escuchar en el puerto
porque el build previo a `dotnet watch` no compila.

Causa localizada con precisión: el volumen anónimo `obj` de `RushOrder.Domain` dentro del
contenedor (el código fuente se monta en vivo desde el host vía
`../../backend/src:/app/src`, pero `/app/src/RushOrder.Domain/obj` es un volumen anónimo
aislado del host — ver la sección `api.volumes` en `docker-compose.yml`) tiene cacheado un
`project.assets.json` que resuelve `MediatR` a la versión **0.1.0** — un paquete placeholder
histórico sin un `INotification` utilizable — mientras que `Directory.Packages.props` fija
`MediatR` en `12.5.0`. Esa versión correcta **sí** está descargada en el caché global de NuGet
del contenedor (`/root/.nuget/packages/mediatr/12.5.0`, comprobado con `docker exec`), pero el
volumen `obj` nunca se volvió a restaurar contra ella tras la migración a Central Package
Management: `dotnet watch` recompila en cada cambio de archivo, pero no fuerza un
`dotnet restore`, así que el volumen se quedó apuntando indefinidamente a la resolución vieja.

No se aplicó ningún cambio para esta entrada — solo se investigó lo necesario para describir el
fallo con precisión. El arreglo más probable sería invalidar ese volumen (`docker compose down
-v` seguido de `docker compose up --build`, o un `dotnet restore` forzado dentro del contenedor),
pero queda fuera del alcance de esta tarea de documentación.

---

## 8. Checklist rápido

- [ ] `docker compose` arriba (Postgres/Redis/pgAdmin/MailHog)
- [ ] Backend arranca, aplica migraciones y siembra datos sin errores
- [ ] `curl http://localhost:5000/health` → 200
- [ ] Login con `owner@demo.com` devuelve un `accessToken`
- [ ] PWA carga el menú demo (con el `QrCode` correcto, no el `restaurantId`)
- [ ] Se puede completar un pedido de principio a fin desde la PWA
- [ ] El pedido aparece en tiempo real en el Kanban del escritorio
- [ ] Cambiar el estado del pedido en el escritorio se refleja en el tracking de la PWA
- [ ] `dotnet test` pasa en `Domain.Tests` y `Application.Tests`
- [ ] `dotnet test` pasa en `API.IntegrationTests` (con Docker arriba)
- [ ] `npm run test:ci` pasa en `pwa/`
- [ ] Previsión de demanda y Panel IA cargan sin error en el escritorio
