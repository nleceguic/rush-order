// Central SLOs (Service Level Objectives) so every scenario enforces the
// same numbers, instead of each script drifting its own copy.
//
//   Menú QR      — P95 < 200ms,  P99 < 500ms,  error rate < 0.1%
//   Crear pedido — P95 < 500ms,  P99 < 1000ms, error rate < 0.5%
//   Dashboard    — P95 < 500ms with a warm cache
//   SignalR      — event delivered in < 500ms P95 (best-effort, opt-in — see config/signalr.js)

export const menuThresholds = {
  http_req_duration: ['p(95)<200', 'p(99)<500'],
  http_req_failed: ['rate<0.001'],
};

export const orderThresholds = {
  'http_req_duration{name:create_order}': ['p(95)<500', 'p(99)<1000'],
  http_req_failed: ['rate<0.005'],
};

export const dashboardThresholds = {
  'http_req_duration{name:dashboard}': ['p(95)<500'],
  http_req_failed: ['rate<0.01'],
};

export const signalrThresholds = {
  signalr_event_latency: ['p(95)<500'],
};

// The spike scenario intentionally overloads the API 10x — some shedding
// (429s, slower responses) is expected and should not fail the run; it
// exists to observe behavior under an extreme burst, not to enforce the
// steady-state SLOs above.
export const spikeThresholds = {
  http_req_duration: ['p(95)<1000'],
  http_req_failed: ['rate<0.05'],
};
