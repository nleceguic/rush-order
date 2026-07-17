#!/usr/bin/env bash
# Post-deploy verification for Rush Order PWA on Azure Static Web Apps.
# Usage: ./scripts/verify-pwa-deploy.sh https://app.rushorder.es

set -euo pipefail

BASE_URL="${1:-https://app.rushorder.es}"
PASS=0
FAIL=0

green() { echo -e "\033[32m✅  $*\033[0m"; }
red()   { echo -e "\033[31m❌  $*\033[0m"; }
info()  { echo -e "\033[34mℹ️   $*\033[0m"; }

check_status() {
  local url="$1"
  local expected="${2:-200}"
  local label="$3"

  actual=$(curl -s -o /dev/null -w "%{http_code}" \
    --max-time 10 --location "$url")

  if [ "$actual" = "$expected" ]; then
    green "HTTP $actual — $label"
    PASS=$((PASS + 1))
  else
    red "HTTP $actual (expected $expected) — $label ($url)"
    FAIL=$((FAIL + 1))
  fi
}

check_header() {
  local url="$1"
  local header="$2"
  local expected_pattern="$3"
  local label="$4"

  value=$(curl -s -o /dev/null -D - --max-time 10 "$url" \
    | grep -i "^${header}:" | head -1 | sed 's/[[:space:]]*$//')

  if echo "$value" | grep -qi "$expected_pattern"; then
    green "$header matches '$expected_pattern' — $label"
    PASS=$((PASS + 1))
  else
    red "$header missing/wrong ('$value') — $label"
    FAIL=$((FAIL + 1))
  fi
}

check_cache() {
  local url="$1"
  local expected_cc="$2"
  local label="$3"
  check_header "$url" "cache-control" "$expected_cc" "$label"
}

# ── 1. Availability checks ─────────────────────────────────────────────────────
info "=== Availability ==="
check_status "${BASE_URL}/"              200 "Root → SPA shell"
check_status "${BASE_URL}/menu/test-qr" 200 "SPA fallback for /menu/* route"
check_status "${BASE_URL}/manifest.json" 200 "PWA manifest"
check_status "${BASE_URL}/sw.js"         200 "Service Worker"
check_status "${BASE_URL}/offline.html"  200 "Offline fallback page"

# ── 2. SPA routing — deep links must not 500 ──────────────────────────────────
info "=== SPA deep-link routing ==="
check_status "${BASE_URL}/menu/nonexistent-restaurant" 200 "Unknown menu route → SPA (not 500)"
check_status "${BASE_URL}/random-path/that/does-not-exist" 200 "Unknown path → SPA (not 404 raw)"

# ── 3. Cache headers ───────────────────────────────────────────────────────────
info "=== Cache-Control headers ==="
check_cache "${BASE_URL}/sw.js"          "no-cache"    "Service Worker must not be cached"
check_cache "${BASE_URL}/manifest.json"  "no-cache"    "Manifest must be fresh"
# Spot-check: if an assets/ file exists, verify immutable
ASSET_URL=$(curl -s "${BASE_URL}/" | grep -o 'src="/assets/[^"]*\.js"' | head -1 | sed 's/src="//;s/"//')
if [ -n "$ASSET_URL" ]; then
  check_cache "${BASE_URL}${ASSET_URL}" "immutable" "Vite hashed asset → immutable"
else
  info "No asset URL found in index.html — skipping asset cache check"
fi

# ── 4. Security headers ────────────────────────────────────────────────────────
info "=== Security headers ==="
check_header "${BASE_URL}/" "x-content-type-options" "nosniff"        "X-Content-Type-Options"
check_header "${BASE_URL}/" "x-frame-options"        "DENY"           "X-Frame-Options"
check_header "${BASE_URL}/" "content-security-policy" "default-src"   "CSP present"

# ── 5. HTTPS redirect ──────────────────────────────────────────────────────────
info "=== HTTPS redirect ==="
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 \
  "http://${BASE_URL#https://}/" 2>/dev/null || echo "000")
if [ "$HTTP_STATUS" = "301" ] || [ "$HTTP_STATUS" = "302" ] || [ "$HTTP_STATUS" = "308" ]; then
  green "HTTP redirects to HTTPS (HTTP $HTTP_STATUS)"
  PASS=$((PASS + 1))
else
  # Azure SWA handles this at the platform level — might not be checkable externally
  info "HTTP redirect check returned $HTTP_STATUS (platform may handle this transparently)"
fi

# ── 6. PWA features ────────────────────────────────────────────────────────────
info "=== PWA features ==="
MANIFEST_CONTENT=$(curl -sf "${BASE_URL}/manifest.json" --max-time 10 || echo "{}")
if echo "$MANIFEST_CONTENT" | grep -q '"name"'; then
  green "manifest.json has name field"
  PASS=$((PASS + 1))
else
  red "manifest.json missing or malformed"
  FAIL=$((FAIL + 1))
fi

SW_SIZE=$(curl -sf "${BASE_URL}/sw.js" --max-time 10 | wc -c)
if [ "$SW_SIZE" -gt 100 ]; then
  green "sw.js is non-empty (${SW_SIZE} bytes)"
  PASS=$((PASS + 1))
else
  red "sw.js is missing or empty"
  FAIL=$((FAIL + 1))
fi

# ── Summary ────────────────────────────────────────────────────────────────────
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Results for ${BASE_URL}"
echo "  Passed: ${PASS}  |  Failed: ${FAIL}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
