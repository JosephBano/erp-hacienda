#!/usr/bin/env bash
# Smoke test: levanta la pila (postgres + api), golpea /health, baja.
# Pensado para revisión manual del PR — no se ejecuta en CI en este commit.
#
# Uso:
#   ./scripts/smoke-api-container.sh
#
# Requiere: docker compose v2, curl. La pila previa (hato-postgres, hato-api)
# se detiene y recrea — no apto para entornos donde ya haya otros contenedores
# de compose en pie con esos nombres.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

API_URL="http://localhost:8080/health"
MAX_WAIT_SECONDS=60
SLEEP_SECONDS=2

cleanup() {
    echo "[smoke] bajando la pila..."
    docker compose down --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "[smoke] construyendo y levantando la pila..."
docker compose up -d --build

echo -n "[smoke] esperando /health"
elapsed=0
while (( elapsed < MAX_WAIT_SECONDS )); do
    if curl -fsS -o /dev/null "$API_URL"; then
        echo
        echo "[smoke] OK — /health responde en $API_URL"
        echo "[smoke] respuesta:"
        curl -fsS "$API_URL"
        echo
        exit 0
    fi
    echo -n "."
    sleep "$SLEEP_SECONDS"
    elapsed=$(( elapsed + SLEEP_SECONDS ))
done

echo
echo "[smoke] FAIL — /health no respondió en ${MAX_WAIT_SECONDS}s"
echo "[smoke] logs del api (últimas 50 líneas):"
docker compose logs --tail=50 api || true
exit 1
