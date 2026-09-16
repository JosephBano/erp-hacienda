#!/usr/bin/env bash
# Smoke test: verifica que el API responde /health y /version.
# Soporta ejecución local levantando docker compose, o verificación
# remota contra un endpoint desplegado (D8 / ADR-0031).
#
# Uso:
#   ./scripts/smoke-api-container.sh                     # local con docker compose
#   ./scripts/smoke-api-container.sh http://host/health  # remoto sin tocar docker compose

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

API_URL="${1:-${SMOKE_API_URL:-http://localhost:8080/health}}"
MAX_WAIT_SECONDS="${MAX_WAIT_SECONDS:-60}"
SLEEP_SECONDS=2

IS_REMOTE=0
if [ -n "${1:-}" ] || [ -n "${SMOKE_API_URL:-}" ]; then
    IS_REMOTE=1
fi

if [ "$IS_REMOTE" -eq 0 ]; then
    cleanup() {
        echo "[smoke] bajando la pila..."
        docker compose down --remove-orphans >/dev/null 2>&1 || true
    }
    trap cleanup EXIT

    echo "[smoke] construyendo y levantando la pila..."
    docker compose up -d --build
fi

echo -n "[smoke] esperando que la API responda en $API_URL"
elapsed=0
while (( elapsed < MAX_WAIT_SECONDS )); do
    if curl -fsS -o /dev/null "$API_URL" 2>/dev/null; then
        echo
        echo "[smoke] OK — endpoint respondió en $API_URL"
        echo "[smoke] respuesta de health:"
        curl -fsS "$API_URL" || true
        echo

        # También verificar endpoint de versión si es accesible
        BASE_URL="${API_URL%/health}"
        VERSION_URL="${BASE_URL}/version"
        echo "[smoke] verificando endpoint de versión en $VERSION_URL..."
        if curl -fsS -o /dev/null "$VERSION_URL" 2>/dev/null; then
            echo "[smoke] respuesta de version:"
            curl -fsS "$VERSION_URL" || true
            echo
        fi
        exit 0
    fi
    echo -n "."
    sleep "$SLEEP_SECONDS"
    elapsed=$(( elapsed + SLEEP_SECONDS ))
done

echo
echo "[smoke] FAIL — endpoint no respondió en ${MAX_WAIT_SECONDS}s ($API_URL)"
if [ "$IS_REMOTE" -eq 0 ]; then
    echo "[smoke] logs del api (últimas 50 líneas):"
    docker compose logs --tail=50 api || true
fi
exit 1
