#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOCKER_DIR="$(cd "$(dirname "$0")" && pwd)"

# Crear .env desde .env.example si no existe
if [ ! -f "$ROOT/.env" ]; then
  echo "  .env no encontrado — creando desde .env.example..."
  cp "$ROOT/.env.example" "$ROOT/.env"
  echo "  Edita $ROOT/.env antes de continuar si necesitas cambiar credenciales."
fi

echo "Levantando entorno de desarrollo..."
docker compose \
  -f "$DOCKER_DIR/docker-compose.yml" \
  --env-file "$ROOT/.env" \
  up -d --remove-orphans

echo ""
echo "Servicios disponibles:"
echo "  PostgreSQL  → localhost:5432"
echo "  Redis       → localhost:6379"
echo "  pgAdmin     → http://localhost:5050"
echo "  MailHog UI  → http://localhost:8025"
echo "  MailHog SMTP→ localhost:1025"
