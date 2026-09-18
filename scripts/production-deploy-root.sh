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
readonly PRODUCTION_ENV_FILE="/etc/hato-production/production.env"
readonly RELEASE_ENV_DIRECTORY="/run/hato-production"
readonly RELEASE_ENV_FILE="${RELEASE_ENV_DIRECTORY}/release.env"
readonly CONSUMED_AUTHORIZATIONS_DIRECTORY="/var/lib/hato-production/consumed-deployments"

# Deben coincidir con la allowlist instalada en deploy-entry. No se acepta que
# un manifiesto elija un registro o nombre de imagen distinto.
readonly API_IMAGE="ghcr.io/josephbano/hato-api"
readonly MIGRATE_IMAGE="ghcr.io/josephbano/hato-migrate"
readonly PRE_RELEASE_BACKUP_HELPER="/usr/local/libexec/hato/production-backup-pre-release"

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
# DECISIÓN (Entrega 4, .github/workflows/deploy-production.yml): este servidor NO
# vuelve a consultar la API de GitHub para confirmar CI/aprobación. La verificación
# real ya ocurrió antes de que este manifiesto pudiera siquiera llegar por SSH:
#
#   1. El job `resolve-and-build` de deploy-production.yml exige, vía `gh api`, una
#      ejecución con conclusion=="success" de ci.yml para el SHA exacto solicitado
#      (nunca asume éxito si la consulta falla).
#   2. El job `deploy` de ese mismo workflow tiene `environment: production`, que
#      GitHub pausa hasta que un revisor humano lo aprueba (gate declarado en
#      .github/branch-protection.expected.json, required_reviewers: true).
#   3. La ÚNICA clave privada capaz de autenticar como hato-deploy
#      (secrets.DEPLOY_SSH_KEY) vive exclusivamente en el GitHub Environment
#      `production` y solo el runner de ese job aprobado puede leerla. sshd en este
#      host además fuerza ForceCommand=deploy-entry (ops/production/sshd-hato.conf):
#      no hay ninguna otra vía de entrada que acepte un manifiesto.
#
# En otras palabras: la posesión misma de una sesión SSH autenticada como
# hato-deploy que llega hasta aquí YA ES la prueba de que 1) y 2) ocurrieron — no
# existe un camino donde un manifiesto llegue a este script sin haber pasado el gate
# aprobado. Repetir la consulta a la API de GitHub desde el servidor no añadiría
# garantía adicional (este host no tiene ni debe tener credenciales propias para
# hablar con la API de GitHub — spec D7, sin acceso amplio) y sería redundante con
# la custodia exclusiva de la clave SSH. Este análisis asume que
# .github/workflows/deploy-production.yml no fue alterado por un cambio no aprobado
# (protegido por branch protection en main) y que DEPLOY_SSH_KEY no se filtró — si
# alguna de esas dos premisas se rompe, la verificación de campos no vacíos de abajo
# tampoco lo detectaría; el control real contra eso es la custodia de secretos de
# GitHub Environments, fuera del alcance de este script.
#
# verify_release_manifest() por tanto queda como una comprobación de defensa en
# profundidad de forma del payload (no de autorización): rechaza un manifiesto con
# campos vacíos, pero la decisión de "sha/digests corresponden a un release
# aprobado" ya se tomó, y se tomó, en GitHub antes de llegar aquí.
verify_release_manifest() {
    local release="$1"
    local sha="$2"
    local digests_json="$3"

    if ! [[ "$release" =~ ^[A-Za-z0-9._-]{1,128}$ ]] \
        || ! [[ "$sha" =~ ^[0-9a-f]{40}$ ]]; then
        return 1
    fi

    # deploy-root mantiene esta comprobación aun cuando deploy-entry ya hizo
    # una allowlist: sudo no convierte esa primera validación en una frontera de
    # seguridad suficiente. Deben venir exactamente API y migrador, una vez cada
    # una, como strings sha256 canónicos; ni imágenes omitidas ni extras llegan a
    # Compose.
    printf '%s' "$digests_json" | jq -e \
        --arg api_image "$API_IMAGE" \
        --arg migrate_image "$MIGRATE_IMAGE" \
        '
          type == "object"
          and (keys | sort) == ([$api_image, $migrate_image] | sort)
          and (.[$api_image] | type == "string" and test("^sha256:[0-9a-f]{64}$"))
          and (.[$migrate_image] | type == "string" and test("^sha256:[0-9a-f]{64}$"))
        ' >/dev/null
}

# --- lectura de la petición ya validada por deploy-entry --------------------------

command -v jq >/dev/null 2>&1 || fail "jq no está disponible en este host"

request_raw="$(cat)"
[ -n "$request_raw" ] || fail "petición vacía recibida de deploy-entry"

printf '%s' "$request_raw" | jq empty >/dev/null 2>&1 || fail "petición no es JSON válido"

