// ESCENARIO 2 — Order Flow
// Simula 50 restaurantes con 3 camareros cada uno (150 VUs) tomando
// pedidos: login -> mesas -> productos -> crear pedido -> confirmar ->
// sleep 5-10s -> repite.
//
// El login se cachea por VU (variable de módulo `session`, persiste entre
// iteraciones de un mismo VU en k6) en vez de re-loguear en cada vuelta:
// con 150 VUs, loguear en cada iteración agotaría el rate limiter global
// de la API (100 req/min por IP — ver Program.cs) casi de inmediato, y
// tampoco refleja cómo se comporta la app real (el camarero mantiene el
// JWT hasta que expira).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL, WS_BASE_URL, WAITER_POOL, pick } from '../config/environments.js';
import { orderThresholds, signalrThresholds } from '../config/thresholds.js';
import { buildSummary } from '../config/report.js';
import { measureOrderStatusLatency } from '../config/signalr.js';

const MEASURE_SIGNALR = (__ENV.MEASURE_SIGNALR || 'false') === 'true';

export const options = {
  stages: [
    { duration: '30s', target: 150 },
    { duration: '3m', target: 150 },
    { duration: '30s', target: 0 },
  ],
  thresholds: MEASURE_SIGNALR ? { ...orderThresholds, ...signalrThresholds } : orderThresholds,
};

const orderFlowFailures = new Counter('order_flow_failures');

let session = null; // cached per-VU across iterations, see header comment

function login(waiter) {
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: waiter.email, password: waiter.password }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'login' } },
  );

  if (!check(res, { 'login succeeded': (r) => r.status === 200 })) return null;

  const body = res.json();
  if (body.requiresMfa) {
    console.warn(`Waiter ${waiter.email} has MFA enabled — load-test accounts must have MFA off.`);
    return null;
  }
  return { token: body.accessToken, restaurantId: waiter.restaurantId };
}

function abort() {
  orderFlowFailures.add(1);
  sleep(5);
}

export default function () {
  const waiter = pick(WAITER_POOL, __VU);

  if (!session) {
    session = login(waiter);
    if (!session) return abort();
  }

  const authHeaders = { Authorization: `Bearer ${session.token}`, 'Content-Type': 'application/json' };

  // 2. GET /tables
  const tablesRes = http.get(`${BASE_URL}/api/v1/tables?restaurantId=${session.restaurantId}`, {
    headers: authHeaders,
    tags: { name: 'get_tables' },
  });
  if (!check(tablesRes, { 'tables 200': (r) => r.status === 200 })) return abort();

  const tables = tablesRes.json('data') || [];
  if (tables.length === 0) return abort();
  const table = tables[Math.floor(Math.random() * tables.length)];

  // 3. GET /menu/products
  const productsRes = http.get(
    `${BASE_URL}/api/v1/menu/products?restaurantId=${session.restaurantId}&onlyAvailable=true&pageSize=50`,
    { headers: authHeaders, tags: { name: 'get_products' } },
  );
  if (!check(productsRes, { 'products 200': (r) => r.status === 200 })) return abort();

  const products = productsRes.json('data') || [];
  if (products.length === 0) return abort();

  // 3-5 random items
  const itemCount = 3 + Math.floor(Math.random() * 3);
  const items = [];
  for (let i = 0; i < itemCount; i++) {
    const product = products[Math.floor(Math.random() * products.length)];
    items.push({ productId: product.id, quantity: 1 + Math.floor(Math.random() * 3) });
  }

  // 4. POST /orders
  const orderRes = http.post(
    `${BASE_URL}/api/v1/orders`,
    JSON.stringify({ tableId: table.id, customerId: null, items, notes: null, source: 'Manual' }),
    { headers: authHeaders, tags: { name: 'create_order' } },
  );
  if (!check(orderRes, { 'order created': (r) => r.status === 201 })) return abort();

  const orderId = orderRes.json('data.orderId');

  // 5. PATCH /orders/{id}/status
  const sendStatusUpdate = () => {
    const statusRes = http.patch(
      `${BASE_URL}/api/v1/orders/${orderId}/status`,
      JSON.stringify({ status: 'Confirmed', note: null }),
      { headers: authHeaders, tags: { name: 'update_status' } },
    );
    check(statusRes, { 'status updated': (r) => r.status === 204 });
  };

  if (MEASURE_SIGNALR) {
    measureOrderStatusLatency(WS_BASE_URL, session.token, session.restaurantId, orderId, sendStatusUpdate);
  } else {
    sendStatusUpdate();
  }

  // 6-7. Sleep 5-10s, repeat
  sleep(5 + Math.random() * 5);
}

export function handleSummary(data) {
  return buildSummary(data, 'order-flow');
}
