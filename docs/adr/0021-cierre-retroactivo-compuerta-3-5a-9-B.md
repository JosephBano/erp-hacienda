# ADR-0021 — Cierre retroactivo (parcial y condicionado) de la compuerta sec.2.3 sobre 3.5a.9-B

- **Estado:** Aceptado
- **Fecha:** 2026-08-07
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina
- **Reemplaza a:** ADR-0020

## Contexto

Hechos verificables en este repositorio (al 2026-08-07):

- 2026-08-06 — PR #51 (commit `bc3d315`) mergea "feat(field-app): activity tree with
  subjects as first level (3.5a.9-B)" a develop.
- 2026-08-06 — PR #53 (commit `57cf5a3`) mergea "feat(field-app): pre-select animal +
  activity in EventsScreen (3.5a.9-B)" a develop.
- 2026-08-04 (aprox.) — PR #54 (commit `d817524`) mergea 3.5a.1 (group-subject events,
  control por conteo, ADR-0015). El stub "Un lote" se renderiza en `ActivitiesHub` con
  texto que dice "3.5a.1 has not landed" — **esa referencia quedó obsoleta** al
  merge de #54.
- 2026-08-07 — ADR-0020 ratifica que 3.5a.9-B está fuera del alcance porque "no hay
  evidencia de que esa compuerta se haya cerrado con el cliente en este repositorio".
  Esto es **posterior** a los merges de #51 y #53.
- 2026-08-07 — La auditoría del mismo día detecta la contradicción entre el código
  mergeado y el ADR.

La compuerta `PLAN-FASE-3-5-PORCINO.md` sec.2.3 y la sección "Compuerta" de
`PLAN-FASE-3-5-PORCINO-3.5a.9-B.md` dicen textual:

> "el árbol sólo se implementa cuando esté dibujado y los toques contados con el
> cliente. Sin esa conversación este PR no arranca."

El sub-plan (sec."Decisión de diseño" punto 1 y tarea 6) proponía una infraestructura
`ACTIVITY_TREE` en TypeScript y tests `TapBudget` para enforcear la regla "3 toques /
4 toques" del plan. Esa infraestructura **no se implementó** en los PRs #51/#53:
`grep -rE "TapBudget|ACTIVITY_TREE|activityTree"` en `clients/field-app/` devuelve
0 resultados.

El código mergeado entrega: navegación de primer nivel (cuatro sujetos: animal, lote,
parto, hoy), pre-selección de animal/actividad en `EventsScreen` (optimización de un
flujo ya existente, no actividad nueva), y un stub deshabilitado para "Un lote".
**No entrega** ninguna actividad nueva del sujeto "lote" — esas viven en 3.5a.7
(pendiente).

## Decisión

Este ADR **acepta retroactivamente el merge** de los PRs #51 y #53 con cierre
**parcial y condicionado** de la compuerta sec.2.3, no total.

### Lo que la compuerta sec.2.3 exigía

Dos productos, ambos condición previa para "no arrancar pantallas":

1. **Árbol dibujado.** El árbol de primer nivel con sus cuatro sujetos como raíz.
2. **Toques contados sobre papel.** La regla "3 toques para lo normal, 4 para lo
   raro" validada contra el operador antes de escribir cada actividad.

### Lo que se considera cerrado

**(i) Árbol dibujado, sí.** La estructura del árbol está dibujada en el propio macro
plan: `docs/spec/PLAN-FASE-3-5-PORCINO.md` sec.2.3 líneas 133–155 muestran los
cuatro sujetos como primer nivel. El código mergeado por #51 implementa exactamente
esa estructura, sin adiciones. **El primer nivel del árbol no depende de la
frecuencia de actividades** — los sujetos son fijos en el plan.

### Lo que queda parcialmente abierto (diferido, no cerrado)

**(ii) Toques contados sobre papel, no cerrado.** Lo que sí está formalizado:

- La regla "3/4 toques" es parte del macro plan sec.2.3 (no del merge).
- El sub-plan tarea 6 proponía tests `TapBudget` para enforcear esa regla. Esos
  tests **no se escribieron** en #51/#53 — el merge sólo entregó navegación y
  pre-selección.

Lo que falta:

- La validación real con el operador parado en el corral, midiendo toques por
  actividad concreta.
- La infraestructura `TapBudget`/`ACTIVITY_TREE` que la enforce automáticamente.

Esto se difiere al trabajo de 3.5a.7 (la primera actividad del sujeto "lote" será
pesaje muestral, tarea 1 de 3.5a.7) y a la respuesta del cliente a las preguntas de
sec.7-C (frecuencias reales).

### Por qué es seguro aceptar el merge como está

- **El sujeto "lote" no permite registrar operaciones.** La rama "Un lote" se
  renderiza como stub deshabilitado (`clients/field-app/src/screens/ActivitiesHub.tsx`
  líneas 71–77, pineado por `tests/ActivitiesHub.test.tsx` líneas 33–35). El árbol
  mergeado no expone al usuario final un flujo de registro nuevo del sujeto "lote" —
  sólo los sujetos que ya existían como pantallas (animal, parto, hoy) más el stub.
