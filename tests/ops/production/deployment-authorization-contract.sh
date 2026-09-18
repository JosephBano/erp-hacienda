#!/usr/bin/env bash
# Contract test for the production deployment authorization boundary.
# It intentionally has no network or secret dependency.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
entry="$repo_root/scripts/production-deploy-entry.sh"
workflow="$repo_root/.github/workflows/deploy-production.yml"
root_helper="$repo_root/scripts/production-deploy-root.sh"
pre_backup="$repo_root/scripts/production-backup-pre-release.sh"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

for required in run_id run_attempt authorization; do
    grep -Fq "\"$required\"" "$entry" \
        || fail "deploy-entry must require manifest field $required"
done

grep -Fq 'ssh-keygen -Y verify' "$entry" \
    || fail 'deploy-entry must verify the host-side deployment authorization signature'
grep -Fq 'DEPLOY_AUTHORIZATION_KEY' "$workflow" \
    || fail 'workflow must use the environment-scoped authorization signing key'
grep -Fq 'github.run_id' "$workflow" \
    || fail 'workflow must bind authorization to the GitHub run id'
grep -Fq 'github.run_attempt' "$workflow" \
    || fail 'workflow must bind authorization to the GitHub run attempt'
grep -Fq 'production-backup-pre-release' "$root_helper" \
    || fail 'root deploy helper must run the pre-release backup before compose'
grep -Fq 'pre-release backup is executed on the production host' "$workflow" \
    || fail 'workflow must document that pre-release backup runs on the production host'
grep -Fq 'ssh-keygen -E sha256 -lf -' "$workflow" \
    || fail 'workflow must compute the SSH fingerprint explicitly as SHA256'
grep -Fq 'getent ahosts' "$workflow" \
    || fail 'workflow must diagnose DEPLOY_HOST resolution before scanning'
grep -Fq 'candidate_fingerprint=' "$workflow" \
    || fail 'workflow must capture the candidate host fingerprint for diagnostics'
test -x "$pre_backup" || fail 'pre-release backup helper must be executable'
grep -Fq 'REMOTE_NAMESPACE=database/prod/pre-release' "$pre_backup" \
    || fail 'pre-release helper must use the dedicated remote namespace'

echo 'PASS: deployment authorization contract'
