// ESCENARIO 1 — Menu Load
// Simula 100 clientes escaneando el QR de mesa simultáneamente y leyendo
// el menú público (GET /api/v1/menu/public/{qrToken}, sin auth, cacheado
// 30s en el controller — ver MenuController.GetPublicMenu).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, QR_TOKEN } from '../config/environments.js';
import { menuThresholds } from '../config/thresholds.js';
import { buildSummary } from '../config/report.js';

export const options = {
  stages: [
    { duration: '30s', target: 50 }, // ramp up
    { duration: '2m', target: 100 }, // steady state
    { duration: '30s', target: 0 }, // ramp down
  ],
  thresholds: menuThresholds,
};

export default function () {
  const res = http.get(`${BASE_URL}/api/v1/menu/public/${QR_TOKEN}`, {
    tags: { name: 'menu_public' },
  });

  check(res, {
    'status 200': (r) => r.status === 200,
    'has categories': (r) => {
      try {
        return Array.isArray(r.json('data.categories'));
      } catch {
        return false;
      }
    },
  });

  sleep(Math.random() * 2);
}

export function handleSummary(data) {
  return buildSummary(data, 'menu-load');
}
