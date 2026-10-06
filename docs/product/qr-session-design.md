# Diseño: sesión anónima QR → menú → pedido

Estado actual: **no implementado**. Este documento describe el diseño antes de
tocar código. Ámbito: sustituir la resolución directa `QR → TablePublicDto`
por un flujo `escaneo → token de sesión firmado → menú → pedido validado
contra ese token`.

## 0. Estado real del código (punto de partida)

- `GET /api/v1/qr/{qrCode}` (`QrController.cs` → `GetTableByQrCodeQueryHandler`)
  resuelve el QR a `TablePublicDto` y no emite ningún credential. Cualquiera
  que conozca o adivine un `qrCode` puede llamarlo.
- `POST /api/v1/orders` (`CreateOrder`) está marcado `[AllowAnonymous]` con un
  comentario que dice *"QR-sourced orders don't require auth"*. Pero
  `CreateOrderCommandHandler` exige `_tenantService.TenantId` (línea 85-86) y
  lanza `UnauthorizedAccessException` si es `null`. `ICurrentTenantService`
  sólo rellena `TenantId` a partir del claim `"tid"` de un JWT autenticado
  (`CurrentTenantService.cs`). Resultado: **hoy un cliente anónimo real no
  puede completar un pedido** — el test de integración
  `CreateOrder_WithoutAuth_Returns401` lo confirma explícitamente
  (`OrderTests.cs:37-54`, comentario: *"Handler requires TenantId from
  JWT — anonymous request → UnauthorizedAccessException → 401"*). El único
  camino que funciona hoy es el de staff autenticado
  (`CreateOrder_AsOwner_WithQrSource_Returns201`). El flujo QR→pedido está
  roto para clientes reales, no sólo sin validar.
- `POST /orders/{id}/items` (`AddItem`) y `GET /orders/{id}` (`GetById`) no
  tienen `[Authorize]` ni `[AllowAnonymous]` propios — pero **sí exigen
  autenticación hoy**, heredada: `ApiController.cs:12-13` declara
  `[Authorize]` a nivel de clase, y `[AllowAnonymous]` en una acción concreta
  es lo único que lo desactiva. Como ninguna de las dos acciones lo tiene,
  ambas requieren *algún* JWT válido (de cualquier rol). Corrección respecto
  a una versión anterior de este documento, que las describía como
  "abiertas por omisión" — no lo están a nivel de autenticación. Lo que sí
  falta es *ownership*: cualquier JWT válido del mismo tenant (staff, o tras
  este diseño, cualquier sesión QR de cualquier mesa de ese tenant) puede
  añadir ítems a o leer *cualquier* pedido de ese tenant, no sólo el suyo —
  el filtro de tenant (`AppDbContext.cs:55`, `HasQueryFilter` sobre
  `Order.TenantId`) ya impide el acceso *entre* tenants, pero no dentro del
  mismo tenant.
- `POST /orders/{id}/rating` (`RateOrder`) es `[AllowAnonymous]` sin
  comprobar que quien puntúa sea quien hizo el pedido.
- `POST /orders/{id}/cancel` (`Cancel`) es
  `[Authorize(Roles = "Admin,Owner,Manager")]` — **solo staff**. No existe
  hoy una vía anónima para que un cliente cancele su propio pedido. (Decisión
  ya confirmada contigo: fuera de alcance de este diseño — ver §6.)
- Ya existe un mecanismo de verificación por pedido, sin usar:
  `IOrderVerificationService` / `OrderVerificationService.cs` genera un HMAC
  (`trackingToken`) por `orderId` y `CreateOrderResult` ya lo devuelve al
  cliente (`CreateOrderCommand.cs:137-138`). Nadie lo verifica en ningún
  controller, y el PWA no lo guarda ni lo reenvía (comprobado: cero
  referencias a `trackingToken` en `pwa/src`).
- La infraestructura JWT (`FileRsaKeyProvider.cs`, `JwtTokenService.cs`) usa
  RSA-2048 + `RS256`, con `Issuer`/`Audience` fijos y un único esquema
  `JwtBearer` registrado (`DependencyInjection.cs:166-186`). `MapInboundClaims
  = false` es deliberado — sin eso, `"tid"` se remapea a una claim URI legacy
  y `CurrentTenantService` deja de encontrarlo (comentario explícito en el
  código). Cualquier token nuevo debe respetar esto.
- `Table` y `Restaurant` heredan de `TenantEntity`, que ya expone `TenantId`
  (`TenantEntity.cs`). O sea: al resolver una mesa por QR ya tenemos
  `table.TenantId`, `table.Id` y `table.RestaurantId` disponibles sin
  consultas adicionales.
- El PWA guarda `tableId`/`restaurantId` en **`sessionStorage`**, no
  `localStorage` (`cartStore.ts:96-97`, `createJSONStorage(() =>
  sessionStorage)`). Esto cambia la respuesta de compatibilidad hacia atrás
  (§5).
- La ruta del PWA es `/menu/:qrToken` (`routes.tsx:28`) y `LandingPage`
  permanece montada en esa URL durante toda la sesión de navegación — el
  `qrToken` original sigue disponible vía `useParams` en todo momento, no
  sólo en el primer render (`LandingPage.tsx:25`, `useQRMenu.ts`). Esto
  importa para el re-intento silencioso en §4.

## 1. Forma del JWT de sesión

Nuevo tipo de token, generado por un método nuevo en el `IJwtTokenService`
existente (`GenerateQrSessionToken(Guid tableId, Guid restaurantId, Guid
tenantId)`), reutilizando `IRsaKeyProvider`/`FileRsaKeyProvider` tal cual —
misma clave RSA-2048, mismo algoritmo `RS256`, mismo `Issuer`/`Audience` que
los tokens de staff. No se monta una segunda infraestructura de firma: es
literalmente un tercer método en `JwtTokenService.cs` junto a
`GenerateAccessToken` y `GenerateImpersonationToken`, siguiendo el mismo
patrón (`JwtSecurityToken` + `SigningCredentials` con la misma clave).

Claims:

| Claim | Valor | Por qué |
|---|---|---|
| `tid` | `table.TenantId` | Reutiliza el contrato existente de `CurrentTenantService`, que ya lee `"tid"` sin fallback. Con esto, `CreateOrderCommandHandler` obtiene `TenantId` **gratis**, sin tocar `CurrentTenantService`. |
| `table_id` | `table.Id` | Nueva claim custom. Es el dato que los endpoints deben validar contra lo que envía el cliente. |
| `restaurant_id` | `table.RestaurantId` | Nueva claim custom. Evita una consulta extra a `Table` sólo para saber el restaurante en logging/handlers. |
| `token_use` | `"qr_session"` | Marca el tipo de token. Un token de staff nunca lo lleva; un token QR nunca lleva `ClaimTypes.Role`. Esto es lo que separa ambos mundos sin necesitar un segundo `Audience` ni un segundo esquema `JwtBearer` — un `[Authorize(Roles = "...")]` ya falla automáticamente contra un token QR porque no hay claim de rol, y las nuevas comprobaciones de "requiere sesión QR" comprueban `token_use` explícitamente. |
| `jti` | `Guid` nuevo | Igual que los tokens de staff — sólo para logging/correlación, no hay lista de revocación (ver justificación de TTL corto más abajo). |
| `iat` / `exp` | estándar | — |

Explícitamente **sin**: `sub`, `email`, ningún identificador de persona o
dispositivo, ningún `ClaimTypes.Role`. El token no representa a un usuario;
representa "este cliente demostró haber resuelto el QR de esta mesa".

**Tiempo de vida: 3 horas.** Justificación: es un techo absoluto de sesión,
no un timeout de inactividad (eso ya lo cubre `useQRSession.ts` en el
cliente, ver §3). Una comida larga con sobremesa puede superar fácilmente
1-2h; forzar re-emisión a los 30-60 min interrumpiría pedidos de una segunda
ronda o postre pedidos tarde. 3h cubre el caso extremo razonable (grupo
grande, servicio lento) sin dejar el token vivo indefinidamente si alguien
se lo lleva fuera del restaurante. No hay lista de revocación por diseño —
con un TTL de horas (no días) el coste de no poder revocar individualmente
es aceptable; ver §5 para el caso en que esto sí importa (regeneración de QR
por abuso).

## 2. Endpoints que deben exigir el token

Repaso completo de `OrdersController.cs` con foco en los que reciben
`TableId`/`OrderId` del cliente sin más validación hoy:

| Endpoint | Estado hoy | Cambio propuesto |
|---|---|---|
| `POST /orders` (CreateOrder) | `[AllowAnonymous]`, roto para clientes reales (§0) | Mantiene `[AllowAnonymous]` a nivel de atributo (staff también lo usa, ver `CreateOrder_AsOwner_WithQrSource_Returns201`) — pero `[AllowAnonymous]` sólo salta la *autorización*, no la *autenticación*: si llega un `Authorization: Bearer`, `HttpContext.User` igual se rellena. La validación real pasa a `CreateOrderCommandHandler`: si el principal tiene rol de staff, comportamiento actual sin cambios; si no, debe llevar `token_use=qr_session` y su claim `table_id` debe coincidir exactamente con `request.TableId` — si no coincide o no hay token, 401/403. Esto además **arregla** el bug de §0: el token QR aporta `tid`, así que `CurrentTenantService.TenantId` deja de ser `null` para pedidos anónimos reales. |
| `POST /orders/{id}/items` (AddItem) | Sin atributo propio — hereda `[Authorize]` de `ApiController`, exige algún JWT válido pero sin ownership | **Sin cambio de atributo** (ya está correctamente gateado a nivel de autenticación; añadir `[AllowAnonymous]` aquí sería un retroceso real, no cosmético). Se añade la comprobación de ownership en `AddItemToOrderCommandHandler`: staff, o `token_use=qr_session` con `table_id` == `TableId` del **pedido cargado** (hay que cargar el `Order` para conocer su mesa; la ruta sólo trae `orderId`). |
| `GET /orders/{id}` (GetById) | Sin atributo propio — hereda `[Authorize]` de `ApiController` | Sin cambio de atributo. Se añade la misma comprobación de ownership que en `AddItem`: staff, o `token_use=qr_session` con `table_id` == `TableId` del pedido cargado. |
| `POST /orders/{id}/rating` (RateOrder) | `[AllowAnonymous]`, sin ownership check | **Se retira `[AllowAnonymous]`** — pasa a heredar `[Authorize]` de `ApiController` como `GetById`, con la misma comprobación de ownership (staff o `token_use=qr_session` con `table_id` coincidente). |
| `PATCH /{id}/status`, `DELETE /{id}/items/{itemId}`, `POST /{id}/cancel` | Ya `[Authorize(Roles = "...")]` staff-only | Sin cambios — fuera de alcance (§6). |

**Corrección respecto a una versión anterior de este documento**, que
proponía gatear `GetById`/`RateOrder` con el `trackingToken` existente
(`IOrderVerificationService`) en vez del JWT de sesión QR, razonando que son
una fase distinta del flujo (seguimiento post-pedido) con un ciclo de vida
propio. Se descubrió durante la planificación de implementación que esa vía
no es viable sin tocar seguridad a nivel de base de datos: `orders` tiene
Row-Level Security de Postgres (`001_row_level_security.sql`), no sólo un
`HasQueryFilter` de EF —

```sql
CREATE POLICY tenant_isolation ON orders
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
```

— y `TenantDbCommandInterceptor` fija esa variable de sesión a
`ICurrentTenantService.TenantId ?? Guid.Empty`. Una petición sólo con
`trackingToken` (sin ningún JWT) no tiene `tid`, así que la variable de
sesión queda en `Guid.Empty` y RLS devuelve cero filas para el pedido real,
sin importar si el `trackingToken` es válido — `IgnoreQueryFilters()` de EF
no sirve aquí porque RLS se aplica en la base de datos, no en el SQL que
genera EF. Arreglarlo habría exigido una política RLS nueva y una señal
mutable por-request para "este pedido concreto fue verificado
criptográficamente" — tocar un control de seguridad (RLS) sólo para esto.

En su lugar, `GetById`/`RateOrder` reutilizan el mismo `token_use=qr_session`
que `CreateOrder`/`AddItem`: el claim `tid` ya fluye por el mecanismo
existente sin ningún cambio en RLS/DB, exactamente igual que para esos dos
endpoints. El coste es que consultar el estado del pedido vuelve a estar
sujeto al techo de 3h del JWT — mitigado por el mismo remint silencioso vía
`GET /qr/{qrToken}` ya descrito en §3, que ahora también cubre este caso.
`trackingToken`/`IOrderVerificationService` quedan sin usar — ya lo estaban
antes de este diseño (§0) — y siguen fuera de alcance (§6).

## 3. Expiración a mitad de sesión

Dos mecanismos independientes, con ejes distintos, que **no se alinean**:

- **`useQRSession.ts` (cliente, ya existe)**: timer de *inactividad*
  (25 min aviso / 30 min expiración), que se resetea con cualquier
  interacción (`click`/`touchstart`/`scroll`/`keydown`). No llama a ningún
  endpoint — hoy es puramente local. Resuelve "el cliente dejó de
  interactuar", no "el credential caducó".
- **JWT de sesión QR (nuevo, backend)**: techo *absoluto* de 3h,
  independiente de la actividad. Resuelve "este credential lleva demasiado
  tiempo vivo", incluyendo el caso de que alguien se lo lleve fuera del
  local o lo reutilice al día siguiente.

No tiene sentido igualarlos: si los unificara al valor más corto (30 min),
un cliente activo pidiendo una segunda ronda a los 40 min se quedaría sin
poder pedir aunque haya estado interactuando todo el rato. Si los igualara
al más largo (3h) para el timer de inactividad, perdería su propósito de UX
(avisar cuando alguien se fue sin cerrar la pestaña).

**Qué pasa exactamente cuando el JWT expira a mitad de sesión:**

1. El carrito **no se pierde** — vive en el store de Zustand
   (`cartStore.ts`), que es un estado de cliente completamente separado del
   token. Perder el token sólo bloquea *nuevas escrituras* (`CreateOrder`,
   `AddItem`); no borra `items`/`round` en memoria/`sessionStorage`.
2. La siguiente llamada a `CreateOrder`/`AddItem` con el token caducado
   devuelve 401 (rechazado por el `token_use=qr_session` + `table_id`
   check, o por `ValidateLifetime=true` del propio `JwtBearer`).
3. El PWA, al recibir ese 401 en esas dos llamadas específicas, **no obliga
   a re-escanear el QR físico**: vuelve a llamar
   `GET /api/v1/qr/{qrToken}` usando el mismo `qrToken` que ya tiene
   disponible en la URL (`/menu/:qrToken`, montado durante toda la sesión —
   §0), obtiene un JWT de sesión nuevo de forma transparente, y reintenta la
   acción que falló. El carrito sigue intacto porque nunca se tocó.
4. Sólo se fuerza un re-escaneo real cuando ese mismo `GET /qr/{qrToken}`
   devuelve 404 — lo que ocurre si, mientras tanto, se regeneró el QR físico
   de esa mesa (§5, `RegenerateQrCommand`), no por la mera expiración del
   JWT.

Esto convierte la expiración del JWT en un detalle de bajo nivel manejado
por un interceptor HTTP del PWA (reintento con re-mint), no en un evento que
el usuario perciba — salvo que además haya estado inactivo 30 min, en cuyo
caso `useQRSession.ts` ya le muestra su propio aviso/expiración
independientemente de si el JWT sigue vivo o no.

## 4. Compatibilidad hacia atrás

El enunciado original asumía `localStorage`; el código real usa
`sessionStorage` (`cartStore.ts:97`). Esto cambia el problema: no hay un
`tableId` de una versión antigua sobreviviendo días o semanas en el
navegador — como mucho sobrevive mientras la pestaña siga abierta. El caso
real a cubrir es más estrecho: una pestaña que quedó abierta con el PWA
antiguo (sin lógica de token) justo cuando se despliega esta feature.

Como frontend y backend se despliegan juntos, ese caso se resuelve solo con
el mismo mecanismo del §3: un cliente sin token que intenta `CreateOrder`/
`AddItem` recibe 401 exactamente igual que un cliente con token caducado, y
el mismo interceptor de re-mint via `GET /qr/{qrToken}` lo resuelve sin
código especial de "migración de clientes antiguos" — "sin token" y "token
caducado" son, a efectos del cliente, el mismo caso: pedir uno nuevo antes
de reintentar.

## 5. Impacto en `RegenerateQrCommand`

**Decisión: son independientes.** Regenerar el QR físico (`SetQrCode` en
`Table`, `RegenerateQrCommand.cs:56`) no invalida las sesiones activas de
esa mesa. Razón principal: el JWT de sesión no contiene el string del QR en
ningún claim — sólo `table_id`/`restaurant_id`/`tid`, que no cambian al
regenerar. Es decir, técnicamente *no podría* invalidarlas sin mecanismo
adicional (ver limitación abajo), pero además **no conviene** que lo haga en
el caso de uso normal: regenerar el QR (rotación rutinaria, sticker
dañado/sustituido) no debería expulsar a mitad de comida a un cliente que ya
está sentado y pidiendo. El QR físico es la puerta de entrada; la sesión ya
concedida es un asunto aparte, tal como lo planteaba el enunciado.

**Limitación aceptada para v1**: si la razón de regenerar es un caso de
abuso (alguien fotografió el QR y está pidiendo en bucle o desde fuera del
local), esta decisión significa que su JWT sigue siendo válido hasta sus 3h
de TTL — regenerar el QR no lo corta en seco. No se construye una lista de
revocación para este caso porque hoy no hay evidencia de que ocurra y
añadiría un `SELECT` extra en cada request gateado por sesión QR sólo para
cubrir un caso hipotético (YAGNI). Mitigación futura de bajo coste si hiciera
falta: guardar `Table.QrRegeneratedAt` y comparar contra el `iat` del token
en el momento de validar — se documenta aquí como opción, no se implementa
ahora.

## 6. Fuera de alcance

- **Cancelación por parte del cliente**: confirmado contigo — `Cancel` sigue
  siendo staff-only (`[Authorize(Roles = "Admin,Owner,Manager")]`). No se
  añade una vía anónima de cancelación en este diseño.
- `trackingToken`/`IOrderVerificationService`: quedan como estaban — sin
  usar. Este diseño no los conecta a nada (ver corrección en §2) ni los
  elimina; decisión de qué hacer con ese código muerto queda fuera de este
  documento.
- Lista de revocación de sesiones QR (ver limitación de §5).
