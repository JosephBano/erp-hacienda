#!/usr/bin/env bash
# deploy-root — helper root-owned invocado únicamente por deploy-entry vía sudo.
#
# Se instala como /usr/local/libexec/hato/deploy-root (root:root, 755, no escribible
# por hato-deploy). La única vía de invocación autorizada es la regla exacta en
# ops/production/sudoers-hato: `hato-deploy ALL=(root) NOPASSWD:
# /usr/local/libexec/hato/deploy-root`, a su vez llamada solo por
# scripts/production-deploy-entry.sh.
#
# Este script NO ejecuta shell recibido del caller ni acepta compose arbitrario: lee
# la petición ya validada (release, sha, digests) por stdin y opera solo sobre nombres
# de archivo y constantes fijas en este script.
#
# Ver ops/production/bootstrap.md para el procedimiento de instalación completo.

set -euo pipefail

# TODO: reemplazar con la ruta real donde vive compose.production.yml en el host de
# producción (spec.md §4: "releases fuera de pgdata"). Este archivo aún no existe en
# este commit — la Entrega 3 de este plan lo crea. Aquí solo se referencia el nombre
# esperado, sin asumir su contenido.
readonly COMPOSE_FILE="/srv/hato-production/releases/current/compose.production.yml"
readonly COMPOSE_PROJECT="hato-production"

# TODO: reemplazar con la ruta real del archivo de estado que registra la última
# versión desplegada con éxito (fuera del checkout, análogo a /etc/hato-production).
readonly STATE_FILE="/etc/hato-production/last-deployed-release"

# TODO: reemplazar con la ruta real de la bitácora de despliegues. Nunca debe
# contener secretos (spec.md línea 148): solo release, sha y resultado.
readonly LOG_FILE="/var/log/hato-production/deploy.log"

log() {
    # Cada línea de log incluye únicamente release/sha/resultado, nunca secretos ni
    # cadenas de conexión. No usar `echo "$request"` en ningún punto de este script.
    printf '%s [deploy-root] %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$1" | tee -a "$LOG_FILE" >&2
}

fail() {
    log "fallo: $1"
    exit 1
}

# --- verificación del manifiesto de release --------------------------------------
#
# TODO: la verificación real contra la aprobación de GitHub Environments (que el
# release/sha/digests corresponde efectivamente a un run de CI aprobado para
# `production`) se define en la Entrega 4 de este mismo plan. Esta entrega solo deja
# el contrato/interfaz — la firma de la función y el punto donde se invoca — sin la
# integración completa con GitHub Environments todavía. No desplegar en producción
# real hasta que esta función haga la verificación real.
verify_release_manifest() {
    local release="$1"
    local sha="$2"
    local digests_json="$3"

    if [ -z "$release" ] || [ -z "$sha" ] || [ -z "$digests_json" ]; then
        return 1
    fi

    # TODO(Entrega 4): consultar la API de GitHub para confirmar que $sha tiene un
    # run de CI exitoso y una aprobación registrada del entorno `production` antes
    # de continuar. Por ahora, placeholder que solo exige campos no vacíos.
    return 0
}

# --- lectura de la petición ya validada por deploy-entry --------------------------

command -v jq >/dev/null 2>&1 || fail "jq no está disponible en este host"

request_raw="$(cat)"
[ -n "$request_raw" ] || fail "petición vacía recibida de deploy-entry"

printf '%s' "$request_raw" | jq empty >/dev/null 2>&1 || fail "petición no es JSON válido"

release="$(printf '%s' "$request_raw" | jq -r '.release // empty')"
sha="$(printf '%s' "$request_raw" | jq -r '.sha // empty')"
digests_json="$(printf '%s' "$request_raw" | jq -c '.digests // empty')"

[ -n "$release" ] && [ -n "$sha" ] && [ -n "$digests_json" ] || fail "petición incompleta"

verify_release_manifest "$release" "$sha" "$digests_json" \
    || fail "el manifiesto de release no pasó la verificación"

log "iniciando despliegue release=$release sha=$sha"

if [ -f "$STATE_FILE" ]; then
    previous_release="$(cat "$STATE_FILE" 2>/dev/null || echo 'desconocida')"
else
    previous_release='ninguna'
fi
log "versión previa registrada: $previous_release"

if [ ! -f "$COMPOSE_FILE" ]; then
    fail "no se encontró $COMPOSE_FILE (aún no provisto por la Entrega 3 de este plan)"
fi

# Placeholder de logging hasta la Entrega 3 (compose.production.yml): esta función
# TODAVÍA NO exporta nada al entorno ni interpola compose — solo deja constancia en
# el log de qué pares imagen -> digesto ya validados se recibieron. La Entrega 3
# define cómo estos digestos llegan realmente a compose (variables de entorno por
# archivo, nunca como argumento de línea de comandos: un argumento de proceso es
# visible para cualquier usuario con `ps`, y las cadenas de conexión a la base de
# datos nunca deben pasar por ahí — spec.md §5). Sin `eval`, sin interpolación de
# comandos.
log_digest_manifest() {
    local entries
    entries="$(printf '%s' "$digests_json" | jq -r 'to_entries[] | "\(.key)=\(.value)"')"
    while IFS='=' read -r image digest; do
        [ -n "$image" ] || continue
        log "usando $image@$digest"
    done <<< "$entries"
}
log_digest_manifest

# docker compose no recibe shell ni compose arbitrario del caller: siempre el mismo
# archivo referenciado arriba, siempre el mismo proyecto fijo.
if docker compose -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" pull \
    && docker compose -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" up -d; then
    printf '%s\n' "$release" > "$STATE_FILE"
    log "despliegue exitoso release=$release sha=$sha"
else
    fail "docker compose pull/up falló para release=$release sha=$sha"
fi
