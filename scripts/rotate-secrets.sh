#!/usr/bin/env bash
# JWT RSA key pair rotation.
# Generates a new 4096-bit RSA key pair, uploads to Azure Key Vault,
# and triggers a rolling restart of the App Service.
#
# Usage: KEY_VAULT_NAME=mykv APP_SERVICE_NAME=myapp RESOURCE_GROUP=myrg \
#        ./scripts/rotate-secrets.sh
#
# Required env vars:
#   KEY_VAULT_NAME     — Azure Key Vault name
#   APP_SERVICE_NAME   — App Service name (will be restarted)
#   RESOURCE_GROUP     — Resource group containing the App Service

set -euo pipefail

green() { echo -e "\033[32m✅  $*\033[0m"; }
red()   { echo -e "\033[31m❌  $*\033[0m"; }
info()  { echo -e "\033[34mℹ️   $*\033[0m"; }
warn()  { echo -e "\033[33m⚠️   $*\033[0m"; }

for var in KEY_VAULT_NAME APP_SERVICE_NAME RESOURCE_GROUP; do
  if [ -z "${!var:-}" ]; then
    red "Required env var $var is not set"
    exit 1
  fi
done

TMPDIR=$(mktemp -d)
PRIVATE_KEY="${TMPDIR}/private.pem"
PUBLIC_KEY="${TMPDIR}/public.pem"
ROTATION_DATE=$(date -u +%Y-%m-%dT%H:%M:%SZ)

cleanup() { rm -rf "${TMPDIR}"; }
trap cleanup EXIT

# ── 1. Generate new RSA-4096 key pair ─────────────────────────────────────────
info "Generating new RSA-4096 key pair…"
openssl genrsa -out "${PRIVATE_KEY}" 4096 2>/dev/null
openssl rsa -in "${PRIVATE_KEY}" -pubout -out "${PUBLIC_KEY}" 2>/dev/null
green "Key pair generated"

# ── 2. Archive current secret versions (for rollback) ─────────────────────────
info "Backing up current secret versions…"
CURRENT_PRIVATE_VERSION=$(az keyvault secret show \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "JwtPrivateKey" \
  --query "id" -o tsv 2>/dev/null || echo "none")
echo "  Current JwtPrivateKey version: ${CURRENT_PRIVATE_VERSION}"

# ── 3. Upload new secrets to Key Vault ────────────────────────────────────────
info "Uploading new private key to Key Vault…"
az keyvault secret set \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "JwtPrivateKey" \
  --file "${PRIVATE_KEY}" \
  --content-type "text/plain" \
  --tags "rotated=${ROTATION_DATE}" \
  --output none

info "Uploading new public key to Key Vault…"
az keyvault secret set \
  --vault-name "${KEY_VAULT_NAME}" \
  --name "JwtPublicKey" \
  --file "${PUBLIC_KEY}" \
  --content-type "text/plain" \
  --tags "rotated=${ROTATION_DATE}" \
  --output none

green "Secrets updated in Key Vault"

# ── 4. Rolling restart — App Service picks up new secrets from KV references ──
# Existing sessions using old JWTs will fail after restart.
# Users need to log in again (by design: refresh token rotation handles this gracefully).
warn "About to restart App Service '${APP_SERVICE_NAME}'. Active sessions will be invalidated."
read -r -p "Continue? (y/N): " confirm
if [[ "${confirm,,}" != "y" ]]; then
  warn "Rotation aborted after secret update. Re-run to restart the App Service."
  exit 0
fi

info "Restarting App Service (rolling — ~30s downtime)…"
az webapp restart \
  --name "${APP_SERVICE_NAME}" \
  --resource-group "${RESOURCE_GROUP}"

green "App Service restarted"

# ── 5. Verify health after restart ────────────────────────────────────────────
info "Waiting 30s for App Service to become healthy…"
sleep 30
APP_URL=$(az webapp show \
  --name "${APP_SERVICE_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query "defaultHostName" -o tsv)

HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  "https://${APP_URL}/health" --max-time 15 || echo "000")

if [ "${HTTP_STATUS}" = "200" ]; then
  green "App Service healthy after key rotation (HTTP 200)"
else
  red "App Service health check returned HTTP ${HTTP_STATUS}"
  warn "If the app is degraded, roll back by disabling the new secret version in Key Vault:"
  echo "  az keyvault secret set-attributes --vault-name ${KEY_VAULT_NAME} --name JwtPrivateKey --enabled false"
  echo "  Then restore the previous version and restart."
  exit 1
fi

echo ""
green "Secret rotation completed successfully at ${ROTATION_DATE}"
echo "  Rollback: re-enable previous Key Vault secret version"
echo "  Next rotation: in 90 days"
