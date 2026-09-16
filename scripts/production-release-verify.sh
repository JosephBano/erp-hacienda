#!/usr/bin/env bash
# Smoke test post-deploy de producción: verifica health y que la versión
# desplegada corresponde EXACTAMENTE al SHA que se pretendía desplegar.
#
# A diferencia de scripts/smoke-api-container.sh (que solo confirma que el
# endpoint responde y, si puede, imprime /version de forma informativa), este
# script hace del match de SHA una condición de éxito/fracaso: spec.md sec. 6
# exige publicar "SOLO tras health, versión SHA y smoke autenticado". Un
# despliegue que arranca sano pero sirve el commit equivocado (p. ej. por un
# release anterior que no se reemplazó) debe fallar aquí, no pasar en silencio.
#
# Uso:
#   ./scripts/production-release-verify.sh <base_url> <sha_esperado>
#
#   base_url:      URL base de producción, alcanzable por Tailscale sobre HTTPS
#                   (ej. https://hato-prod.tailnetXXXX.ts.net:443). Sin barra final.
#   sha_esperado:  SHA completo (40 caracteres hex) del commit que se acaba de
#                   desplegar. Debe coincidir EXACTO con el campo "Commit" que
#                   reporta /version — no se acepta coincidencia parcial.

set -euo pipefail

MAX_WAIT_SECONDS="${MAX_WAIT_SECONDS:-120}"
SLEEP_SECONDS=3

fail() {
    echo "[production-release-verify] FAIL: $1" >&2
    exit 1
}

BASE_URL="${1:-}"
EXPECTED_SHA="${2:-}"

[ -n "$BASE_URL" ] || fail "falta el argumento base_url"
[ -n "$EXPECTED_SHA" ] || fail "falta el argumento sha_esperado"

# Formato conservador: SHA completo hexadecimal (40 caracteres), igual patrón
# que exige scripts/production-deploy-entry.sh para el campo "sha" del
# manifiesto. Un SHA corto o vacío aquí es un error del caller, no algo que
# este script deba adivinar o completar.
case "$EXPECTED_SHA" in
    [0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F])
        ;;
    *)
        fail "sha_esperado no tiene forma de SHA completo (40 hex): '$EXPECTED_SHA'"
        ;;
esac

command -v jq >/dev/null 2>&1 || fail "jq no está disponible (requerido para leer /version)"
command -v curl >/dev/null 2>&1 || fail "curl no está disponible"

HEALTH_URL="${BASE_URL%/}/health"
VERSION_URL="${BASE_URL%/}/version"

echo "[production-release-verify] esperando health en $HEALTH_URL ..."
elapsed=0
health_ok=0
while (( elapsed < MAX_WAIT_SECONDS )); do
    if curl -fsS -o /dev/null "$HEALTH_URL" 2>/dev/null; then
        health_ok=1
        break
    fi
    sleep "$SLEEP_SECONDS"
    elapsed=$(( elapsed + SLEEP_SECONDS ))
done

[ "$health_ok" -eq 1 ] || fail "el endpoint de health no respondió en ${MAX_WAIT_SECONDS}s ($HEALTH_URL). No se publica sin health verificado."

echo "[production-release-verify] health OK. Verificando versión reportada en $VERSION_URL ..."

version_body="$(curl -fsS "$VERSION_URL")" \
    || fail "el endpoint de versión no respondió ($VERSION_URL). No se publica sin confirmar el SHA desplegado."

printf '%s' "$version_body" | jq empty >/dev/null 2>&1 \
    || fail "la respuesta de $VERSION_URL no es JSON válido: $version_body"

# VersionEndpoints.cs (src/Hato.Api/Endpoints/VersionEndpoints.cs) responde
# {"version":...,"commit":...,"buildDate":...} (serialización camelCase por
# defecto de ASP.NET Core minimal APIs); se acepta también PascalCase por si
# la política de serialización cambia, sin adivinar nada más allá de eso.
reported_sha="$(printf '%s' "$version_body" | jq -r '.commit // .Commit // empty')"

[ -n "$reported_sha" ] || fail "la respuesta de /version no trae un campo commit/Commit: $version_body"

if [ "$reported_sha" != "$EXPECTED_SHA" ]; then
    fail "el SHA reportado por /version ($reported_sha) NO coincide con el SHA desplegado ($EXPECTED_SHA). Nunca se publica un release con versión sin confirmar — no hay fallback silencioso."
fi

echo "[production-release-verify] versión confirmada: $reported_sha coincide exacto con el release desplegado."
echo "[production-release-verify] OK — health y SHA verificados en $BASE_URL."
