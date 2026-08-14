# Disaster Recovery Plan — Rush Order SaaS

**RTO**: 4 horas | **RPO**: 5 minutos  
**Última revisión**: 2026-08-14  
**Propietario**: Equipo de Ingeniería

---

## Resumen ejecutivo

| Escenario | RTO estimado | Acción principal |
|-----------|-------------|-----------------|
| API down (contenedor crash) | 5-15 min | Auto-restart → swap de slot |
| Base de datos inaccesible | 0-60 min | HA auto-failover → PITR si corrupción |
| Fallo catastrófico de región | 2-4 horas | Promote réplica + failover App Service |
| Secreto comprometido | 30 min | Rotación manual + invalidación de sesiones |

---

## Escenario 1 — API down (App Service crash)

### Detección
- Alert: Disponibilidad < 99% (Azure Monitor, 5 min)
- Slack: #alerts con severity crítica

### Procedimiento

```
Paso 1 — Verificar estado
az webapp show --name <APP> --resource-group <RG> --query "state"

Paso 2 — Revisar logs
az webapp log tail --name <APP> --resource-group <RG>

Paso 3 — ¿Reinicio automático en curso?
  SÍ → Esperar 5 min. Azure reinicia el contenedor automáticamente.
  NO → Continuar

Paso 4 — Reinicio manual
az webapp restart --name <APP> --resource-group <RG>

Paso 5 — Si no se recupera en 5 min: swap al slot de staging (versión anterior)
az webapp deployment slot swap \
  --name <APP> --resource-group <RG> \
  --slot staging --target-slot production

Paso 6 — Si el slot tampoco funciona: deploy manual desde GHCR
az webapp config container set \
  --name <APP> --resource-group <RG> \
  --docker-custom-image-name ghcr.io/<owner>/rush-order-api:<last-good-sha>
az webapp restart --name <APP> --resource-group <RG>
```

### Verificación
```bash
curl -f https://<PROD_URL>/health | jq .status
```

---

## Escenario 2 — Base de datos inaccesible

### Detección
- Alert: PostgreSQL CPU > 90% o `/health` con el check `postgres` en estado `"Unhealthy"`
- Logs: `NpgsqlException` en Application Insights

### Sub-escenario 2a — Fallo de instancia (HA automático)

Azure PostgreSQL Flexible Server con `high_availability_mode = "ZoneRedundant"` hace failover automático al standby en ~60 segundos. **No se requiere acción manual.**

Verificar:
```bash
az postgres flexible-server show \
  --name <PG_SERVER> --resource-group <RG> \
  --query "{state:state, haState:highAvailability.state}"
```

### Sub-escenario 2b — Modo degradado (API continúa operando)

Mientras la BD no responde, la API sirve datos desde caché Redis (solo lectura):
- Los endpoints GET del menú funcionan desde caché
- Las escrituras (nuevos pedidos) fallan con 503 graceful
- El KDS muestra el estado cacheado

### Sub-escenario 2c — Corrupción de datos: Point-in-Time Recovery

> ⚠️ PITR crea un **nuevo servidor**. Luego hay que actualizar las connection strings.

```bash
# 1. Identificar el timestamp de recovery (5-min granularity)
RECOVERY_TIME="2026-06-22T14:30:00Z"

# 2. Crear servidor restaurado
az postgres flexible-server restore \
  --name <RESTORED_SERVER_NAME> \
  --resource-group <RG> \
  --source-server <ORIGINAL_SERVER_NAME> \
  --restore-time "${RECOVERY_TIME}"

# 3. Verificar datos en servidor restaurado (conectar con psql)
# 4. Actualizar secret en Key Vault con nueva connection string
az keyvault secret set \
  --vault-name <KV_NAME> \
  --name "ConnectionStringsDefaultConnection" \
  --value "Host=<new-server-fqdn>;Database=rushorder;..."

# 5. Reiniciar App Service para que tome la nueva connection string
az webapp restart --name <APP> --resource-group <RG>
```

**Tiempo estimado**: 30-90 minutos dependiendo del tamaño de BD.

---

## Escenario 3 — Fallo catastrófico de región (North Europe)

> Activar solo si Azure confirma un fallo de región completo.

