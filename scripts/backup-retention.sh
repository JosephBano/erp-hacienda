#!/usr/bin/env bash
set -euo pipefail

# Retention operates only on complete backup sets. A valid set contains dump,
# checksum, manifest and completion marker.
if [ -f "/etc/hato-backup/backup.env" ]; then
    set -a
    # shellcheck disable=SC1091
    . "/etc/hato-backup/backup.env"
    set +a
fi

DRY_RUN=0
SCOPE=all
while [ "$#" -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1 ;;
        --local-only) SCOPE=local ;;
        --remote-only) SCOPE=remote ;;
        *) echo "Opción desconocida: $1" >&2; exit 1 ;;
    esac
    shift
done

BACKUP_DIR="${BACKUP_DIR:-/var/backups/hato-db}"
RCLONE_CMD="${RCLONE_CMD:-rclone}"
RCLONE_CONFIG="${RCLONE_CONFIG:-/etc/hato-backup/rclone.conf}"
RCLONE_REMOTE="${RCLONE_REMOTE:-hato-crypt}"
RETENTION_LOCAL_DAYS="${RETENTION_LOCAL_DAYS:-7}"
RETENTION_DAILY_DAYS="${RETENTION_REMOTE_DAILY_DAYS:-30}"
RETENTION_MONTHLY_DAYS=$(( ${RETENTION_REMOTE_MONTHLY_MONTHS:-12} * 30 ))

is_complete_local_set() {
    local dir="$1" stem="$2"
    [[ -f "$dir/$stem.dump" && -f "$dir/$stem.sha256" \
       && -f "$dir/$stem.manifest.json" && -f "$dir/$stem.complete" ]]
}

remove_local_set() {
    local dir="$1" stem="$2"
    rm -f -- "$dir/$stem.dump" "$dir/$stem.sha256" \
        "$dir/$stem.manifest.json" "$dir/$stem.complete"
}

backup_month() {
    local stem="$1" value="${stem#*-db-}"
    value="${value:0:6}"
    [[ "$value" =~ ^[0-9]{6}$ ]] || return 1
    printf '%s\n' "$value"
}

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Retención (dry-run=$DRY_RUN, scope=$SCOPE)"

if [ "$SCOPE" = all ] || [ "$SCOPE" = local ]; then
    if [ -d "$BACKUP_DIR" ]; then
        newest=""
        while IFS= read -r dump; do
            stem="$(basename "$dump" .dump)"
            if is_complete_local_set "$BACKUP_DIR" "$stem"; then newest="$dump"; break; fi
        done < <(find "$BACKUP_DIR" -name 'hato-*-db-*.dump' -type f -printf '%T@ %p\n' | sort -nr | awk '{print $2}')

        while IFS= read -r dump; do
            stem="$(basename "$dump" .dump)"
            is_complete_local_set "$BACKUP_DIR" "$stem" || continue
            [ "$dump" = "$newest" ] && continue
            if [ "$DRY_RUN" -eq 1 ]; then echo "[DRY-RUN] local prune $stem"; else remove_local_set "$BACKUP_DIR" "$stem"; fi
        done < <(find "$BACKUP_DIR" -name 'hato-*-db-*.dump' -type f -mtime +"$RETENTION_LOCAL_DAYS")
    fi
fi

