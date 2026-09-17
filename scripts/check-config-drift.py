#!/usr/bin/env python3
"""
check-config-drift.py
Compara la configuracion de proteccion de ramas y entornos en GitHub API
contra la declaracion esperada en .github/branch-protection.expected.json.

ADR-0030 sec. 3 / D12 / spec.md sec. 5.6:
Este script DETECTA y DELATA la deriva de configuracion nombrando el campo
que cambio. En un repositorio de cuenta personal de GitHub no es posible
impedir tecnicamente que el propietario o un agente con su token altere la
configuracion; por tanto, este chequeo no afirma bloquear ni impedir el cambio,
sino hacerlo inmediatamente visible en rojo en el CI del siguiente PR.
"""

import json
import os
import subprocess
import sys

def run_cmd(cmd):
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return result.returncode, result.stdout.strip(), result.stderr.strip()

def get_repo():
    repo = os.environ.get("GITHUB_REPOSITORY")
    if repo:
        return repo
    rc, out, _ = run_cmd("gh repo view --json nameWithOwner --jq .nameWithOwner")
    if rc == 0 and out:
        return out
    return "JosephBano/erp-hacienda"

def gh_api_json(endpoint):
    rc, out, err = run_cmd(f"gh api {endpoint}")
    if rc != 0 or not out:
        return None
    try:
        return json.loads(out)
    except json.JSONDecodeError:
        return None

