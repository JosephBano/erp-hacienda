#!/usr/bin/env bash
# Comprueba a diario que el certificado TLS aún tiene al menos 21 días de validez.
# La URL de alerta es un secreto root-owned y nunca se imprime.
set -euo pipefail

readonly CADDY_CONTAINER="${HATO_CADDY_CONTAINER:-hato-production-proxy}"
readonly ALERT_URL_FILE="${HATO_TLS_ALERT_URL_FILE:-/etc/hato-production/tls-alert-url}"
readonly MINIMUM_VALIDITY_DAYS=21

fail() {
    printf 'production TLS monitor failed: %s\n' "$1" >&2
    exit 1
}

send_alert() {
    [[ -r "$ALERT_URL_FILE" ]] || return 0
    local alert_url
    alert_url="$(<"$ALERT_URL_FILE")"
    [[ -n "$alert_url" ]] || return 0
    # Healthchecks recibe un ping de fallo; ni la URL secreta ni datos de la finca
    # entran al log de systemd.
    curl --fail --silent --show-error --max-time 15 --output /dev/null \
        --request POST "${alert_url%/}/fail" || true
}

main() {
    [[ "$(id -u)" == "0" ]] || fail "must run as root"
    command -v docker >/dev/null 2>&1 || fail "required command is unavailable"
    command -v openssl >/dev/null 2>&1 || fail "required command is unavailable"
    command -v curl >/dev/null 2>&1 || fail "required command is unavailable"

    local temporary_directory certificate validity_seconds
    temporary_directory="$(mktemp -d /run/hato-tls-monitor.XXXXXX)"
    trap 'rm -rf "$temporary_directory"' EXIT
    certificate="$temporary_directory/server.crt"
    validity_seconds=$((MINIMUM_VALIDITY_DAYS * 24 * 60 * 60))

    if ! docker cp "$CADDY_CONTAINER:/data/tls/server.crt" "$certificate" 2>/dev/null ||
       ! openssl x509 -in "$certificate" -noout -checkend "$validity_seconds" >/dev/null 2>&1; then
        send_alert
        fail "certificate is absent or expires in fewer than ${MINIMUM_VALIDITY_DAYS} days"
    fi
}

main "$@"