if [ "$SCOPE" = all ] || [ "$SCOPE" = remote ]; then
    if [ -n "${REMOTE_DAILY_DIR:-}" ] && [ -d "${REMOTE_DAILY_DIR}" ]; then
        daily="$REMOTE_DAILY_DIR"
        monthly="${REMOTE_MONTHLY_DIR:-}"
        declare -A selected=()
        while IFS= read -r dump; do
            stem="$(basename "$dump" .dump)"
            is_complete_local_set "$daily" "$stem" || continue
            age=$(( ( $(date +%s) - $(stat -c%Y "$dump") ) / 86400 ))
            if [ "$age" -gt "$RETENTION_DAILY_DAYS" ]; then
                month="$(backup_month "$stem")" || continue
                candidate="${selected[$month]:-}"
                if [ -z "$candidate" ] || [ "$dump" -nt "$candidate" ]; then selected[$month]="$dump"; fi
            fi
        done < <(find "$daily" -name 'hato-prod-db-*.dump' -type f -printf '%T@ %p\n' | sort -nr | awk '{print $2}')

        for month in "${!selected[@]}"; do
            dump="${selected[$month]}"; stem="$(basename "$dump" .dump)"
            [ -n "$monthly" ] || continue
            mkdir -p "$monthly"
            if [ ! -f "$monthly/$stem.complete" ]; then
                if [ "$DRY_RUN" -eq 1 ]; then echo "[DRY-RUN] promote monthly $stem"; else cp -- "$daily/$stem".* "$monthly/"; fi
            fi
        done

        if [ -n "$monthly" ] && [ -d "$monthly" ]; then
            monthly_dumps=()
            while IFS= read -r dump; do
                stem="$(basename "$dump" .dump)"
                is_complete_local_set "$monthly" "$stem" || continue
                monthly_dumps+=("$dump")
            done < <(find "$monthly" -name 'hato-prod-db-*.dump' -type f -printf '%T@ %p\n' | sort -nr | awk '{print $2}')
            index=0
            for dump in "${monthly_dumps[@]}"; do
                index=$((index + 1)); age=$(( ( $(date +%s) - $(stat -c%Y "$dump") ) / 86400 ))
                if [ "$index" -gt 1 ] && [ "$age" -gt "$RETENTION_MONTHLY_DAYS" ]; then
                    stem="$(basename "$dump" .dump)"
                    if [ "$DRY_RUN" -eq 1 ]; then echo "[DRY-RUN] monthly prune $stem"; else remove_local_set "$monthly" "$stem"; fi
                fi
            done
        fi
    elif [ -f "$RCLONE_CONFIG" ] && command -v "$RCLONE_CMD" >/dev/null 2>&1; then
        daily="${RCLONE_REMOTE}:database/prod/daily"; monthly="${RCLONE_REMOTE}:database/prod/monthly"
        files="$($RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$daily" 2>/dev/null || true)"
        complete_dumps=()
        while IFS= read -r dump; do
            stem="${dump%.dump}"
            grep -Fxq "$stem.complete" <<<"$files" || continue
            grep -Fxq "$stem.sha256" <<<"$files" || continue
            grep -Fxq "$stem.manifest.json" <<<"$files" || continue
            complete_dumps+=("$dump")
        done < <(grep -E '^hato-prod-db-.*\.dump$' <<<"$files" | sort)

        declare -A selected_remote=()
        for dump in "${complete_dumps[@]}"; do
            stem="${dump%.dump}"; timestamp="$($RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$daily/$dump" --format t --time-format unix)"
            age=$(( ( $(date +%s) - timestamp ) / 86400 )); [ "$age" -gt "$RETENTION_DAILY_DAYS" ] || continue
            month="$(backup_month "$stem")" || continue; selected_remote[$month]="$dump"
        done
        for month in "${!selected_remote[@]}"; do
            dump="${selected_remote[$month]}"; stem="${dump%.dump}"
            if ! $RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$monthly/$stem.complete" >/dev/null 2>&1; then
                [ "$DRY_RUN" -eq 1 ] || $RCLONE_CMD --config "$RCLONE_CONFIG" copy "$daily" "$monthly" --include "$stem.*"
            fi
        done

        newest="${complete_dumps[${#complete_dumps[@]}-1]:-}"
        for dump in "${complete_dumps[@]}"; do
            [ "$dump" = "$newest" ] && continue
            stem="${dump%.dump}"; timestamp="$($RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$daily/$dump" --format t --time-format unix)"
            age=$(( ( $(date +%s) - timestamp ) / 86400 ))
            if [ "$age" -gt "$RETENTION_DAILY_DAYS" ]; then [ "$DRY_RUN" -eq 1 ] || $RCLONE_CMD --config "$RCLONE_CONFIG" delete "$daily" --include "$stem.*"; fi
        done

        # Apply the same 12-month policy to the monthly namespace. Preserve
        # the newest complete monthly set even if it is older than the window;
        # deleting the last known-good recovery point is never acceptable.
        monthly_files="$($RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$monthly" 2>/dev/null || true)"
        monthly_dumps=()
        while IFS= read -r dump; do
            stem="${dump%.dump}"
            grep -Fxq "$stem.complete" <<<"$monthly_files" || continue
            grep -Fxq "$stem.sha256" <<<"$monthly_files" || continue
            grep -Fxq "$stem.manifest.json" <<<"$monthly_files" || continue
            monthly_dumps+=("$dump")
        done < <(grep -E '^hato-prod-db-.*\.dump$' <<<"$monthly_files" | sort -r)
        monthly_index=0
        for dump in "${monthly_dumps[@]}"; do
            monthly_index=$((monthly_index + 1)); stem="${dump%.dump}"
            timestamp="$($RCLONE_CMD --config "$RCLONE_CONFIG" lsf "$monthly/$dump" --format t --time-format unix)"
            age=$(( ( $(date +%s) - timestamp ) / 86400 ))
            if [ "$monthly_index" -gt 1 ] && [ "$age" -gt "$RETENTION_MONTHLY_DAYS" ]; then
                [ "$DRY_RUN" -eq 1 ] || $RCLONE_CMD --config "$RCLONE_CONFIG" delete "$monthly" --include "$stem.*"
            fi
        done
    fi
fi

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Retención finalizada"
