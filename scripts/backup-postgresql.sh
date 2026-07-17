#!/usr/bin/env bash
# PostgreSQL backup: pg_dump + upload to Azure Blob Storage.
# Usage: ENVIRONMENT=production ./scripts/backup-postgresql.sh
#
# Required env vars:
#   PGHOST, PGPORT (default 5432), PGUSER, PGPASSWORD, PGDATABASE
#   AZURE_STORAGE_ACCOUNT   — target storage account name
#   AZURE_STORAGE_CONTAINER — container name (default: backups)
#   ENVIRONMENT             — environment label (production, staging)

set -euo pipefail

ENVIRONMENT="${ENVIRONMENT:-production}"
PGPORT="${PGPORT:-5432}"
CONTAINER="${AZURE_STORAGE_CONTAINER:-backups}"

TIMESTAMP=$(date -u +%Y-%m-%d-%H-%M)
DUMP_DIR="/tmp/rushorder-backup-${TIMESTAMP}"
DUMP_FILE="${ENVIRONMENT}.dump"
BLOB_PATH="postgresql/${TIMESTAMP}/${DUMP_FILE}"

green()  { echo -e "\033[32m✅  $*\033[0m"; }
red()    { echo -e "\033[31m❌  $*\033[0m"; }
info()   { echo -e "\033[34mℹ️   $*\033[0m"; }

# ── Validate required vars ─────────────────────────────────────────────────────
for var in PGHOST PGUSER PGPASSWORD PGDATABASE AZURE_STORAGE_ACCOUNT; do
  if [ -z "${!var:-}" ]; then
    red "Required environment variable $var is not set"
    exit 1
  fi
done

mkdir -p "${DUMP_DIR}"

info "Starting backup of '${PGDATABASE}' on ${PGHOST} → ${BLOB_PATH}"

# ── pg_dump ───────────────────────────────────────────────────────────────────
# -Fc: custom format — compressed, restoreable with pg_restore
export PGPASSWORD
pg_dump \
  -h "${PGHOST}" \
  -p "${PGPORT}" \
  -U "${PGUSER}" \
  -d "${PGDATABASE}" \
  -Fc \
  -v \
  -f "${DUMP_DIR}/${DUMP_FILE}"

DUMP_SIZE=$(du -sh "${DUMP_DIR}/${DUMP_FILE}" | cut -f1)
green "Dump completed — size: ${DUMP_SIZE}"

# ── Upload to Azure Blob Storage ──────────────────────────────────────────────
info "Uploading to ${AZURE_STORAGE_ACCOUNT}/${CONTAINER}/${BLOB_PATH}…"
az storage blob upload \
  --account-name  "${AZURE_STORAGE_ACCOUNT}" \
  --container-name "${CONTAINER}" \
  --name          "${BLOB_PATH}" \
  --file          "${DUMP_DIR}/${DUMP_FILE}" \
  --auth-mode     login \
  --overwrite     false \
  --metadata      "environment=${ENVIRONMENT}" "timestamp=${TIMESTAMP}" "database=${PGDATABASE}"

green "Upload complete: ${BLOB_PATH}"

# ── Write checksum ────────────────────────────────────────────────────────────
sha256sum "${DUMP_DIR}/${DUMP_FILE}" > "${DUMP_DIR}/${DUMP_FILE}.sha256"
az storage blob upload \
  --account-name  "${AZURE_STORAGE_ACCOUNT}" \
  --container-name "${CONTAINER}" \
  --name          "${BLOB_PATH}.sha256" \
  --file          "${DUMP_DIR}/${DUMP_FILE}.sha256" \
  --auth-mode     login \
  --overwrite     false

# ── Clean up ──────────────────────────────────────────────────────────────────
rm -rf "${DUMP_DIR}"
unset PGPASSWORD

echo ""
green "Backup finished: ${BLOB_PATH}"
echo "   Restore command:"
echo "   pg_restore -h \$PGHOST -U \$PGUSER -d \$PGDATABASE -Fc <downloaded-dump>"
