# plan.md — Ejecución de la Fase 3.5 (Adaptación porcina)

> **Qué es este documento.** El orden de ramas, sus dependencias y el estado real de merge
> de la Fase 3.5, contra `develop`. Las decisiones que fijan *qué* se construye están en
> [`spec.md`](./spec.md) y [`spec-3.5a.md`](./spec-3.5a.md); el desglose ejecutable con
> casillas en [`tasks.md`](./tasks.md); la verificación en [`test-e2e.md`](./test-e2e.md).

**Objetivo:** adaptar el dominio al negocio porcino real del piloto antes de abrir la
Fase 4, en dos bloques — captura (3.5a) primero, análisis (3.5b) en paralelo al uso real
una vez que 3.5a tiene el mínimo mergeado (ADR-0024).

**Enfoque:** no es una rama única. Son ~17 ramas de feature, cada una con su propio PR,
secuenciadas por dependencia estructural. El protocolo de trabajo (los 9 pasos del ciclo de
una feature, el nivel de exigencia en pruebas, el formato de PR) es el de
`docs/PROTOCOLO-DE-TRABAJO.md` sec.1 y sec.2 — no se repite acá.

---

## Restricciones globales

- **Compuerta sec. 2.3** (`spec.md`): ninguna rama que toque el móvil con navegación nueva
  arranca sin el árbol de actividades cerrado con el cliente y los toques contados sobre
  papel. Excepción explícita: 3.5a.0 (corrige pantallas existentes, no agrega navegación).
- **Los ADRs mergean antes que su código** (Art. 14): 0015 antes de 3.5a.1, 0016 antes de
  3.5b.1, 0017 antes de 3.5a.8, 0018 antes de 3.5b.5-A/B/C, 0019 antes de 3.5a.9-A.
- **El piloto real no espera al cierre completo de 3.5a** (ADR-0024): abre con el
  sub-conjunto mínimo de la tabla "Para abrir el piloto real" (`spec-3.5a.md`) mergeado a
  develop; el resto queda como deuda rastreable en `docs/BACKLOG.md` sección 3.5.
- **Nada de plata en esta fase**: el FCR y todo lo demás va en kg. La contabilidad es
  Fase 4 (riesgo declarado en `spec.md` sec. 6).

---

## Índice

