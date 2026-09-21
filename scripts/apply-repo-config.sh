#!/usr/bin/env bash
# ==============================================================================
# apply-repo-config.sh
# Aplica la configuracion esperada de ramas y entornos en GitHub API.
# Principio 3 de home-server: «La configuracion se versiona, no se hace clic».
# Referencia: docs/spec/feature-0011-devops-delivery-pipeline/spec.md sec. 5.2, 5.3
# ADR-0029, ADR-0030
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_FILE="$REPO_ROOT/.github/branch-protection.expected.json"

if ! command -v gh &>/dev/null; then
  echo "Error: gh CLI no está instalado o no se encuentra en el PATH." >&2
  exit 1
fi

if ! command -v python3 &>/dev/null; then
  echo "Error: python3 no está instalado o no se encuentra en el PATH." >&2
  exit 1
fi

REPO="${GITHUB_REPOSITORY:-$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || echo "JosephBano/erp-hacienda")}"
echo "==> Aplicando configuración declarada para: $REPO"

OWNER_ID="$(python3 -c "import json, subprocess, sys; res = json.loads(subprocess.check_output(['gh', 'api', f'repos/{sys.argv[1]}'])); print(res['owner']['id'])" "$REPO")"

# 1. Configurar protección de rama 'develop'
echo "--> Aplicando protección a rama 'develop'..."
gh api -X PUT "repos/$REPO/branches/develop/protection" \
  --input - <<EOF
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "backend-build-and-test",
      "field-app-ci",
      "admin-web-ci",
      "pr-hygiene",
      "tesis-privacy-guard"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": false,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": true,
  "lock_branch": false,
  "allow_fork_syncing": false
}
EOF
echo "    Protección de 'develop' aplicada."

# 2. Configurar protección de rama 'main'
echo "--> Aplicando protección a rama 'main'..."
gh api -X PUT "repos/$REPO/branches/main/protection" \
  --input - <<EOF
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "backend-build-and-test",
      "field-app-ci",
      "admin-web-ci",
      "pr-hygiene",
      "tesis-privacy-guard"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": false,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": true,
  "lock_branch": false,
  "allow_fork_syncing": false
}
EOF
echo "    Protección de 'main' aplicada."

# 3. Configurar entorno 'staging'
echo "--> Configurando entorno 'staging'..."
gh api -X PUT "repos/$REPO/environments/staging" \
  --input - <<EOF
{
  "deployment_branch_policy": {
    "protected_branches": false,
    "custom_branch_policies": true
  }
}
EOF

STAGING_POLICIES="$(python3 -c "import json, subprocess, sys; res = json.loads(subprocess.check_output(['gh', 'api', f'repos/{sys.argv[1]}/environments/staging/deployment-branch-policies'])); print('\n'.join(p.get('name','') for p in res.get('branch_policies', [])))" "$REPO" 2>/dev/null || true)"
if ! echo "$STAGING_POLICIES" | grep -qx "develop"; then
  gh api -X POST "repos/$REPO/environments/staging/deployment-branch-policies" -f name="develop"
fi
echo "    Entorno 'staging' configurado."

# 4. Configurar entorno 'production'
echo "--> Configurando entorno 'production'..."
gh api -X PUT "repos/$REPO/environments/production" \
  --input - <<EOF
{
  "reviewers": [
    {
      "type": "User",
      "id": $OWNER_ID
    }
  ],
  "deployment_branch_policy": {
    "protected_branches": false,
    "custom_branch_policies": true
  }
}
EOF

PROD_POLICIES="$(python3 -c "import json, subprocess, sys; res = json.loads(subprocess.check_output(['gh', 'api', f'repos/{sys.argv[1]}/environments/production/deployment-branch-policies'])); print('\n'.join(p.get('name','') for p in res.get('branch_policies', [])))" "$REPO" 2>/dev/null || true)"
if ! echo "$PROD_POLICIES" | grep -qx "main"; then
  gh api -X POST "repos/$REPO/environments/production/deployment-branch-policies" -f name="main"
fi
echo "    Entorno 'production' configurado."

echo "==> Configuración aplicada exitosamente y sin errores."
