# Runbook: Errores de conexión a base de datos

**Trigger:** Logs con `NpgsqlException` / `connection pool exhausted` / health check `postgres: Unhealthy`  
**Severidad:** Crítica (P0)  
**Tiempo objetivo de resolución:** 20 min

---

## Síntomas

- Health check `/health` devuelve el check `"postgres"` en estado `"Unhealthy"` dentro de `checks[]`
  (no hay un campo `"database"` a nivel raíz — filtrar con `jq '.checks[] | select(.name=="postgres")'`)
- Logs con `Npgsql.NpgsqlException: Failed to connect`
- Latencia de queries > 5s en Application Insights
- Alert: PostgreSQL CPU > 90%

---

## Diagnóstico (< 5 min)

### 1. Verificar estado de PostgreSQL Flexible Server

```bash
az postgres flexible-server show \
  --name <PG_SERVER_NAME> \
  --resource-group <RG> \
  --query "{state:state, availabilityZone:availabilityZone}"
```

### 2. Revisar métricas en Azure Monitor

En **Azure Portal → PostgreSQL → Monitoring → Metrics**:
- `cpu_percent` — si > 90%, problema de carga
- `connections_failed` — conexiones rechazadas
- `active_connections` — comparar con `max_connections`

```bash
# Ver max_connections del servidor
az postgres flexible-server parameter show \
  --name max_connections \
  --server-name <PG_SERVER_NAME> \
  --resource-group <RG>
```

### 3. Consulta KQL en Log Analytics

```kusto
-- Errores de BD en los últimos 30 min
exceptions
| where timestamp > ago(30m)
| where type contains "Npgsql" or type contains "DbException"
| summarize count() by type, outerMessage
| order by count_ desc

-- Latencia de queries
dependencies
| where timestamp > ago(30m)
| where type == "SQL"
| summarize
    avg_ms = avg(duration),
    p95_ms = percentile(duration, 95),
    p99_ms = percentile(duration, 99)
  by name
| order by p99_ms desc
```

---

## Árbol de decisión

```
Error de conexión a BD
│
├─ ¿Servidor PostgreSQL "Ready"?
│   ├─ NO → Esperar recuperación automática (HA failover ~60s)
│   │        Si no recupera en 5 min → escalar a Azure Support
│   └─ SÍ → siguiente check
│
├─ ¿Connection pool exhausted?
│   ├─ SÍ → Reiniciar app (libera conexiones):
│   │        az webapp restart --name <APP> --resource-group <RG>
│   │        Revisar MaxPoolSize en connection string
│   └─ NO → siguiente check
│
├─ ¿CPU > 90% sostenido?
│   ├─ SÍ → Escalar servidor (ver sección Scale Up)
│   │        Identificar query costosa (pg_stat_activity)
│   └─ NO → siguiente check
│
├─ ¿Cambio reciente en connection string / credenciales?
│   ├─ SÍ → Verificar Key Vault secret + App Service env var
│   └─ NO → siguiente check
│
└─ ¿Red / Private Link?
    └─ Verificar NSG rules y Private DNS zone
```

---

## Acciones correctivas

### Pool agotado

```bash
# Reiniciar la aplicación (libera conexiones huérfanas)
az webapp restart --name <APP_NAME> --resource-group <RG>
```

En connection string, verificar `Maximum Pool Size`:
```
...;Maximum Pool Size=50;Connection Idle Lifetime=300;...
```

### CPU alto — identificar queries lentas

El servidor está en una subnet delegada sin acceso público (`delegated_subnet_id` +
Private DNS zone en `infrastructure/terraform/shared/main.tf`) — no hay Azure Bastion
ni jump host provisionado todavía en el Terraform actual, así que conectarte con `psql`
requiere estar en la VNet: Azure Cloud Shell con VNet integration configurada contra la
subnet, una VPN punto a sitio, o un Bastion/jump host que haya que levantar ad-hoc. Si
ninguna de esas opciones existe cuando la necesites, es un hueco de infraestructura a
resolver antes de que este runbook sea 100% ejecutable en un incidente real.

```sql
-- Queries activas ahora mismo
SELECT pid, now() - pg_stat_activity.query_start AS duration, query, state
FROM pg_stat_activity
WHERE (now() - pg_stat_activity.query_start) > interval '5 seconds'
ORDER BY duration DESC;

-- Matar query lenta específica
SELECT pg_cancel_backend(<pid>);

-- Forzar desconexión (último recurso)
SELECT pg_terminate_backend(<pid>);
```

### Scale up PostgreSQL

```bash
# Escalar a SKU mayor (operación sin downtime en Flexible Server con HA)
az postgres flexible-server update \
  --name <PG_SERVER_NAME> \
  --resource-group <RG> \
  --sku-name Standard_D8s_v3
```

### HA Failover manual

Si la réplica primaria está degradada:
```bash
az postgres flexible-server restart \
  --name <PG_SERVER_NAME> \
  --resource-group <RG> \
  --failover Forced
```

---

## Verificación de red

```bash
# Verificar Private DNS zone
az network private-dns record-set a list \
  --resource-group <RG> \
  --zone-name privatelink.postgres.database.azure.com

# Verificar NSG rules del subnet de PostgreSQL
az network nsg rule list \
  --nsg-name <NSG_NAME> \
  --resource-group <RG> \
  --output table
```

---

## Verificación tras la acción

```bash
curl -f https://<PROD_URL>/health | jq '.checks[] | select(.name == "postgres")'
```
- [ ] El check de base de datos en `/health` vuelve a `Healthy`
- [ ] `active_connections` está por debajo de `max_connections` con margen
- [ ] No hay nuevas excepciones `Npgsql`/`DbException` en Application Insights en los últimos 5 min

---

## Post-incidente

1. Revisar `max_connections` y ajustar si es necesario
2. Comprobar si hay queries sin índice (EXPLAIN ANALYZE)
3. Considerar PgBouncer si el pool exhaustion es recurrente
4. Revisar alertas: ¿umbral de 90% CPU era correcto?