### Pre-requisitos
- Read replica en West Europe configurada (módulo Terraform `enable_read_replica = true` en `infrastructure/terraform/environments/prod/main.tf`)
- App Service principal en North Europe (`var.location = "northeurope"`), detrás de Azure Front Door (`azurerm_cdn_frontdoor_profile.this` — perfil global, no atado a una región)

> ⚠️ **No existe un `environments/dr/` en Terraform ni un App Service de failover pre-creado en West Europe.**
> Solo la réplica de PostgreSQL está pre-provisionada; el App Service de West Europe hay
> que crearlo en el momento (Paso 4). Si se decide que este escenario debe tener un App
> Service de pie permanentemente, es trabajo de infraestructura pendiente — no lo des por
> hecho durante el incidente.

### Procedimiento

```
Paso 1 — Confirmar fallo de región con Azure Status (status.azure.com)

Paso 2 — Promote réplica a primary
az postgres flexible-server replica promote \
  --name <REPLICA_SERVER_NAME> \
  --resource-group <RG>
  # ⚠️ Tarda ~5-10 min. La réplica se convierte en servidor independiente.

Paso 3 — Actualizar connection string en Key Vault (West Europe)
az keyvault secret set \
  --vault-name <KV_NAME_WEST> \
  --name "ConnectionStringsDefaultConnection" \
  --value "Host=<replica-fqdn>;Database=rushorder;..."

Paso 4 — Crear App Service en West Europe
# No hay Terraform de DR pre-preparado (ver aviso arriba) — deploy manual de la imagen
# desde GHCR sobre un App Service creado ad-hoc en esa región:
az webapp config container set \
  --name <APP_WEST> --resource-group <RG_WEST> \
  --docker-custom-image-name ghcr.io/<owner>/rush-order-api:<last-stable-sha>

Paso 5 — Repuntar el origen de Azure Front Door al App Service de West Europe
# api.rushorder.es apunta a Azure Front Door (azurerm_cdn_frontdoor_endpoint), no
# directamente al App Service — Front Door es un servicio global (anycast), así que un
# fallo de región NO lo tumba a él. La recuperación es repuntar el origen, no el DNS:
az afd origin update \
  --profile-name rush-order-prod-afd \
  --origin-group-name app-service \
  --origin-name app-service-origin \
  --resource-group <RG> \
  --host-name <app-west-europe-hostname>
# (o el equivalente en Terraform: actualizar `host_name`/`origin_host_header` en
# azurerm_cdn_frontdoor_origin.app y aplicar)

Paso 6 — Verificar
curl -f https://api.rushorder.es/health | jq .
```

**Tiempo estimado**: 2-4 horas (incluye creación del App Service y validaciones; al no haber
DNS de por medio la propagación de Front Door es más rápida que un cambio de DNS clásico).

### Post-recovery
1. Documentar el incidente (timeline, causa raíz, impacto)
2. Restaurar a North Europe cuando se recupere la región
3. Verificar que los datos escritos durante el fallo se sincronizaron
4. Re-configurar réplica en la región original

---

## Escenario 4 — Secreto comprometido

> No existe un endpoint de admin para revocar sesiones en bloque (`/api/v1/admin/revoke-all-sessions`
> no está implementado — el único endpoint de logout revoca la sesión de quien lo llama,
> `POST /api/v1/auth/logout`). El mecanismo real para invalidar de golpe todos los access
> tokens JWT ya emitidos es rotar el par de claves RSA: `rotate-secrets.sh` reinicia el
> App Service, que carga la nueva clave pública, así que cualquier JWT firmado con la clave
> privada anterior deja de validar inmediatamente (ver el comentario del propio script:
> "Existing sessions using old JWTs will fail after restart").

```bash
# 1. Rotar JWT keys inmediatamente — invalida todos los access tokens ya emitidos
KEY_VAULT_NAME=<KV> APP_SERVICE_NAME=<APP> RESOURCE_GROUP=<RG> \
  ./scripts/rotate-secrets.sh
# El script pide confirmación antes de reiniciar el App Service (ver su salida).

# 2. Si el secreto comprometido incluye refresh tokens (no solo JWTs), revocarlos también:
# no hay endpoint ni script para revocación masiva por tenant/global — IRefreshTokenRepository
# solo expone RevokeAllForUserAsync(userId), pensado para reseteo de contraseña individual.
# Para un incidente que afecte a muchos usuarios, revocar directamente en BD
# (tabla/columnas confirmadas contra RefreshTokenConfiguration.cs — sin snake_case,
# EF las mapea con su nombre C# tal cual, entre comillas dobles):
psql "$PGCONN" -c 'UPDATE refresh_tokens SET "IsRevoked" = true, "UpdatedAt" = now() WHERE "IsRevoked" = false;'

# 3. Si es la clave de Stripe: revocar desde el dashboard de Stripe y actualizar KV
az keyvault secret set --vault-name <KV> --name StripeKey --value <new-key>
az webapp restart --name <APP> --resource-group <RG>

# 4. Notificar a los usuarios si hubo exposición de datos
# 5. Abrir incidente de seguridad en GitHub
```

