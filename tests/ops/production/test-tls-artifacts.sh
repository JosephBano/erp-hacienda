#!/usr/bin/env bash
# Verificaciones estáticas de TLS.1/TLS.2.
# No requiere Docker, Tailscale ni credenciales de producción.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
readonly CADDYFILE="$ROOT/ops/production/Caddyfile.production"
readonly RENEW_SCRIPT="$ROOT/scripts/production-tls-renew.sh"
readonly MONITOR_SCRIPT="$ROOT/scripts/production-tls-monitor.sh"
readonly RENEW_SERVICE="$ROOT/ops/production/hato-tls-renew.service"
readonly RENEW_TIMER="$ROOT/ops/production/hato-tls-renew.timer"
readonly MONITOR_SERVICE="$ROOT/ops/production/hato-tls-monitor.service"
readonly MONITOR_TIMER="$ROOT/ops/production/hato-tls-monitor.timer"

failures=0

require_file() {
  local file="$1"
  if [[ ! -f "$file" ]]; then
    printf 'missing required TLS artifact: %s\n' "$file" >&2
    failures=$((failures + 1))
  fi
}

require_contains() {
  local file="$1" pattern="$2"
  if ! grep -Fq -- "$pattern" "$file"; then
    printf 'expected %s in %s\n' "$pattern" "$file" >&2
    failures=$((failures + 1))
  fi
}

require_not_contains() {
  local file="$1" pattern="$2"
  if grep -Fq -- "$pattern" "$file"; then
    printf 'did not expect %s in %s\n' "$pattern" "$file" >&2
    failures=$((failures + 1))
  fi
}

require_file "$CADDYFILE"
require_file "$RENEW_SCRIPT"
require_file "$MONITOR_SCRIPT"
require_file "$RENEW_SERVICE"
require_file "$RENEW_TIMER"
require_file "$MONITOR_SERVICE"
require_file "$MONITOR_TIMER"

if (( failures == 0 )); then
  require_contains "$CADDYFILE" 'admin unix//data/caddy-admin.sock'
  require_contains "$CADDYFILE" 'auto_https off'
  require_contains "$CADDYFILE" 'tls /data/tls/server.crt /data/tls/server.key'
  require_contains "$CADDYFILE" 'reverse_proxy api:8080'

  require_contains "$RENEW_SCRIPT" 'tailscale cert'
  require_contains "$RENEW_SCRIPT" '-checkend "$validity_seconds"'
  require_contains "$RENEW_SCRIPT" 'caddy validate'
  require_contains "$RENEW_SCRIPT" 'caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile --address unix//data/caddy-admin.sock'
  require_contains "$RENEW_SCRIPT" '-m 640'
  require_contains "$RENEW_SCRIPT" 'HATO_TLS_HOSTNAME'
  require_contains "$RENEW_SCRIPT" 'temporary_directory=""'
  require_contains "$RENEW_SCRIPT" 'trap cleanup EXIT'
  require_not_contains "$RENEW_SCRIPT" '-g caddy'

  require_contains "$MONITOR_SCRIPT" 'readonly MINIMUM_VALIDITY_DAYS=21'
  require_contains "$MONITOR_SCRIPT" 'tls-alert-url'
  require_contains "$MONITOR_SCRIPT" '/fail'

  require_contains "$RENEW_TIMER" 'OnCalendar=weekly'
  require_contains "$MONITOR_TIMER" 'OnCalendar=daily'
  require_contains "$RENEW_SERVICE" 'User=root'
  require_contains "$MONITOR_SERVICE" 'User=root'

  bash -n "$RENEW_SCRIPT"
  bash -n "$MONITOR_SCRIPT"
fi

if (( failures > 0 )); then
  exit 1
fi

printf 'TLS production artifact checks passed.\n'
