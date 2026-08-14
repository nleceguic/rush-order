# Runbook: Latencia alta

**Trigger:** Alerta P99 > 5s (Critical) o P95 > 2s (Warning)  
**Severidad:** Warning → Critical según umbral  
**Tiempo objetivo de diagnóstico:** 10 min

---

## Síntomas

- Application Insights: `requests | percentile(duration, 99) > 5000ms`
- Alert: "P99 response time > 5 seconds"
- Usuarios reportan lentitud en la PWA o app de escritorio

---

## Diagnóstico rápido (< 5 min)

### 1. Localizar el cuello de botella

```kusto
-- Top endpoints más lentos (últimos 15 min)
requests
| where timestamp > ago(15m)
| summarize
    count   = count(),
    p50     = percentile(duration, 50),
    p95     = percentile(duration, 95),
    p99     = percentile(duration, 99)
  by name
| order by p99 desc
| take 10

-- Dependencias lentas (BD, Redis, Service Bus)
dependencies
| where timestamp > ago(15m)
| summarize
    count   = count(),
    avg_ms  = avg(duration),
    p99_ms  = percentile(duration, 99)
  by type, name
| order by p99_ms desc
| take 20
```

### 2. Ver traces completos

```kusto
-- Requests con duration > 2000ms
requests
| where timestamp > ago(15m) and duration > 2000
| project timestamp, name, duration, id, resultCode
| order by duration desc
| take 20
```

Clicar en un `id` en Application Insights para ver el trace completo (end-to-end).

### 3. Correlacionar con métricas de infraestructura

En **Azure Monitor → Metrics**:
- App Service: `CpuPercentage`, `MemoryPercentage`, `HttpQueueLength`
- PostgreSQL: `cpu_percent`, `storage_io_consumption_percent`
- Redis: `used_memory_percentage`, `cache_misses`

---

## Árbol de decisión

```
Latencia alta (P95 > 2s o P99 > 5s)
│
├─ ¿Queries SQL lentas (p99 > 1s)?
│   ├─ SÍ → ver sección "BD lenta"
│   └─ NO → siguiente check
│
├─ ¿Redis timeouts o cache misses altos?
│   ├─ SÍ → ver sección "Redis"
│   └─ NO → siguiente check
│
├─ ¿HttpQueueLength > 0 (requests en cola)?
│   ├─ SÍ → CPU saturado → escalar instancias (ver sección "Scale out")
│   └─ NO → siguiente check
│
├─ ¿Latencia solo en un endpoint específico?
│   ├─ SÍ → revisar código de ese handler (N+1 queries, bucle, etc.)
│   └─ NO → problema sistémico
│
└─ ¿Latencia apareció tras un deploy?
    ├─ SÍ → ROLLBACK (ver runbook-api-down.md)
    └─ NO → investigar cambio de tráfico / datos
```

---

## BD lenta

### Identificar queries sin índice

```sql
-- Queries con sequential scans (sin índice)
SELECT
  schemaname, tablename, seq_scan, seq_tup_read,
  idx_scan, idx_tup_fetch,
  round(seq_tup_read::numeric / nullif(seq_scan, 0), 0) AS avg_rows_per_seq_scan
FROM pg_stat_user_tables
ORDER BY seq_tup_read DESC
LIMIT 20;

-- Queries más costosas (requiere pg_stat_statements)
SELECT query, calls, mean_exec_time, total_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;
```

> El módulo Terraform de PostgreSQL (`infrastructure/terraform/modules/postgresql/main.tf`)
> no configura `azure.extensions` ni `shared_preload_libraries`, así que si la query de
> arriba falla con `relation "pg_stat_statements" does not exist`, hay que habilitar la
> extensión primero (requiere reinicio del servidor):
> ```bash
> az postgres flexible-server parameter set \
>   --name azure.extensions --value pg_stat_statements \
>   --server-name <PG_SERVER_NAME> --resource-group <RG>
> # luego, conectado a la BD:
> # CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
> ```

### Forzar refresco de estadísticas

```sql
-- Actualizar estadísticas del planner
ANALYZE VERBOSE;

-- Ver si el autovacuum está al día
SELECT schemaname, tablename, last_autovacuum, last_autoanalyze, n_dead_tup
FROM pg_stat_user_tables
ORDER BY n_dead_tup DESC;
```

### Añadir índice de emergencia (sin bloqueo)

> Antes de crear uno nuevo: `orders` ya tiene `IX_orders_TenantId_RestaurantId_Status_CreatedAt`
> (`TenantId, RestaurantId, Status, CreatedAt`) y un índice parcial de pedidos activos,
> `ix_orders_table_status_active` (`TableId, Status`, excluyendo `Paid`/`Cancelled` — esos
> son los únicos dos estados terminales del enum `OrderStatus`, no existe un estado
> `Completed`). Confirma con `EXPLAIN ANALYZE` que la query lenta no puede ya usar alguno
> de esos dos antes de añadir uno más — si el cuello de botella está en otra tabla, ajusta
> nombre de tabla/columnas al esquema real (recuerda: sin `HasColumnName` explícito, EF usa
> el nombre C# en PascalCase entre comillas, como `"RestaurantId"`, no `restaurant_id`).

```sql
-- Ejemplo — índice concurrente (no bloquea escrituras) para un patrón de consulta
-- distinto al que ya cubren los índices existentes:
CREATE INDEX CONCURRENTLY IF NOT EXISTS
  idx_orders_emergency
  ON orders ("RestaurantId", "Status")
  WHERE "Status" NOT IN ('Paid', 'Cancelled');
```

---

## Redis lento o con alta presión de memoria

```bash
# Ver memoria actual
az redis show --name <REDIS_NAME> --resource-group <RG> \
  --query "{usedMemory:redisConfiguration.maxmemory, sku:sku}"
```

```bash
# INFO memory via redis-cli (dentro de Cloud Shell con Private Link habilitado)
redis-cli -h <HOST> -p 6380 -a <KEY> --tls INFO memory | grep -E "used_memory_human|maxmemory_human|mem_fragmentation_ratio"
```

**Si `mem_fragmentation_ratio` > 1.5:**
```bash
# Reiniciar Redis para desfragmentar (causa ~30s de indisponibilidad del caché)
# Solo en staging — en prod evaluar impacto
az redis force-reboot --name <REDIS_NAME> --resource-group <RG> --reboot-type AllNodes
```

**Si usado > 80% de maxmemory:**
```bash
# Escalar Redis a tier superior
az redis update --name <REDIS_NAME> --resource-group <RG> --sku Premium --vm-size P2
```

---

## Scale out de la aplicación

```bash
# Aumentar número de instancias de App Service manualmente
az monitor autoscale update \
  --name <AUTOSCALE_SETTING_NAME> \
  --resource-group <RG> \
  --min-count 2 \
  --count 4 \
  --max-count 8
```

O directamente:
```bash
az appservice plan update \
  --name <PLAN_NAME> \
  --resource-group <RG> \
  --number-of-workers 4
```

---

## Verificación tras la acción

```kusto
-- Latencia tras aplicar el fix
requests
| where timestamp > ago(10m)
| summarize
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
  by bin(timestamp, 1m)
| render timechart
```

---

## Post-incidente

1. Documentar la query o componente causante
2. Añadir índice permanentemente vía migración EF Core si fue problema de BD
3. Revisar el umbral de alerta: ¿P95 > 2s es demasiado sensible?
4. Comprobar si el autoscale reaccionó a tiempo o si hay que bajar el trigger de CPU
