# Runbook: API no responde

**Trigger:** Alerta de disponibilidad < 99% — Azure Monitor / Slack #alerts  
**Severidad:** Crítica (P0)  
**Tiempo objetivo de resolución:** 15 min

---

## Diagnóstico rápido (< 5 min)

### 1. Confirmar el incidente

```bash
# Health check básico
curl -s https://<PROD_URL>/health | jq .

# Verificar el slot de producción en App Service
az webapp show --name <APP_NAME> --resource-group <RG> --query "state"

# Comprobar últimos deployments
az webapp deployment list --name <APP_NAME> --resource-group <RG> --query "[].{id:id,status:status,message:message,receivedTime:receivedTime}"
```

### 2. Revisar logs en tiempo real

```bash
# Log stream de App Service
az webapp log tail --name <APP_NAME> --resource-group <RG>
```

O en **Azure Portal → App Service → Log stream**.

### 3. Consulta en Log Analytics (KQL)

```kusto
-- Errores de los últimos 15 min
exceptions
| where timestamp > ago(15m)
| summarize count() by type, outerMessage
| order by count_ desc

-- Tasa de errores HTTP
requests
| where timestamp > ago(15m)
| summarize
    total = count(),
    errors = countif(success == false)
  by bin(timestamp, 1m)
| extend errorRate = round(100.0 * errors / total, 2)
```

---

## Árbol de decisión

```
API no responde (HTTP timeout / 5xx)
│
├─ ¿App Service running?
│   ├─ NO → Reiniciar: az webapp restart --name <APP> --resource-group <RG>
│   └─ SÍ → siguiente check
│
├─ ¿Último deploy reciente (< 30 min)?
│   ├─ SÍ → ROLLBACK (ver sección)
│   └─ NO → siguiente check
│
├─ ¿Errores de base de datos en logs?
│   ├─ SÍ → Ver runbook-db-connection.md
│   └─ NO → siguiente check
│
├─ ¿OOM / Memory pressure?
│   ├─ SÍ → Scale up el App Service Plan: az appservice plan update --sku P3v3
│   └─ NO → siguiente check
│
└─ ¿Error en código / excepción no controlada?
    └─ Revisar exceptions en Application Insights, hotfix y nuevo deploy
```

---

## Rollback (blue/green swap)

Si el incidente coincide con un deploy reciente, el slot de staging aún tiene la versión anterior:

```bash
az webapp deployment slot swap \
  --name <APP_NAME> \
  --resource-group <RG> \
  --slot staging \
  --target-slot production
```

Verificar tras el swap:
```bash
curl -s https://<PROD_URL>/health | jq .status
```

---

## Reinicio de emergencia

```bash
# Reiniciar la app (provoca ~30s de downtime)
az webapp restart --name <APP_NAME> --resource-group <RG>

# O reiniciar el slot de staging si el swap es la causa
az webapp restart --name <APP_NAME> --resource-group <RG> --slot staging
```

---

## Escalado de emergencia

```bash
# Escalar verticalmente (requiere ~5 min de reinicio)
az appservice plan update \
  --name <APP_SERVICE_PLAN> \
  --resource-group <RG> \
  --sku P3v3

# Escalar horizontalmente (instancias adicionales, sin downtime)
az monitor autoscale update \
  --name <AUTOSCALE_NAME> \
  --resource-group <RG> \
  --count 5
```

---

## Verificación (tras cualquiera de las acciones anteriores)

```bash
curl -f https://<PROD_URL>/health | jq .
```
- [ ] `status` es `Healthy` y todos los `checks` individuales también
- [ ] La tasa de errores 5xx vuelve a niveles normales (consulta KQL de la sección de diagnóstico)
- [ ] La alerta de disponibilidad en Azure Monitor se resuelve (deja de estar en estado "Fired")

---

## Post-incidente

1. Documentar en canal #incidents: causa raíz, tiempo de resolución, impacto
2. Abrir issue en GitHub con label `incident`
3. Revisión post-mortem dentro de 48h
4. Actualizar umbrales de alerta si hubo falso positivo
