#!/usr/bin/env bash
# Emite y rota el certificado TLS privado de producción sin exponer la clave al
# usuario de despliegue. Debe instalarse root:root 0750 y ejecutarse por systemd.
set -euo pipefail

readonly CADDY_CONTAINER="${HATO_CADDY_CONTAINER:-hato-production-proxy}"
readonly CADDY_VOLUME="${HATO_CADDY_VOLUME:-hato-production-caddy-data}"
readonly CADDY_IMAGE="${HATO_CADDY_IMAGE:-caddy:2-alpine}"
readonly CADDYFILE="${HATO_CADDYFILE:-/srv/hato-production/releases/current/ops/production/Caddyfile.production}"
readonly CERTIFICATE_VALIDITY_BUFFER_DAYS=21
temporary_directory=""

fail() {
    printf 'production TLS renewal failed: %s\n' "$1" >&2
    exit 1
}

cleanup() {
    if [[ -n "$temporary_directory" ]]; then
        rm -rf -- "$temporary_directory"
    fi
}

trap cleanup EXIT

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "required command is unavailable"
}

is_proxy_running() {
    [[ "$(docker inspect --format '{{.State.Running}}' "$CADDY_CONTAINER" 2>/dev/null || true)" == "true" ]]
}

validate_certificate_pair() {
    local certificate="$1" key="$2" hostname="$3"
    local validity_seconds=$((CERTIFICATE_VALIDITY_BUFFER_DAYS * 24 * 60 * 60))

    openssl x509 -in "$certificate" -noout -checkhost "$hostname" >/dev/null 2>&1 ||
        fail "issued certificate does not cover the configured hostname"
    openssl x509 -in "$certificate" -noout -checkend "$validity_seconds" >/dev/null 2>&1 ||
        fail "issued certificate expires too soon"
    cmp -s \
        <(openssl x509 -in "$certificate" -noout -pubkey | openssl pkey -pubin -outform PEM) \
        <(openssl pkey -in "$key" -pubout -outform PEM) ||
        fail "issued certificate and private key do not match"
}

validate_caddy_configuration() {
    local staging_directory="$1"
    docker run --rm --user root \
        --mount "type=volume,source=$CADDY_VOLUME,target=/data,volume-nocopy" \
        --mount "type=bind,source=$CADDYFILE,target=/etc/caddy/Caddyfile,readonly" \
        --mount "type=bind,source=$staging_directory,target=/incoming,readonly" \
        --entrypoint sh "$CADDY_IMAGE" -ec '
            trap "rm -f /tmp/Caddyfile.candidate /data/tls/server.crt.candidate /data/tls/server.key.candidate" EXIT
            install -d -o root -g root -m 750 /data/tls
            install -o root -g root -m 640 /incoming/server.crt /data/tls/server.crt.candidate
            install -o root -g root -m 640 /incoming/server.key /data/tls/server.key.candidate
            sed \
                -e "s#/data/tls/server.crt#/data/tls/server.crt.candidate#g" \
                -e "s#/data/tls/server.key#/data/tls/server.key.candidate#g" \
                /etc/caddy/Caddyfile > /tmp/Caddyfile.candidate
            caddy validate --config /tmp/Caddyfile.candidate --adapter caddyfile
        '
}

install_and_reload() {
    local staging_directory="$1"

    # La validación usa rutas candidatas; no sustituye el par activo si el
    # Caddyfile no puede cargar el certificado recién emitido.
    validate_caddy_configuration "$staging_directory"

    if ! is_proxy_running; then
        # Primer aprovisionamiento: el volumen existe antes del primer `compose up`.
        # No hay proceso Caddy que recargar todavía; la siguiente inicialización lo lee.
        docker run --rm --user root \
            --mount "type=volume,source=$CADDY_VOLUME,target=/data,volume-nocopy" \
            --mount "type=bind,source=$staging_directory,target=/incoming,readonly" \
            --entrypoint sh "$CADDY_IMAGE" -ec '
                install -d -o root -g root -m 750 /data/tls
                install -o root -g root -m 640 /incoming/server.crt /data/tls/server.crt.next
                install -o root -g root -m 640 /incoming/server.key /data/tls/server.key.next
                mv -f /data/tls/server.crt.next /data/tls/server.crt
                mv -f /data/tls/server.key.next /data/tls/server.key
            '
        return
    fi

    docker cp "$CADDY_CONTAINER:/data/tls/server.crt" "$staging_directory/previous.crt" 2>/dev/null || true
    docker cp "$CADDY_CONTAINER:/data/tls/server.key" "$staging_directory/previous.key" 2>/dev/null || true
    docker cp "$staging_directory/server.crt" "$CADDY_CONTAINER:/data/tls/server.crt.incoming"
    docker cp "$staging_directory/server.key" "$CADDY_CONTAINER:/data/tls/server.key.incoming"

    docker exec --user root "$CADDY_CONTAINER" sh -ec '
        install -d -o root -g root -m 750 /data/tls
        install -o root -g root -m 640 /data/tls/server.crt.incoming /data/tls/server.crt.next
        install -o root -g root -m 640 /data/tls/server.key.incoming /data/tls/server.key.next
        rm -f /data/tls/server.crt.incoming /data/tls/server.key.incoming
        mv -f /data/tls/server.crt.next /data/tls/server.crt
        mv -f /data/tls/server.key.next /data/tls/server.key
        caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
    '

    if ! docker exec "$CADDY_CONTAINER" \
        caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile --address unix//data/caddy-admin.sock; then
        # La renovación no deja un par nuevo activo si la recarga falla. El par
        # anterior solo se restaura cuando existía; un primer arranque no llega aquí.
        if [[ -f "$staging_directory/previous.crt" && -f "$staging_directory/previous.key" ]]; then
            docker cp "$staging_directory/previous.crt" "$CADDY_CONTAINER:/data/tls/server.crt.rollback"
            docker cp "$staging_directory/previous.key" "$CADDY_CONTAINER:/data/tls/server.key.rollback"
            docker exec --user root "$CADDY_CONTAINER" sh -ec '
                install -o root -g root -m 640 /data/tls/server.crt.rollback /data/tls/server.crt
                install -o root -g root -m 640 /data/tls/server.key.rollback /data/tls/server.key
                rm -f /data/tls/server.crt.rollback /data/tls/server.key.rollback
            '
            docker exec "$CADDY_CONTAINER" \
                caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile --address unix//data/caddy-admin.sock || true
        fi
        fail "Caddy reload failed; previous certificate was restored when available"
    fi
}

main() {
    [[ "$(id -u)" == "0" ]] || fail "must run as root"
    : "${HATO_TLS_HOSTNAME:?HATO_TLS_HOSTNAME must be supplied by systemd EnvironmentFile}"
    [[ -f "$CADDYFILE" ]] || fail "installed Caddyfile is missing"
    require_command docker
    require_command openssl
    require_command tailscale

    temporary_directory="$(mktemp -d /run/hato-tls.XXXXXX)"
    umask 077

    tailscale cert --cert-file "$temporary_directory/server.crt" \
        --key-file "$temporary_directory/server.key" "$HATO_TLS_HOSTNAME" ||
        fail "tailscale certificate issuance failed; existing certificate was not changed"
    validate_certificate_pair "$temporary_directory/server.crt" "$temporary_directory/server.key" "$HATO_TLS_HOSTNAME"
    install_and_reload "$temporary_directory"
    printf 'production TLS certificate renewed successfully.\n'
}

main "$@"
