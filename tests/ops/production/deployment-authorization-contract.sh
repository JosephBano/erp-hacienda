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

# La lista de firmantes la lee deploy-entry corriendo como hato-deploy, SIN sudo
# (spec D7). /etc/hato-production es 700 y guarda secretos, asi que hato-deploy no
# puede atravesarlo: mientras la lista vivio ahi, el despliegue fallaba con "no
# existe el archivo root-owned de firmantes autorizados" aunque el archivo existia
# y tenia 644 (visto en el run 35564486035). Debe vivir en el directorio publico.
signers_path="$(grep -E '^readonly AUTHORIZATION_ALLOWED_SIGNERS=' "$entry" | cut -d'"' -f2)"
[ -n "$signers_path" ] \
    || fail 'deploy-entry must declare AUTHORIZATION_ALLOWED_SIGNERS'
case "$signers_path" in
    /etc/hato-production/*)
        fail "allowed-signers must not live under the 700 secrets dir (got $signers_path); hato-deploy cannot traverse it"
        ;;
    /etc/hato-production-public/*) ;;
    *)
        fail "unexpected allowed-signers location: $signers_path"
        ;;
esac

bootstrap="$repo_root/ops/production/bootstrap.md"
grep -Fq 'install -d -o root -g root -m 755 /etc/hato-production-public' "$bootstrap" \
    || fail 'bootstrap must create the public trust dir traversable by hato-deploy'
grep -Fq '/etc/hato-production-public/deploy-authorization.allowed-signers' "$bootstrap" \
    || fail 'bootstrap must install allowed-signers into the public trust dir'

echo 'PASS: deployment authorization contract'
