# Plan de implementación — Fase Avanzada del motor de recomendaciones

Estado actual: **no implementada**. `RecommendationService` cae de vuelta en la
lógica de la Fase Intermedia (co-occurrence SQL) para restaurantes con más de
1000 pedidos, hasta que el pipeline descrito aquí exista.

## Cuándo activarla

Un restaurante entra en esta fase al superar ~1000 pedidos completados
(`IRecommendationRepository.GetCompletedOrderCountAsync`). A ese volumen, el
co-occurrence simple pierde precisión frente a un modelo que use señales
temporales y de cliente.

## Feature engineering

Por cada (cliente o dispositivo, producto candidato, momento del pedido):

- **Temporales**: hora del día, día de la semana, festivo/víspera, temporada.
- **Historial del cliente**: productos pedidos antes, frecuencia de visita,
  ticket medio, última visita (recencia).
- **Producto**: categoría, precio relativo al ticket medio del carrito,
  tiempo de preparación, tags.
- **Contexto del carrito actual**: productos ya añadidos, subtotal, nº de
  items, franja horaria del pedido.

## Modelo

**LightGBM** (ranking, `objective: lambdarank` o clasificación binaria
`¿se añadirá este producto?` con `objective: binary`), entrenado por
restaurante o con un modelo multi-tenant + embedding de `restaurantId` si el
volumen por restaurante individual no alcanza para entrenar de forma aislada.

Entrenamiento batch (Azure ML pipeline, semanal): extrae `orders` +
`order_items` (JSONB) de Postgres → feature store → entrena → registra el
modelo en el Azure ML Model Registry.

## Serving

`GET /api/v1/recommendations/{customerId}` (el endpoint ya reservado para
esta fase — el endpoint actual, `GET /api/v1/recommendations`, seguiría
sirviendo el resto de restaurantes). Un Azure ML Managed Online Endpoint
expone el modelo; `RecommendationService` lo llamaría como una implementación
alternativa de `IRecommendationRepository`/`IRecommendationService` sólo para
restaurantes en esta fase, cacheado igual que hoy (Redis, TTL corto).

## Por qué no está hecho todavía

Requiere volumen real de pedidos para entrenar (que no existe fuera de
producción), una pipeline de Azure ML y presupuesto de cómputo — fuera del
alcance de esta implementación inicial del motor de recomendaciones, que
prioriza que cold start e intermedia funcionen end-to-end primero.