release="$(printf '%s' "$request_raw" | jq -r '.release // empty')"
sha="$(printf '%s' "$request_raw" | jq -r '.sha // empty')"
run_id="$(printf '%s' "$request_raw" | jq -r '.run_id // empty')"
run_attempt="$(printf '%s' "$request_raw" | jq -r '.run_attempt // empty')"
digests_json="$(printf '%s' "$request_raw" | jq -c '.digests // empty')"

[ -n "$release" ] && [ -n "$sha" ] && [ -n "$run_id" ] && [ -n "$run_attempt" ] && [ -n "$digests_json" ] || fail "petición incompleta"

if ! [[ "$run_id" =~ ^[1-9][0-9]{0,19}$ ]] || ! [[ "$run_attempt" =~ ^[1-9][0-9]{0,9}$ ]]; then
    fail "run_id o run_attempt con formato inválido"
fi

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

if [ ! -f "$PRODUCTION_ENV_FILE" ] || [ -L "$PRODUCTION_ENV_FILE" ]; then
    fail "no se encontró un production.env regular en la ruta protegida"
fi

if [ "$(stat -c '%a:%U:%G' "$PRODUCTION_ENV_FILE")" != "600:root:root" ]; then
    fail "production.env debe ser root:root con modo 600"
fi

# Una firma válida se consume una vez, antes de que Compose pueda cambiar el stack.
# Un reintento debe crear un nuevo GitHub run attempt, firmado de nuevo, en vez de
# repetir una orden anterior sobre datos que quizá ya migraron.
install -d -o root -g root -m 700 "$CONSUMED_AUTHORIZATIONS_DIRECTORY"
authorization_receipt="${CONSUMED_AUTHORIZATIONS_DIRECTORY}/${run_id}-${run_attempt}"
if ! (umask 077; set -C; : > "$authorization_receipt") 2>/dev/null; then
    fail "autorización ya consumida (posible replay)"
fi
printf '%s %s %s\n' "$sha" "$release" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" > "$authorization_receipt"

api_digest="$(printf '%s' "$digests_json" | jq -r --arg image "$API_IMAGE" '.[$image]')"
migrate_digest="$(printf '%s' "$digests_json" | jq -r --arg image "$MIGRATE_IMAGE" '.[$image]')"

# El env-file de release no contiene secretos: solo identificadores inmutables
# públicos del artefacto aprobado. Aun así se mantiene root-only, fuera del
# checkout y fuera de argv/logs. Se reemplaza atómicamente para no dejar a
# Compose observar un manifiesto parcial.
install -d -o root -g root -m 700 "$RELEASE_ENV_DIRECTORY"
release_env_tmp="$(mktemp "$RELEASE_ENV_DIRECTORY/release.env.XXXXXX")"
chmod 600 "$release_env_tmp"
{
    printf 'HATO_API_IMAGE=%s\n' "$API_IMAGE"
    printf 'HATO_API_DIGEST=%s\n' "$api_digest"
    printf 'HATO_MIGRATE_IMAGE=%s\n' "$MIGRATE_IMAGE"
    printf 'HATO_MIGRATE_DIGEST=%s\n' "$migrate_digest"
    printf 'HATO_VERSION=%s\n' "$release"
    printf 'HATO_COMMIT=%s\n' "$sha"
    printf 'HATO_BUILD_DATE=%s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
} > "$release_env_tmp"
mv -f "$release_env_tmp" "$RELEASE_ENV_FILE"

log "artefactos aprobados: api=$API_IMAGE@$api_digest migrador=$MIGRATE_IMAGE@$migrate_digest"

# D6: the backup is executed on the production host, where the protected DB and
# Drive credentials exist. The GitHub runner must never pretend it can perform
# this operation remotely. A missing helper is a hard failure before Compose.
[ -x "$PRE_RELEASE_BACKUP_HELPER" ] || fail "no existe el helper root-owned de backup pre-release"
RELEASE_SHA="$sha" "$PRE_RELEASE_BACKUP_HELPER" || fail "backup pre-release no verificado"

# docker compose no recibe shell ni compose arbitrario del caller: siempre el mismo
# archivo, project y env-files root-owned. `--no-build` es una segunda barrera:
# aun si alguien reintrodujera una sección build por error, la VPS la rechaza.
if docker compose --env-file "$PRODUCTION_ENV_FILE" --env-file "$RELEASE_ENV_FILE" \
    -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" pull \
    && docker compose --env-file "$PRODUCTION_ENV_FILE" --env-file "$RELEASE_ENV_FILE" \
        -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE" up -d --no-build; then
    printf '%s\n' "$release" > "$STATE_FILE"
    log "despliegue exitoso release=$release sha=$sha"
else
    fail "docker compose pull/up falló para release=$release sha=$sha"
fi
