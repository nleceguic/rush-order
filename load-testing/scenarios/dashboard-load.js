// ESCENARIO 3 — Dashboard Load
// Simula 100 propietarios consultando el dashboard (GET
// /api/v1/analytics/dashboard) simultáneamente. setup() precalienta la
// cache de Redis (60s TTL — ver AnalyticsController / memoria del módulo
// de analytics) antes del ramp-up, para que el umbral P95<500ms se mida
// con caché caliente, tal como pide el SLO.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, OWNER_POOL, pick } from '../config/environments.js';
import { dashboardThresholds } from '../config/thresholds.js';
import { buildSummary } from '../config/report.js';

export const options = {
  stages: [
    { duration: '20s', target: 100 },
    { duration: '2m', target: 100 },
    { duration: '20s', target: 0 },
  ],
  thresholds: dashboardThresholds,
};

let session = null; // cached per-VU across iterations, same rationale as order-flow.js

function login(owner) {
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: owner.email, password: owner.password }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'login' } },
  );
  if (res.status !== 200) return null;

  const body = res.json();
  if (body.requiresMfa) {
    console.warn(`Owner ${owner.email} has MFA enabled — load-test accounts must have MFA off.`);
    return null;
  }
  return { token: body.accessToken, restaurantId: owner.restaurantId };
}

export function setup() {
  OWNER_POOL.forEach((owner) => {
    const s = login(owner);
    if (!s) return;
    http.get(`${BASE_URL}/api/v1/analytics/dashboard?restaurantId=${s.restaurantId}`, {
      headers: { Authorization: `Bearer ${s.token}` },
      tags: { name: 'cache_warmup' },
    });
  });
}

export default function () {
  const owner = pick(OWNER_POOL, __VU);

  if (!session) {
    session = login(owner);
    if (!session) {
      sleep(5);
      return;
    }
  }

  const res = http.get(`${BASE_URL}/api/v1/analytics/dashboard?restaurantId=${session.restaurantId}`, {
    headers: { Authorization: `Bearer ${session.token}` },
    tags: { name: 'dashboard' },
  });

  check(res, { 'dashboard 200': (r) => r.status === 200 });

  sleep(2 + Math.random() * 3);
}

export function handleSummary(data) {
  return buildSummary(data, 'dashboard-load');
}
