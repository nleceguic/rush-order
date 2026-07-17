// Resolves BASE_URL / WS_BASE_URL and test-account pools for the target
// environment. Every scenario imports from here instead of hardcoding URLs.
//
// Usage: k6 run -e ENVIRONMENT=staging -e BASE_URL=https://api-staging... scenarios/menu-load.js
// (run-load-tests.sh sets ENVIRONMENT for you; BASE_URL is required for
// staging/production — see the error thrown below for why).

const LOCAL_DEFAULT = {
  baseUrl: 'http://localhost:5000',
  wsBaseUrl: 'ws://localhost:5000',
};

function deriveWsUrl(httpUrl) {
  if (!httpUrl) return '';
  return httpUrl.replace(/^https:/, 'wss:').replace(/^http:/, 'ws:');
}

export const ENVIRONMENT = __ENV.ENVIRONMENT || 'local';

export const BASE_URL =
  __ENV.BASE_URL || (ENVIRONMENT === 'local' ? LOCAL_DEFAULT.baseUrl : '');

if (!BASE_URL) {
  throw new Error(
    `BASE_URL is required for environment "${ENVIRONMENT}". ` +
      `Pass -e BASE_URL=https://... (in CI: vars.STAGING_API_URL / vars.PROD_API_URL).`,
  );
}

export const WS_BASE_URL =
  __ENV.WS_BASE_URL ||
  (ENVIRONMENT === 'local' ? LOCAL_DEFAULT.wsBaseUrl : deriveWsUrl(BASE_URL));

// QR token for the public menu endpoint. "demo-token" matches the seeded
// demo restaurant already referenced by the Lighthouse CI step in
// cd-staging.yml (pwa/menu/demo-token) — reuse the same seed data.
export const QR_TOKEN = __ENV.QR_TOKEN || 'demo-token';

// Fallback restaurant/account used when no multi-tenant pool is supplied.
// Reuses the same seeded E2E account the Playwright smoke tests use
// (E2E_TEST_EMAIL / E2E_TEST_PASSWORD / E2E_RESTAURANT_ID), so the flow
// scenarios are runnable out of the box without extra data setup.
const RESTAURANT_ID = __ENV.RESTAURANT_ID || __ENV.E2E_RESTAURANT_ID || '';
const FALLBACK_EMAIL = __ENV.LOAD_TEST_EMAIL || __ENV.E2E_TEST_EMAIL || '';
const FALLBACK_PASSWORD = __ENV.LOAD_TEST_PASSWORD || __ENV.E2E_TEST_PASSWORD || '';

function parsePoolJson(raw) {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) && parsed.length > 0 ? parsed : null;
  } catch (e) {
    console.warn(`Could not parse pool JSON (${e}) — falling back to the single seeded account.`);
    return null;
  }
}

// WAITER_POOL / OWNER_POOL: [{ email, password, restaurantId }, ...]
//
// For a realistic "50 restaurantes x 3 camareros" run, seed staging with
// 150 waiter accounts across 50 tenants and pass them as
// LOAD_TEST_WAITERS_JSON='[{"email":"...","password":"...","restaurantId":"..."}, ...]'.
// Without that, every VU shares the one fallback account above — order-flow.js
// and dashboard-load.js still exercise the same endpoints under load, just
// against a single tenant instead of fifty.
export const WAITER_POOL = parsePoolJson(__ENV.LOAD_TEST_WAITERS_JSON) || [
  { email: FALLBACK_EMAIL, password: FALLBACK_PASSWORD, restaurantId: RESTAURANT_ID },
];

export const OWNER_POOL = parsePoolJson(__ENV.LOAD_TEST_OWNERS_JSON) || [
  { email: FALLBACK_EMAIL, password: FALLBACK_PASSWORD, restaurantId: RESTAURANT_ID },
];

export function pick(pool, index) {
  return pool[index % pool.length];
}