- **El primer nivel no depende de frecuencia.** Los cuatro sujetos son los del plan;
  el orden interno se puede reordenar sin cambiar el árbol.
  `ActivitiesHub.test.tsx` líneas 38–50 pinea el orden actual como placeholder
  responsable hasta que el cliente responda sec.7-C.
- **El merge no cambió el modelo de datos.** El backend (`animal_events` con XOR
  animal/grupo de ADR-0015) ya estaba alineado con el árbol. Lo que cambió es
  sólo navegación de cliente móvil.
- **El trabajo pendiente del sub-plan sigue pendiente.** Las tareas 2 (selector con
  búsqueda y filtro), 3 (recientes), 5 ("hoy pendientes"), 6 (`TapBudget`) y 7
  (pruebas críticas) **no se hicieron** en #51/#53; se harán cuando las pantallas
  que las usen existan.

### Lo que este ADR no autoriza

- **No autoriza activar la rama "Un lote".** El stub sigue deshabilitado y debe
  seguir estándolo hasta que 3.5a.7 tareas 1–5 (pesaje muestral, baja con causa,
  vacunación de lote, diagnóstico grupal, consumo de alimento) mergeen y sus
  `TapBudget` se validen.
- **No cierra 3.5a.7** ni las preguntas de sec.7-C. El cierre de la compuerta sigue
  pendiente para las actividades del segundo nivel del árbol.
- **No relaja la regla "3/4 toques".** La regla del plan sigue vigente; este ADR
  sólo declara que la formalización en test del segundo nivel se difiere.

## Alternativas consideradas

- **Revertir PRs #51 y #53.** Descartada. El código mergeado es coherente con la
  estructura del árbol del plan y con el modelo de datos (ADR-0015). Revertirlo
  desecharía trabajo correcto sólo porque faltó una conversación con el cliente.
  Esa conversación se puede hacer ahora con el árbol ya en pantalla.
- **Tratar el merge como consciente y modificar el plan, sin reabrir el ADR.**
  Descartada. Mantener el ADR-0020 diciendo una cosa y el código diciendo otra deja
  al próximo lector sin saber qué decisión está vigente. Es la opción que la
  auditoría llama "(b)".
- **Mantener la compuerta estricta y bloquear todo merge nuevo del árbol.** Se
  mantiene **para 3.5a.7 y siguientes**: ninguna pantalla nueva del sujeto "lote"
  mergea sin `TapBudget` validado. Esta rama es la única que la compuerta sigue
  bloqueando de forma intacta.

## Consecuencias

### Positivas

- develop deja de tener una contradicción entre ADR y código.
- La compuerta sec.2.3 queda explícitamente **parcial y condicionadamente cerrada**,
  no falsamente cerrada.
- El stub "Un lote" se reetiqueta con honestidad: queda pendiente por las tareas
  1–5 de **3.5a.7**, no por 3.5a.1 (que mergeó en PR #54). Los comentarios
  obsoletos en `ActivitiesHub.tsx` y `ActivitiesHub.test.tsx` se actualizan como
  corrección documental derivada.

### Negativas / costos

- El procedimiento del sub-plan (cliente primero, código después) se rompe
  **una vez**. Es deliberado, no se convierte en norma.
- La auditoría del 2026-08-07 queda como evidencia de que la compuerta se saltó.
  Esa memoria es deliberada: queremos que un próximo auditor o desarrollador sepa
  que el bypass existió y por qué se justificó.

### Cambios documentales derivados (corrección, no justificación funcional)

- ADR-0020 → estado **Reemplazado por ADR-0021**, cuerpo intacto.
- `docs/spec/PLAN-FASE-3-5-PORCINO.md` sec.3.5a.9 fila de la sub-rama 3.5a.9-B:
  referencia al estado de la compuerta pasa a "Cerrada parcialmente por ADR-0021
  (primer nivel aceptado; segundo nivel gated por 3.5a.7)".
- `docs/spec/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-B.md` "Compuerta": apunta a
  ADR-0021.
- `clients/field-app/src/screens/ActivitiesHub.tsx` líneas 13 y 40 (comentarios
  obsoletos sobre 3.5a.1): actualizados.
- `clients/field-app/src/screens/ActivitiesHub.tsx` etiqueta del stub (línea 73):
  reescrita para mencionar 3.5a.7.
- `clients/field-app/tests/ActivitiesHub.test.tsx` líneas 33–35 (descripción del
  stub): actualizada para reflejar la nueva dependencia.

### Condición de reversa

Este ADR se reabre si:

1. El cliente, al ver el árbol, propone una estructura que **no** coincide con
   sec.2.3 (quinto sujeto, primer nivel que no sea el sujeto, o ramificación por
   especie en lugar de filtrado).
2. 3.5a.7 mergea alguna actividad del sujeto "lote" sin que su `TapBudget` esté
   validado contra el operador.
3. Un conteo real de toques para una actividad ya mergeada excede 3 (normal) o
   4 (raro).
4. Una prueba con usuarios muestra confusión o rutas ambiguas en el árbol actual.
5. El stub "Un lote" se habilita antes de que 3.5a.7 tareas 1–5 estén mergeadas
   y probadas.
6. Aparece divergencia entre el árbol implementado y el plan aprobado.
