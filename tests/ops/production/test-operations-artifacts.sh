#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
logrotate_file="$root/ops/production/hato-production.logrotate"

[[ -f "$logrotate_file" ]] || {
    echo 'FAIL: missing production log rotation policy' >&2
    exit 1
}

grep -Fq '/var/log/hato-production/deploy.log' "$logrotate_file"
grep -Fq 'weekly' "$logrotate_file"
grep -Fq 'rotate 14' "$logrotate_file"
grep -Fq 'create 0640 root root' "$logrotate_file"

echo 'PASS: production operations artifacts'
