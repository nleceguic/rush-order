# Disaster Recovery Plan — Rush Order SaaS

**RTO**: 4 horas | **RPO**: 5 minutos  
**Última revisión**: 2026-06-22  
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
- Alert: PostgreSQL CPU > 90% o health check `"database": "Unhealthy"`
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
- Read replica en West Europe configurada (módulo Terraform `enable_read_replica = true`)
- App Service en North Europe (principal)

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

Paso 4 — Crear App Service en West Europe (si no existe DR app service)
# Opción A: usar Terraform pre-preparado en environments/dr/
terraform -chdir=infrastructure/terraform/environments/dr apply

# Opción B: deploy manual de la imagen desde GHCR
az webapp config container set \
  --name <APP_WEST> --resource-group <RG_WEST> \
  --docker-custom-image-name ghcr.io/<owner>/rush-order-api:<last-stable-sha>

Paso 5 — Actualizar DNS para apuntar a West Europe
# En proveedor DNS: api.rushorder.es → <app-west-europe-url>
# TTL debe ser bajo (300s) para propagación rápida

Paso 6 — Verificar
curl -f https://api.rushorder.es/health | jq .
```

**Tiempo estimado**: 2-4 horas (incluye propagación DNS y validaciones).

### Post-recovery
1. Documentar el incidente (timeline, causa raíz, impacto)
2. Restaurar a North Europe cuando se recupere la región
3. Verificar que los datos escritos durante el fallo se sincronizaron
4. Re-configurar réplica en la región original

---

## Escenario 4 — Secreto comprometido

```bash
# 1. Invalidar todas las sesiones activas (bloquear JTIs en Redis)
# Llamar al endpoint de Admin: POST /api/v1/admin/revoke-all-sessions

# 2. Rotar JWT keys inmediatamente
KEY_VAULT_NAME=<KV> APP_SERVICE_NAME=<APP> RESOURCE_GROUP=<RG> \
  ./scripts/rotate-secrets.sh

# 3. Si es la clave de Stripe: revocar desde el dashboard de Stripe y actualizar KV
az keyvault secret set --vault-name <KV> --name StripeKey --value <new-key>
az webapp restart --name <APP> --resource-group <RG>

# 4. Notificar a los usuarios si hubo exposición de datos
# 5. Abrir incidente de seguridad en GitHub
```

---

## Backups manuales pre-release

Antes de cada release, el CI/CD ejecuta automáticamente un backup:

```bash
# Manual:
ENVIRONMENT=production \
PGHOST=<host> PGUSER=rushorder PGPASSWORD=<pass> PGDATABASE=rushorder \
AZURE_STORAGE_ACCOUNT=rushorderprodsa \
./scripts/backup-postgresql.sh
```

Backups almacenados en: `Azure Blob Storage / backups / postgresql / YYYY-MM-DD-HH-MM / production.dump`  
Retención: 6 meses (policy de lifecycle en Terraform)

### Restauración desde backup manual

```bash
# 1. Descargar el dump
az storage blob download \
  --account-name rushorderprodsa \
  --container-name backups \
  --name "postgresql/2026-06-01-02-00/production.dump" \
  --file /tmp/production.dump \
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