1. [Bloque 3.5a — orden de ramas](#1-bloque-35a--orden-de-ramas)
2. [Bloque 3.5b — orden de ramas](#2-bloque-35b--orden-de-ramas)
3. [Estado real contra `develop` (2026-08-16)](#3-estado-real-contra-develop-2026-08-16)
4. [Orden, dependencias y puntos de no retorno](#4-orden-dependencias-y-puntos-de-no-retorno)
5. [Sub-planes enlazados (fuera de alcance de esta carpeta)](#5-sub-planes-enlazados-fuera-de-alcance-de-esta-carpeta)

---

## 1. Bloque 3.5a — orden de ramas

| Orden | Rama Git | Sección | Por qué en esta posición |
|---|---|---|---|
| 1 | `feature/field-app-input-guards` | 3.5a.0 | Sin cambio de backend, cierra en días, sostiene la conversación con el cliente mientras se construye el resto. Excepción a la compuerta sec. 2.3. |
| 2 | `feature/livestock-group-events` | 3.5a.1 | Estructural (ADR-0015). Sin sujeto grupal no existe el lote por conteo; todo lo demás de 3.5a lo asume. |
| 3 | `feature/livestock-treatment-catalog` (`3.5a.2-A`) | 3.5a.2-A | Catálogos primero: 3.5a.2-B y 3.5a.2-C dependen de esta. |
| 4 | `feature/livestock-treatment-dose-logic` (`3.5a.2-B`) | 3.5a.2-B | Depende de A. |
| 5 | `feature/field-app-treatment-ui` (`3.5a.2-C`) | 3.5a.2-C | Depende de A y B. |
| 6 | `feature/livestock-mortality-causes` | 3.5a.3 | Independiente; puede fusionarse con 3.5a.2-A si conviene un solo PR (nota de tamaño en `spec-3.5a.md`). |
| 7 | `feature/breeding-nursing-cohort` | 3.5a.4 | Estructural. Depende de que `Birthing.RecordWeaning` exista (ya existe desde Fase 3). |
| 8 | `feature/inventory-unit-conversions` | 3.5a.5 | Bloqueante para el consumo de alimento por lote (3.5a.7 tarea 5). |
| 9 | `feature/livestock-plausibility-ranges` | 3.5a.6 | Requisito para que 3.5a.7 tarea 1 (pesaje muestral) tenga rangos contra los que validar. |
| 10 | `feature/field-app-lot-registration` | 3.5a.7 | **Compuerta sec. 2.3**: no arranca sin el árbol de actividades cerrado. Depende de 3.5a.1, 3.5a.5, 3.5a.6. |
| 11 | `feature/field-app-corrections` | 3.5a.8 (ADR-0017) | Independiente en el modelo; en la práctica se beneficia de que ya existan registros de campo variados (3.5a.0–3.5a.7) para probar corrección real. |
| 12 | `feature/field-app-module-visibility` (`3.5a.9-A`) | 3.5a.9-A (ADR-0019) | Transversal, testeable en aislamiento, sin compuerta. |
| 13 | `feature/field-app-activity-tree` (`3.5a.9-B`) | 3.5a.9-B | Primer nivel cerrado retroactivamente por ADR-0021 (2026-08-07); segundo nivel (actividades del lote) gated por 3.5a.7 tareas 1–5. |

## 2. Bloque 3.5b — orden de ramas

> Se construye **mientras el campo ya carga datos de 3.5a** — no espera a que 3.5a cierre
> como bloque.

| Orden | Rama Git | Sección | Por qué en esta posición |
|---|---|---|---|
| 1 | `feature/livestock-health-plans` | 4.1 (ADR-0016) | Estructural. `HealthPlanItem.id` ya es nullable en `AnimalEvent` desde 3.5a.2-A (forward-compat); esta rama cierra esa FK. |
| 2 | `feature/tasks-health-plan-alerts` | 4.2 | Depende de 4.1 (lee `HealthPlanItem`). |
| 3 | `feature/inventory-feeding-standards` | 4.3 | Independiente de 4.1/4.2; depende de 3.5a.5 (unidades) y 3.5a.7 (pesaje del lote). |
| 4 | `feature/analytics-lot-fcr` | 4.4 | Depende de 3.5a.5 (consumo) y 3.5a.7 tarea 1 (pesajes). "Sale gratis" de captura ya construida. |
| 5 | `feature/livestock-animal-traits-core` (`3.5b.5-A`) | 3.5b.5-A (ADR-0018) | Núcleo primero; B y C dependen de esta. |
| 6 | `feature/livestock-selection-criterion-deprecation` (`3.5b.5-B`) | 3.5b.5-B | Depende de A. |
| 7 | `feature/livestock-trait-alerts-and-versioning` (`3.5b.5-C`) | 3.5b.5-C | Depende de A (recomendada B). |
| 8 | `feature/breeding-maternal-index` | 4.6 | Consumidor de 3.5b.5 (características subjetivas) y de 3.5a.3 (mortalidad con causa, eventos contables). |
| 9 | `feature/tasks-swine-alerts` | 4.7 | Independiente; depende de que Breeding (destete) y `WithdrawalTarget.Meat` ya existan (Fase 3 y Art. 19). |

## 3. Estado real contra `develop` (2026-08-16)

> Trasladado de `docs/ROADMAP.md:146-217`.

- **Desacople del inicio del piloto** del cierre completo del bloque 3.5a — ADR-0024
  (2026-08-07), ADR-0021 (2026-08-07, sobre la compuerta sec. 2.3). El inicio del piloto
  real no exige cerrar 3.5a como bloque: exige el sub-conjunto mínimo de captura +
  tratamiento + primer nivel del árbol + visibilidad de módulos + rangos de plausibilidad,
  todos mergeados a `integration`.
- **Mergeado al 2026-08-09:** 3.5a.2 (A/B/C) — B/C vía `feature/livestock-treatment-dose-logic`
  + `feature/field-app-treatment-ui`; 3.5a.6 (PR #73); 3.5a.7 tareas 1–5
  (`feature/field-app-lot-registration`).
- **Deuda rastreable, no bloqueo (`docs/BACKLOG.md` sección 3.5):** 3.5a.8 (corrección desde
  el teléfono); 3.5a.3 causas de muerte (lógica de dominio lista, falta pantalla de admin en
  admin-web); 3.5a.4 tarea 4 (clasificación por peso — pieza que cierra el criterio de
  salida completo de 3.5a).
- **Recepción de inventario (ADR-0026, PR #94, mergeada 2026-08-13):** cierre del
  bloqueante transversal "no hay forma trazable de rellenar inventario de comida",
  detectado durante el piloto. Endpoint `POST /receptions`, comando
  `RecordInventoryReceptionCommand`, evento `InventoryReceptionRecorded`, UI admin-web
  "Recibir alimento" con badge "Sin declaración completa" (issue #93). Vida útil declarada:
  deprecado cuando llegue Purchasing (Fase 4) — ver `spec.md` sec. 2.9. El consumo desde
  lote (3.5a.7, mergeado) ya descuenta de `InventoryBatch`; este feature cierra la otra
  mitad del flujo (entrada con fecha declarada, proveedor, factura, autor).
- El criterio completo de salida de 3.5a **sigue exigiendo** clasificación por peso +
  tratamiento con vía/motivo + corrección desde el teléfono como bloque — ver `spec-3.5a.md`
  "Criterio completo (cierre del bloque 3.5a)".

## 4. Orden, dependencias y puntos de no retorno

```
3.5a.0 (sin dependencias, arranca ya)
3.5a.1 (ADR-0015) ── bloquea 3.5a.7, 3.5a.9-B nivel 2 (sujeto "lote" no existe sin esto)
3.5a.2-A ── bloquea 3.5a.2-B, 3.5a.2-C
3.5a.5 ── bloquea 3.5a.7 tarea 5, 4.3, 4.4
3.5a.6 ── bloquea 3.5a.7 tarea 1
3.5a.7 (compuerta sec. 2.3) ── bloquea 3.5a.9-B nivel 2, 4.4
3.5a.9-A (ADR-0019) ── independiente, sin compuerta
3.5b.5-A (ADR-0018) ── bloquea 3.5b.5-B, 3.5b.5-C, 4.6
4.1 (ADR-0016) ── bloquea 4.2
```

**Punto de no retorno:** 3.5a.1. Una vez mergeado, `AnimalEvent.AnimalId` es XOR con
`GroupId` y el CHECK de BD queda escrito en la migración — revertirlo implica una migración
de reversa sobre datos ya cargados en producción, no un `git revert` limpio. Todo lo
posterior a 3.5a.1 asume el modelo grupal.

## 5. Sub-planes enlazados (fuera de alcance de esta carpeta)

Estos ocho archivos **no se absorben** — quedan donde están, en `docs/planes/sub_planes/`,
y esta carpeta sólo los enlaza:

- [`PLAN-FASE-3-5-PORCINO-3.5a.2-A.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md)
- [`PLAN-FASE-3-5-PORCINO-3.5a.2-B.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-B.md)
- [`PLAN-FASE-3-5-PORCINO-3.5a.2-C.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-C.md)
- [`PLAN-FASE-3-5-PORCINO-3.5a.9-A.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md)
- [`PLAN-FASE-3-5-PORCINO-3.5a.9-B.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-B.md)
- [`PLAN-FASE-3-5-PORCINO-3.5b.5-A.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-A.md)
- [`PLAN-FASE-3-5-PORCINO-3.5b.5-B.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-B.md)
- [`PLAN-FASE-3-5-PORCINO-3.5b.5-C.md`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-C.md)
