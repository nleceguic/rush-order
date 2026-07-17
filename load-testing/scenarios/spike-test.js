// ESCENARIO 4 — Spike Test
// Simula el pico de Nochevieja: 10x el tráfico normal en 30 segundos
// contra el menú público (el endpoint que de verdad recibe el pico real,
// ya que cada mesa escanea su QR al mismo tiempo). No espera cero errores
// — ver spikeThresholds — el objetivo es observar cómo degrada el sistema
// bajo una sobrecarga extrema, no validar las SLOs de estado estable.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, QR_TOKEN } from '../config/environments.js';
import { spikeThresholds } from '../config/thresholds.js';
import { buildSummary } from '../config/report.js';

export const options = {
  stages: [
    { duration: '10s', target: 500 }, // 0 -> 500 VUs
    { duration: '30s', target: 500 }, // hold the spike
    { duration: '10s', target: 0 }, // 500 -> 0
  ],
  thresholds: spikeThresholds,
};

export default function () {
  const res = http.get(`${BASE_URL}/api/v1/menu/public/${QR_TOKEN}`, {
    tags: { name: 'menu_public_spike' },
  });

  check(res, {
    'status is 200 or a graceful 429/503': (r) => [200, 429, 503].includes(r.status),
  });

  sleep(Math.random());
}

export function handleSummary(data) {
  return buildSummary(data, 'spike-test');
}
