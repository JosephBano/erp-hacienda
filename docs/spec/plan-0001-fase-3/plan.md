# plan.md — Ejecución de la Fase 3 (archivado)

> **Documento archivado.** Las ~11 ramas listadas abajo ya están mergeadas a `develop`; esto
> no es un plan por ejecutar sino el registro de en qué orden se ejecutó. Se marca en el
> encabezado según la regla de `docs/DOCUMENTACION.md` sec. 4.
>
> **Qué es este documento.** Enlaza a [`spec.md`](./spec.md) (qué se decidió y qué se
> construyó, secs. 2.2 y 3), a [`tasks.md`](./tasks.md) (el desglose con casillas — todas
> cerradas salvo el piloto real) y a [`test-e2e.md`](./test-e2e.md) (los 10 escenarios de
> sincronización como guion ejecutable).

**Objetivo:** que los empleados registren desde el potrero, sin señal — mismo objetivo que
`spec.md` sec. 1, ya alcanzado en el código; pendiente solo el piloto real de campo
(`spec.md` sec. 4).

**Enfoque ejecutado:** ~11 ramas secuenciales agrupadas en tres bloques (3.A, 3.B, 3.C), con
dos hitos de validación entre bloques que detenían el avance si no se sentían sólidos —
`spec.md` sec. 3, Hito 3.A y Hito 3.B. Una rama de corrección adicional
(`fix/fase-3-sync-correctness`) se insertó fuera de esta secuencia cuando una auditoría
posterior al cierre encontró los tres defectos de `spec.md` sec. 4.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales (heredadas de `PLAN-FASE-3-4.md` sec. 1)

- Cada rama sigue el ciclo de 9 pasos: `develop` actualizado → rama → diseño → TDD →
  validación local → commits → push → PR → merge. Nunca se commitea directo a `develop`.
- Dentro de un bloque, las ramas son secuenciales (cada una asume la anterior mergeada).
  Entre bloques hay un hito de validación: no se empieza el bloque siguiente hasta que el
  anterior esté mergeado, en verde y probado a mano.
- Decisión estructural → ADR escrito y mergeado antes que el código (Art. 14).

---

## Índice

1. [Bloque 3.A — Suelo en el backend (ejecutado)](#bloque-3a--suelo-en-el-backend-ejecutado)
2. [Hito 3.A](#hito-3a)
3. [Bloque 3.B — La app (ejecutado)](#bloque-3b--la-app-ejecutado)
4. [Hito 3.B](#hito-3b)
5. [Bloque 3.C — Piloto y cierre (parcialmente ejecutado)](#bloque-3c--piloto-y-cierre-parcialmente-ejecutado)
6. [Corrección post-cierre](#corrección-post-cierre)
7. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)

---

## Bloque 3.A — Suelo en el backend (ejecutado)

Detalle de cada rama en [`spec.md` sec. 3, Bloque 3.A](./spec.md#bloque-3a--suelo-en-el-backend-4-ramas).

1. `feature/people-permissions` — **mergeada**.
2. `feature/people-audit-trail` — **mergeada**.
3. `feature/sync-protocol-pull` — **mergeada**.
4. `feature/sync-protocol-push` — **mergeada**.

## Hito 3.A

Validado: usuario con rol limitado, `pull`, `push` con duplicados desde `curl`, `pull` de
nuevo — datos una sola vez.

## Bloque 3.B — La app (ejecutado)

Detalle de cada rama en [`spec.md` sec. 3, Bloque 3.B](./spec.md#bloque-3b--la-app-5-ramas).

1. `feature/field-app-scaffolding` — **mergeada**.
2. `feature/field-app-sync-engine` — **mergeada**.
3. `feature/field-app-milking` — **mergeada**.
4. `feature/field-app-events` — **mergeada**.
5. `feature/field-app-births` — **mergeada**.

## Hito 3.B

Validado en un primer intento con instalación en teléfono real — pero ver "Corrección
post-cierre" abajo: ese primer intento resultó insuficiente y motivó el cierre revertido.

## Bloque 3.C — Piloto y cierre (parcialmente ejecutado)

Detalle de cada rama en [`spec.md` sec. 3, Bloque 3.C](./spec.md#bloque-3c--piloto-y-cierre-2-ramas).

1. `feature/field-app-pilot-hardening` (e iteraciones `-2`, `-3`) — **mergeadas**: pantalla
   de estado de sincronización, bandeja de conflictos y rechazos, bitácora LWW alcanzable
   en uso real desde `field-app` y `admin-web`.
2. `docs/fase-3-cierre` — **parcial**: la retrospectiva de `ROADMAP.md` está escrita
   (`spec.md` sec. 4), pero el cierre formal de la fase espera el piloto real. Ver
   `tasks.md` de esta carpeta.

## Corrección post-cierre

`fix/fase-3-sync-correctness` — **mergeada**, fuera de la secuencia de bloques porque nace
de una auditoría posterior al primer intento de cierre (2026-08-03), no de la
planificación original. Corrige los tres defectos de `spec.md` sec. 4: interceptor de
auditoría faltante en `Livestock`, pérdida de `motherId` en el push de partos, y outbox en
`localStorage` (API inexistente en React Native).

## Orden, dependencias y puntos de no retorno

```
Bloque 3.A (4 ramas)
  └─ Hito 3.A ── bloquea Bloque 3.B (sin suelo de backend sólido no se camina)
Bloque 3.B (5 ramas)
  └─ Hito 3.B ── bloqueaba Bloque 3.C, pero el primer paso resultó insuficiente
fix/fase-3-sync-correctness ── corrige lo que el Hito 3.B no detectó
Bloque 3.C (2 ramas, la segunda parcial)
  └─ docs/fase-3-cierre ── punto de no retorno real: solo se completa con el piloto
     de campo (`spec.md` sec. 4), que ningún commit de código puede sustituir.
```