### Verificación
- [ ] Un JWT emitido antes de la rotación devuelve HTTP 401 en cualquier endpoint autenticado
- [ ] Un login nuevo funciona correctamente con el par de claves rotado
- [ ] Si se revocaron refresh tokens: `POST /api/v1/auth/refresh` con un token antiguo devuelve HTTP 422
- [ ] Si se rotó la clave de Stripe: un webhook o cobro de prueba usa la clave nueva sin error

### Seguimiento
1. Confirmar el alcance real de la exposición (qué secreto, desde cuándo, quién tuvo acceso)
2. Documentar el incidente (timeline, causa raíz, impacto) y abrir postmortem dentro de 48h
3. Si hubo exposición de datos de usuarios, evaluar obligación de notificación (RGPD)

---

## Backups automáticos y restauración manual

El backup **no** se dispara antes de cada release — corre por su cuenta cada noche a las
02:00 UTC vía `.github/workflows/backup-postgresql.yml` (cron `0 2 * * *`), con
`workflow_dispatch` disponible para lanzarlo a mano contra `production` o `staging`.

```bash
# Manual, sin pasar por GitHub Actions:
ENVIRONMENT=production \
PGHOST=<host> PGUSER=rushorder PGPASSWORD=<pass> PGDATABASE=rushorder \
AZURE_STORAGE_ACCOUNT=rushorderprodbkp \
./scripts/backup-postgresql.sh
```

> `rushorderprodbkp` es la storage account **dedicada a backups** (GRS, ver
> `infrastructure/terraform/modules/storage/main.tf`) — deliberadamente separada de
> `rushorderprodsa` (la de imágenes de producto/recibos, solo LRS) para que la
> replicación geográfica de los backups no encarezca esa otra cuenta. Solo la cuenta
> `*bkp` tiene la política de retención de 6 meses aplicada.

Backups almacenados en: `<storage-account> / backups / postgresql / YYYY-MM-DD-HH-MM / production.dump`
(más un `.sha256` junto a cada dump)  
Retención: 6 meses / 180 días (policy de lifecycle en Terraform, `delete-after-6-months`)

### Restauración desde backup manual

```bash
# 1. Descargar el dump y su checksum
az storage blob download \
  --account-name rushorderprodbkp \
  --container-name backups \
  --name "postgresql/2026-06-01-02-00/production.dump" \
  --file /tmp/production.dump \
  --auth-mode login

az storage blob download \
  --account-name rushorderprodbkp \
  --container-name backups \
  --name "postgresql/2026-06-01-02-00/production.dump.sha256" \
  --file /tmp/production.dump.sha256 \
  --auth-mode login

# 2. Verificar checksum
sha256sum -c /tmp/production.dump.sha256

# 3. Restaurar
pg_restore \
  -h <PGHOST> -U rushorder -d rushorder \
  -Fc -v --clean --if-exists \
  /tmp/production.dump
```

---

## Checklists de validación post-recovery

```
[ ] API responde en /health con status "Healthy"
[ ] /health/detailed muestra todos los componentes en "Healthy"
[ ] Puede autenticarse un usuario de prueba
[ ] Puede crearse un pedido de prueba
[ ] SignalR conecta correctamente
[ ] Stripe puede procesar un pago de prueba (modo test)
[ ] Application Insights recibe telemetría
[ ] No hay errores 5xx en los últimos 5 minutos
```

---

## Contactos de escalado

| Nivel | Contacto | Canal |
|-------|---------|-------|
| L1 — On-call | Equipo de ingeniería | Slack #incidents |
| L2 — Azure Support | Ticket de soporte | portal.azure.com |
| L3 — Stripe Support | api-support@stripe.com | Dashboard Stripe |
