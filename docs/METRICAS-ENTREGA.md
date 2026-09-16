# METRICAS-ENTREGA.md — Línea base de métricas de entrega (DORA)

> Este documento responde **¿cuál es la velocidad y estabilidad de la entrega (DORA) y su línea base histórica?**
> (`docs/DOCUMENTACION.md` sec. 2). Registra la línea base calculada en el **Paso Operativo O1** de `feature-0011`
> antes de activar las compuertas obligatorias de CI (D10 / `docs/spec/feature-0011-devops-delivery-pipeline/spec.md` sec. 9).
>
> Una vez activados los checks obligatorios, el "antes" deja de ser observable. Este archivo fija los números
> de partida contra los que se comparará la evolución futura de la tubería.

---

## 1. Resumen de las cuatro métricas DORA (Línea base histórica)

Calculado el 2026-09-16 sobre un universo de **102 Pull Requests mergeados** a `develop` y 6 semanas de actividad registradas:

| Métrica DORA | Valor de línea base | Fiabilidad | Fuente de datos |
|---|---|---|---|
| **1. Tiempo de entrega del cambio (Lead Time for Changes)** | Mediana: **0.08 h** (~5 min)<br>Media: **0.80 h** (~48 min)<br>P90: **1.57 h** (~94 min) | **Alta** | GitHub PR API (`createdAt` → `mergedAt` de 102 PRs) |
| **2. Frecuencia de integración (Integration Frequency)** | Media: **8.3 merges/semana**<br>Rango: **4 a 18** merges/semana | **Alta** | `git log --merges --first-parent develop` |
| **3. Tasa de fallos del cambio (Change Failure Rate)** | Estimada: **9.8%** (10 incidentes / 102 PRs) | **Baja** ⚠️ | Incidentes documentados en `docs/ROADMAP.md` y `feature-0004` |
| **4. Tiempo de restauración (Time to Restore Service)** | Estimado: **24 h a 72 h** (1 a 3 días) | **Baja** ⚠️ | Auditorías y reversión de Fase 3 en `docs/ROADMAP.md` |

> [!IMPORTANT]
> **Declaración explícita sobre fiabilidad (spec.md sec. 9):**
> Las métricas 1 y 2 (Lead Time y Frecuencia de Integración) tienen **fiabilidad alta**, pues provienen de marcas
> temporales inmutables de Git y de la API de GitHub.
> Por el contrario, las métricas 3 y 4 (Tasa de Fallos y Tiempo de Restauración) son de **fiabilidad baja**,
> ya que el proyecto no contaba con un sistema formal de seguimiento de incidentes en producción ni telemetría
> automática. Inflar o pretender exactitud en estas dos métricas sería peor que no tenerlas.

---

## 2. Detalle de Frecuencia de Integración (Serie Semanal)

Merges a la rama `develop` agrupados por semana del calendario ISO:

| Semana ISO | Número de merges | Contexto / Hito |
|---|---|---|
| `2026-W31` | 18 | Arranque intensivo de Fase 3 (móvil y sincronización) |
| `2026-W32` | 9 | Consolidación de módulos de campo |
| `2026-W33` | 9 | Endpoints administrativos y catálogos |
| `2026-W34` | 4 | Cierre preliminar de Fase 3 |
| `2026-W37` | 5 | Estabilización post-auditoría (`feature-0004` a `feature-0008`) |
| `2026-W38` | 5 | Rediseño de app de campo y DevOps delivery pipeline (`feature-0010`, `feature-0011`) |

---

## 3. Incidentes Históricos Documentados

Clasificación manual de incidentes que motivaron correcciones estructurales o reversiones:

| ID | Origen | Descripción del incidente | Causa raíz | Impacto / Severidad |
|---|---|---|---|---|
| `F3-1` | Fase 3 | Entidades `Livestock` sin auditoría de marcas temporales | Interceptor de auditoría registrado únicamente en `PeopleDbContext` | Pérdida de sincronización incremental |
| `F3-2` | Fase 3 | Parto pierde genealogía silenciosamente | `motherId` enviado a comando de creación que no lo persistía | Pérdida de datos genealógicos |
| `F3-3` | Fase 3 | Cola de sincronización fuera de línea en `localStorage` | `localStorage` no existe en React Native nativo | Pérdida de operaciones offline al cerrar la app |
| `F4-1` | feature-0004 | Ordeño individual rechazado sistemáticamente (S8) | Campo `isPlausibilityConfirmed` en raíz rechazado por `UnmappedMemberHandling.Disallow` | Rechazo determinista de registros válidos |
| `F4-2` | feature-0004 | Operación duplicada enmascara fallo previo (S9) | Motor trataba `Duplicate` como éxito descartando los detalles del error previo | Registros fallidos reportados falsamente como sincronizados |
| `F4-3` | feature-0004 | Desfase de esquema en cargas útiles (S2) | Ausencia de pruebas de contrato entre payload generado en móvil y deserializador backend | Desfase silencioso de esquema en push |
| `F4-4` | feature-0004 | Errores de sincronización volátiles (S1) | Detalles de error no persistidos en almacenamiento nativo | Imposibilidad de diagnóstico en campo |
| `F4-5` | feature-0004 | Salto de eventos en sincronización incremental (S4) | Cursor de pull dependiente de reloj local sin identificador de desempate | Eventos salteados en actualización |
| `F4-6` | feature-0004 | Reaparición de registros eliminados (S5) | Reconciliación incompleta de eliminaciones lógicas (`tombstones`) | Registros eliminados reaparecían en móvil |
| `F4-7` | feature-0004 | Ausencia de bitácora local de diagnóstico (S3) | Sin auditoría diagnóstica en dispositivo móvil | Diagnóstico a ciegas ante quejas de operarios |

---

## 4. Reproducibilidad

El script reproducible que deriva la serie de merges y los cálculos de Git vive en:
[`scripts/compute-delivery-metrics.py`](../scripts/compute-delivery-metrics.py).

Para ejecutarlo localmente:
```bash
python3 scripts/compute-delivery-metrics.py
```