def main():
    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    config_file = os.path.join(repo_root, ".github", "branch-protection.expected.json")

    if not os.path.exists(config_file):
        print(f"::error::Archivo de configuración esperada no encontrado: {config_file}", file=sys.stderr)
        sys.exit(1)

    with open(config_file, "r", encoding="utf-8") as f:
        expected = json.load(f)

    repo = get_repo()
    print(f"==> Verificando deriva de configuración para: {repo}")

    diffs = []

    # 1. Verificar ramas
    for branch, branch_exp in expected.get("branches", {}).items():
        print(f"--> Consultando protección de rama '{branch}'...")
        api_data = gh_api_json(f"repos/{repo}/branches/{branch}/protection")
        if not api_data:
            # Fallback al endpoint de rama (disponible para GITHUB_TOKEN sin permisos admin)
            branch_data = gh_api_json(f"repos/{repo}/branches/{branch}")
            if not branch_data or not branch_data.get("protected"):
                diffs.append(f"[{branch}] Protección de rama no está habilitada en GitHub API.")
                continue

            print(f"  OK [{branch}]: branch.protected = True (verificado vía endpoint de rama)")
            prot = branch_data.get("protection", {})
            req_status = prot.get("required_status_checks", {})
            act_contexts = set(req_status.get("contexts", []))
            exp_contexts = set(branch_exp.get("required_status_checks", {}).get("contexts", []))
            missing = exp_contexts - act_contexts
            if missing:
                diffs.append(f"[{branch}] required_status_checks.contexts: faltan checks requeridos {sorted(missing)}")
            else:
                print(f"  OK [{branch}]: checks requeridos presentes ({sorted(exp_contexts)})")
            continue

        # enforce_admins
        act_enforce = api_data.get("enforce_admins", {}).get("enabled", False)
        exp_enforce = branch_exp.get("enforce_admins", False)
        if act_enforce != exp_enforce:
            diffs.append(f"[{branch}] enforce_admins.enabled: actual={act_enforce}, esperado={exp_enforce}")
        else:
            print(f"  OK [{branch}]: enforce_admins.enabled = {act_enforce}")

        # required_conversation_resolution
        act_conv = api_data.get("required_conversation_resolution", {}).get("enabled", False)
        exp_conv = branch_exp.get("required_conversation_resolution", False)
        if act_conv != exp_conv:
            diffs.append(f"[{branch}] required_conversation_resolution.enabled: actual={act_conv}, esperado={exp_conv}")
        else:
            print(f"  OK [{branch}]: required_conversation_resolution.enabled = {act_conv}")

        # required_linear_history
        act_linear = api_data.get("required_linear_history", {}).get("enabled", False)
        exp_linear = branch_exp.get("required_linear_history", False)
        if act_linear != exp_linear:
            diffs.append(f"[{branch}] required_linear_history.enabled: actual={act_linear}, esperado={exp_linear}")
        else:
            print(f"  OK [{branch}]: required_linear_history.enabled = {act_linear}")

        # allow_force_pushes
        act_force = api_data.get("allow_force_pushes", {}).get("enabled", False)
        exp_force = branch_exp.get("allow_force_pushes", False)
        if act_force != exp_force:
            diffs.append(f"[{branch}] allow_force_pushes.enabled: actual={act_force}, esperado={exp_force}")

        # allow_deletions
        act_del = api_data.get("allow_deletions", {}).get("enabled", False)
        exp_del = branch_exp.get("allow_deletions", False)
        if act_del != exp_del:
            diffs.append(f"[{branch}] allow_deletions.enabled: actual={act_del}, esperado={exp_del}")

        # required_approving_review_count
        reviews = api_data.get("required_pull_request_reviews", {})
        act_reviews = reviews.get("required_approving_review_count", 0)
        exp_reviews = branch_exp.get("required_approving_review_count", 0)
        if act_reviews != exp_reviews:
            diffs.append(f"[{branch}] required_pull_request_reviews.required_approving_review_count: actual={act_reviews}, esperado={exp_reviews}")
        else:
            print(f"  OK [{branch}]: required_approving_review_count = {act_reviews}")

        # required_status_checks
        exp_status = branch_exp.get("required_status_checks", {})
        act_status = api_data.get("required_status_checks")
        if not act_status:
            diffs.append(f"[{branch}] required_status_checks: no configurado en GitHub API (se esperaba estricto con checks obligatorios).")
        else:
            act_strict = act_status.get("strict", False)
            exp_strict = exp_status.get("strict", False)
            if act_strict != exp_strict:
                diffs.append(f"[{branch}] required_status_checks.strict: actual={act_strict}, esperado={exp_strict}")
            else:
                print(f"  OK [{branch}]: required_status_checks.strict = {act_strict}")

            act_contexts = set(act_status.get("contexts", []))
            exp_contexts = set(exp_status.get("contexts", []))
            missing = exp_contexts - act_contexts
            if missing:
                diffs.append(f"[{branch}] required_status_checks.contexts: faltan checks requeridos {sorted(missing)}")
            else:
                print(f"  OK [{branch}]: checks requeridos presentes ({sorted(exp_contexts)})")

    # 2. Verificar entornos
    print("--> Consultando entornos configurados...")
    envs_data = gh_api_json(f"repos/{repo}/environments")
    actual_envs = set()
    if envs_data and "environments" in envs_data:
        actual_envs = {e.get("name") for e in envs_data["environments"]}

    for env_name, env_exp in expected.get("environments", {}).items():
        if env_name not in actual_envs:
            diffs.append(f"[environments] El entorno '{env_name}' no existe en GitHub API.")
        else:
            print(f"  OK [environments]: entorno '{env_name}' existe.")

    # 3. Reporte final
    print("-" * 80)
    if diffs:
        print("================================================================================")
        print("ALERTA DE DERIVA DE CONFIGURACIÓN (ADR-0030 sec. 3 / D12)")
        print("Este job DETECTA y DELATA discrepancias entre la configuración esperada")
        print("en .github/branch-protection.expected.json y el estado real en GitHub API.")
        print("")
        print("NOTA: En un repositorio personal de GitHub, no es técnicamente posible impedir")
        print("que el propietario o un agente con credenciales administrativas modifique")
        print("la configuración. Este job NO afirma impedir el cambio, pero lo visibiliza")
        print("inmediatamente en el CI para asegurar que cualquier modificación no autorizada")
        print("o accidental sea detectada antes de llegar a producción.")
        print("")
        print("Para corregir la deriva y sincronizar el estado, ejecute:")
        print("  ./scripts/apply-repo-config.sh")
        print("================================================================================")
        print(f"Se detectaron {len(diffs)} diferencias:")
        for d in diffs:
            print(f"::error::{d}")
        sys.exit(1)

    print("OK: Configuración de ramas y entornos sin deriva respecto a .github/branch-protection.expected.json.")

if __name__ == "__main__":
    main()
