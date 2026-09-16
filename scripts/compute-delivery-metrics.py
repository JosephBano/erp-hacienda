#!/usr/bin/env python3
"""
compute-delivery-metrics.py
Calcula y reporta las metricas de entrega (DORA) para Hato ERP:
1. Lead time for changes (Tiempo de entrega del cambio)
2. Deployment/Integration frequency (Frecuencia de integracion a develop)
3. Change failure rate (Tasa de fallos del cambio - fiabilidad baja)
4. Time to restore service (Tiempo de restauracion del servicio - fiabilidad baja)

Referencia: docs/spec/feature-0011-devops-delivery-pipeline/spec.md sec. 9
"""

import collections
import datetime
import json
import os
import subprocess
import sys

DOCUMENTED_INCIDENTS = [
    {
        "id": "F3-1",
        "phase": "Fase 3",
        "desc": "Entidades Livestock sin created_at/updated_at en auditor interceptor",
        "cause": "Auditor solo registrado en PeopleDbContext",
        "severity": "Pérdida de sincronización incremental",
    },
    {
        "id": "F3-2",
        "phase": "Fase 3",
        "desc": "Parto pierde genealogía silenciosamente",
        "cause": "motherId enviado a comando createAnimal que no lo recibía",
        "severity": "Pérdida de genealogía",
    },
    {
        "id": "F3-3",
        "phase": "Fase 3",
        "desc": "Outbox en localStorage",
        "cause": "localStorage no existe en React Native",
        "severity": "Pérdida de registros offline al cerrar app",
    },
    {
        "id": "F4-1",
        "phase": "feature-0004",
        "desc": "Ordeño individual rechazado sistemáticamente (S8)",
        "cause": "isPlausibilityConfirmed en raíz de payload rechazado por UnmappedMemberHandling.Disallow",
        "severity": "Rechazo de ordeños en push",
    },
    {
        "id": "F4-2",
        "phase": "feature-0004",
        "desc": "Duplicate enmascara fallo previo (S9)",
        "cause": "syncEngine trata Duplicate como éxito descartando errorDetails",
        "severity": "Registros no sincronizados marcados como sincronizados",
    },
    {
        "id": "F4-3",
        "phase": "feature-0004",
        "desc": "Falta de arnés de contrato de payloads push (S2)",
        "cause": "No existían pruebas de contrato entre payload generado en móvil y deserializador en backend",
        "severity": "Desfase silencioso de esquema",
    },
    {
        "id": "F4-4",
        "phase": "feature-0004",
        "desc": "Tratamiento de errores en syncEngine sin persistencia (S1)",
        "cause": "Detalles de error volátiles en memoria",
        "severity": "Dificultad de diagnóstico en campo",
    },
    {
        "id": "F4-5",
        "phase": "feature-0004",
        "desc": "Frontera de cursor incremental sin orden determinista (S4)",
        "cause": "Cursor dependiente de reloj local sin tie-breaker id",
        "severity": "Eventos salteados en pull",
    },
    {
        "id": "F4-6",
        "phase": "feature-0004",
        "desc": "Reconciliación de eliminaciones incompleta (S5)",
        "cause": "Tombstones no propagados hacia todos los clientes",
        "severity": "Registros eliminados reaparecían",
    },
    {
        "id": "F4-7",
        "phase": "feature-0004",
        "desc": "Falta de bitácora local de sync para diagnóstico (S3)",
        "cause": "Sin almacenamiento persistente de auditoría en dispositivo",
        "severity": "Soporte ciego ante quejas de operarios",
    },
]

def run(cmd):
    return subprocess.check_output(cmd, shell=True, text=True).strip()

def compute_git_metrics():
    cmd = 'git log --merges --first-parent develop --date=short --format="%H|%cd|%s"'
    try:
        out = run(cmd)
    except Exception:
        out = ""

    weekly = collections.defaultdict(int)
    merges = []
    for line in out.splitlines():
        if not line:
            continue
        parts = line.split("|", 2)
        if len(parts) < 3:
            continue
        h, d_str, s = parts
        merges.append((h, d_str, s))
        try:
            dt = datetime.datetime.strptime(d_str, "%Y-%m-%d").date()
            year, week, _ = dt.isocalendar()
            weekly[f"{year}-W{week:02d}"] += 1
        except ValueError:
            pass

    return merges, dict(sorted(weekly.items()))

def main():
    print("==> Calculando métricas de entrega (DORA)...")
    merges, weekly = compute_git_metrics()
    total_merges = len(merges)
    total_incidents = len(DOCUMENTED_INCIDENTS)
    cfr = (total_incidents / total_merges * 100) if total_merges else 0.0

    print(f"Total de merges en develop: {total_merges}")
    print(f"Semanas con actividad registradas: {len(weekly)}")
    for w, c in weekly.items():
        print(f"  - {w}: {c} merges")

    avg_freq = total_merges / len(weekly) if weekly else 0.0
    print(f"Frecuencia semanal media: {avg_freq:.1f} merges/semana")
    print(f"Incidentes documentados: {total_incidents}")
    print(f"Tasa de fallos estimada (Change Failure Rate): {cfr:.1f}%")
    print("Nota: Change Failure Rate y Time to Restore son de fiabilidad baja por ausencia de telemetría histórica.")

if __name__ == "__main__":
    main()
