#!/usr/bin/env bash
# Usage: load-testing/run-load-tests.sh <environment> <scenario>
#
#   environment: local | staging | production
#   scenario:    menu-load | order-flow | dashboard-load | spike-test | all
#
# Exit code: 0 if every threshold in the run passed, 1 if any failed.
#
# Env vars (see config/environments.js for full list + fallbacks):
#   BASE_URL           required for staging/production (e.g. vars.STAGING_API_URL)
#   QR_TOKEN            defaults to "demo-token"
#   RESTAURANT_ID / E2E_RESTAURANT_ID
#   LOAD_TEST_EMAIL / E2E_TEST_EMAIL, LOAD_TEST_PASSWORD / E2E_TEST_PASSWORD
#   LOAD_TEST_WAITERS_JSON, LOAD_TEST_OWNERS_JSON   real multi-tenant pools (optional)
#   MEASURE_SIGNALR=true   opt in to the best-effort SignalR latency check (order-flow only)
#
# IMPORTANT — rate limiting: the API enforces 100 req/min per IP globally
# (Program.cs). k6 traffic comes from a single IP, so at load-test
# concurrency you WILL hit 429s unless DisableRateLimit=true is set on the
# target for the duration of the run. The staging CI job does this
# automatically (see .github/workflows/cd-staging.yml); for ad-hoc runs
# against staging/production, set it yourself first.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ENVIRONMENT="${1:-}"
SCENARIO="${2:-}"

if [[ -z "$ENVIRONMENT" || -z "$SCENARIO" ]]; then
  echo "Usage: $0 <local|staging|production> <menu-load|order-flow|dashboard-load|spike-test|all>" >&2
  exit 1
fi

if ! command -v k6 >/dev/null 2>&1; then
  echo "k6 is not installed — see https://k6.io/docs/get-started/installation/" >&2
  exit 1
fi

if [[ "$ENVIRONMENT" != "local" ]]; then
  echo "⚠️  Running against '$ENVIRONMENT' — make sure the rate limiter is disabled on the target for this window (DisableRateLimit=true), or the run will fail on 429s that aren't a real capacity problem." >&2
fi

mkdir -p reports

declare -A SCENARIO_FILES=(
  [menu-load]="scenarios/menu-load.js"
  [order-flow]="scenarios/order-flow.js"
  [dashboard-load]="scenarios/dashboard-load.js"
  [spike-test]="scenarios/spike-test.js"
)

run_one() {
  local name="$1"
  local file="${SCENARIO_FILES[$name]}"
  echo "── Running $name against $ENVIRONMENT ──"

  if k6 run "$file" -e "ENVIRONMENT=$ENVIRONMENT"; then
    echo "✅ $name passed all thresholds"
    return 0
  else
    echo "❌ $name failed one or more thresholds" >&2
    return 1
  fi
}

exit_code=0

if [[ "$SCENARIO" == "all" ]]; then
  for name in menu-load order-flow dashboard-load spike-test; do
    run_one "$name" || exit_code=1
  done
else
  if [[ -z "${SCENARIO_FILES[$SCENARIO]:-}" ]]; then
    echo "Unknown scenario: $SCENARIO (expected one of: menu-load, order-flow, dashboard-load, spike-test, all)" >&2
    exit 1
  fi
  run_one "$SCENARIO" || exit_code=1
fi

exit $exit_code
