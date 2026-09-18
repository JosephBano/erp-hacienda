#!/usr/bin/env bash
set -euo pipefail

# Runs on the production host, immediately before a release can mutate the
# database. It deliberately does not accept a database, remote or path from the
# caller; those values come from the protected backup environment and constants.

BACKUP_SCRIPT="${HATO_BACKUP_SCRIPT:-/usr/local/libexec/hato/backup.sh}"
UPLOAD_SCRIPT="${HATO_UPLOAD_SCRIPT:-/usr/local/libexec/hato/backup-upload.sh}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"

[[ "$(id -u)" == 0 ]] || { echo "pre-release backup must run as root" >&2; exit 1; }
[[ -x "$BACKUP_SCRIPT" && -x "$UPLOAD_SCRIPT" ]] || {
    echo "backup helpers are not installed" >&2
    exit 1
}

export ENVIRONMENT=production
export BACKUP_DIR
export REMOTE_NAMESPACE=database/prod/pre-release
export RETENTION_REMOTE_DAILY_DAYS="${RETENTION_PRE_RELEASE_DAYS:-30}"

"$BACKUP_SCRIPT"
latest="$(find "$BACKUP_DIR" -maxdepth 1 -name 'hato-production-db-*.dump' -type f -printf '%T@ %p\n' | sort -nr | awk 'NR==1 {print $2}')"
[[ -n "$latest" && -f "$latest" ]] || { echo "pre-release dump was not created" >&2; exit 1; }
"$UPLOAD_SCRIPT" "$latest"
