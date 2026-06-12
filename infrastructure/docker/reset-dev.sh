#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOCKER_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Deteniendo y eliminando contenedores y volúmenes..."
docker compose \
  -f "$DOCKER_DIR/docker-compose.yml" \
  --env-file "$ROOT/.env" \
  down -v --remove-orphans

echo "Reiniciando entorno limpio..."
bash "$DOCKER_DIR/start-dev.sh"
