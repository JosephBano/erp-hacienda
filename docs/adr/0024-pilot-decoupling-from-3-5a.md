# ADR-0024 — Desacople del piloto real del criterio completo de salida de 3.5a

- **Estado:** Aceptado
- **Fecha:** 2026-08-08
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina — bloque 3.5a

## Contexto

Hechos verificables en este repositorio (al 2026-08-08, develop local en `2ec771f`):

- `docs/ROADMAP.md` línea 158 dice textual: *"Se ejecuta en dos bloques; **el piloto real
  arranca al cerrar 3.5a**"*. Acopla el inicio del piloto al cierre del bloque completo.
- `docs/planes/PLAN-FASE-3-5-PORCINO.md` línea 97 repite el mismo acoplamiento: *"La fase
  se parte en dos bloques y **el piloto arranca al cerrar 3.5a**"*. El criterio completo
  de salida del bloque (sección homónima) exige clasificación por peso, tratamiento con
  vía/motivo, y corrección desde el teléfono como bloque único, más una camada real
  pesada y seguida.
- Una tentativa anterior de documentar este desacople (rama `docs/adr-0023-pilot-decoupling`
  en este repositorio) **no llegó a mergear**: la rama existe como puntero a un commit
  antiguo de develop (`e60dc55`) y el archivo `0023-pilot-decoupling-from-3-5a-closure.md`
  nunca fue creado en ninguna rama del repo. La numeración 0023 que esa tentativa eligió
  quedó ocupada por [`ADR-0023`](./0023-eventos-clasificacion-por-peso.md)
  (eventos-clasificación-por-peso), mergeada en el commit `b7265f2` (2026-08-07).
- El conjunto de pendientes de 3.5a se redujo desde ese intento:

  | Sub-tarea | Estado al 2026-08-08 | Dónde |
  |---|---|---|
  | 3.5a.0 input guards | Mergeada | `feature/field-app-input-guards` |
  | 3.5a.1 group events (ADR-0015) | Mergeada | PR #54 |
  | 3.5a.2-A treatment catalogs | Mergeada | PR #69 |
  | 3.5a.2-B structured treatment payload | Mergeada | PR #70 |
  | 3.5a.2-C treatment UI | Parcial (foundations) | `feature/field-app-treatment-ui` |
  | 3.5a.4 task 4 weight classification | En rama | `feature/breeding-weight-classification` (3 commits) |
  | 3.5a.5 inventory unit conversions | Mergeada | commit `f52548a` |
  | 3.5a.6 plausibility ranges | En rama | `feature/livestock-plausibility-ranges` (2 commits) |
  | 3.5a.7 task 6 lot summary endpoint | Mergeada | PR #66 |
  | 3.5a.7 tasks 1–5 (UI del sujeto "lote") | **Sin código** | — |
  | 3.5a.8 field corrections | Sin código | — |
  | 3.5a.9-A module visibility (ADR-0019) | Mergeada | PR #57 |
  | 3.5a.9-B activity tree (primer nivel) | Mergeada parcial | PRs #51 y #53, ADR-0021 |

- La compuerta `PLAN-FASE-3-5-PORCINO.md` sec.2.3 sobre 3.5a.9-B está **parcial y
  condicionadamente cerrada** por [`ADR-0021`](./0021-cierre-retroactivo-compuerta-3-5a-9-B.md).
  La rama "Un lote" del árbol se renderiza como stub deshabilitado
  (`clients/field-app/src/screens/ActivitiesHub.tsx`) y seguirá estándolo hasta que 3.5a.7
  tareas 1–5 mergeen con su `TapBudget` validado.
- El plan sec.2.2 (Captura primero, análisis después) y el plan sec.7-C (frecuencias
  reales) **siguen bloqueando** 3.5a.7: sin frecuencias del cliente no se cierra el árbol
  de actividades del segundo nivel, y sin árbol no se compila la UI del sujeto "lote".

El problema de fondo que este ADR resuelve es el mismo que la versión anterior atacaba y
que el ROADMAP explícitamente plantea: mientras el piloto dependa del cierre completo de
3.5a, y mientras 3.5a.7.1–5 siga bloqueado por la compuerta sec.2.3 y por la respuesta
del cliente a sec.7-C, **el cliente del piloto no puede usar el sistema** — que es
exactamente la condición que el Art. 11 prohíbe como criterio de cierre de fase.

El argumento conceptual del desacople sigue siendo válido. Lo que cambió es la
justificación factual: ya no es "todo 3.5a está inalcanzable" sino "el sub-conjunto que
sí mergeó cubre la captura individual, los tratamientos, el primer nivel del árbol y la
visibilidad de módulos; lo que falta (3.5a.7.1–5 y 3.5a.8) es deuda rastreable, no
bloqueante".

## Decisión

Este ADR acepta el **desacople del inicio del piloto real del criterio completo de salida
de 3.5a**, sujeto a las siguientes condiciones.

### 1. Sub-criterio explícito "Para abrir el piloto real"

El piloto real puede abrir cuando estén **mergeadas a develop** las siguientes piezas
(en cualquier orden, sin requerir 3.5a.7.1–5 ni 3.5a.8):

| Pieza | Por qué es requisito para abrir |
|---|---|
| 3.5a.0 input guards | El cliente reportó defectos de dedo que no se arreglan solos. |
| 3.5a.1 group events (ADR-0015) | El sujeto "lote" no existe en el modelo sin esto. La UI llega después; el modelo no puede esperar. |
| 3.5a.2-A y 3.5a.2-B (catálogos + payload) | El camino de tratamiento (lo más frecuente en porcinos) sin esto se queda en texto libre, violando Art. 10. |
| 3.5a.2-C (UI de tratamiento) | Sin pantallas, lo anterior existe sólo en backend. |
| 3.5a.5 inventory unit conversions | El alimento se compra en sacos y se consume en kilos — sin conversión, los números mienten. |
| 3.5a.6 plausibility ranges | El cliente reportó que la app acepta 1000 L; no se abre el piloto con esa puerta abierta. |
| 3.5a.9-A module visibility (ADR-0019) | Apagar Ordeño para esta finca sin tocar código. |
| 3.5a.9-B primer nivel del árbol (ADR-0021) | La navegación de primer nivel (animal, lote como stub, parto, hoy) mergeada. |

Esto se documenta en `docs/planes/PLAN-FASE-3-5-PORCINO.md` como sub-criterio bajo
"Criterio de salida de 3.5a", titulado **"Para abrir el piloto real"**, separado del
criterio completo de salida del bloque.

### 2. Deuda rastreable, no bloqueante

Lo que **no** entra en el sub-criterio de apertura queda como deuda rastreable en
`docs/BACKLOG.md`, sección "De Fase 3.5 — diferido a propósito", con disparador
explícito:

- **3.5a.7.1–5 (UI del sujeto "lote": pesaje muestral, baja con causa, vacunación de
  lote, diagnóstico grupal, consumo en sacos).** Disparador: el cliente responde las
  preguntas de `PLAN-FASE-3-5-PORCINO.md` sec.7-C (frecuencias reales) y/o la compuerta
  sec.2.3 se cierra para el segundo nivel del árbol (vía ADR-0021, vía sucesor).
- **3.5a.8 (corrección de registros desde el teléfono, ADR-0017).** Disparador: cualquier
  rechazo o dedupe en el outbox del cliente móvil durante el piloto que no pueda
  corregirse volviendo a registrar.
- **3.5a.3 (causas de muerte de lechón).** Disparador: el cliente pide distinguir causas
  durante el piloto, o el índice de madres (3.5b.6) necesita la causa antes de tiempo.

Mientras esa deuda no se pague, el piloto funciona con el flujo viejo:
`recordAnimalEvent` con `GroupId` directo sobre eventos grupales (sin UI específica del
sujeto "lote"), y corrección de eventos por re-registro manual. Esto está **documentado
como deuda**, no oculto.

### 3. Lo que el desacople NO autoriza

- **No autoriza activar el stub "Un lote"** del árbol de actividades antes de que 3.5a.7
  tareas 1–5 mergeen. El stub sigue deshabilitado por ADR-0021.
- **No autoriza tomar decisiones de manejo** (clasificar por peso, vender un lote,
  fechar un retiro de carne) que dependan específicamente de 3.5a.7.1–5. Mientras esa
  UI no exista, esas operaciones se hacen a mano y se documentan como pendientes.
- **No relaja la regla "3 toques / 4 toques"** del plan sec.2.3. La regla sigue vigente
  para toda actividad nueva del segundo nivel del árbol; este ADR sólo declara que
  mientras 3.5a.7.1–5 no exista, no hay segundo nivel del sujeto "lote" que evaluar.
- **No cierra 3.5a como bloque.** El criterio completo de salida de 3.5a sigue exigiendo
  clasificación por peso + tratamiento con vía/motivo + corrección desde el teléfono. El
  sub-criterio "Para abrir el piloto real" es condición **necesaria** para el inicio del
  piloto, **no suficiente** para cerrar 3.5a.

### 4. Ediciones documentales derivadas

- `docs/ROADMAP.md` línea 158: el acoplamiento literal se reemplaza por una referencia
  al sub-criterio de este ADR.
- `docs/ROADMAP.md` estado de la Fase 3.5: nota explícita de que el desacople se ejecuta
  por ADR-0024.
- `docs/planes/PLAN-FASE-3-5-PORCINO.md` línea 97: acoplamiento literal reemplazado por
  referencia al sub-criterio.
- `docs/planes/PLAN-FASE-3-5-PORCINO.md` "Criterio de salida de 3.5a": nuevo sub-criterio
  "Para abrir el piloto real" antes del criterio completo.
- `docs/BACKLOG.md` sección 3.5: entradas de deuda rastreable para 3.5a.7.1–5, 3.5a.8 y
  3.5a.3, con disparador explícito.

## Alternativas consideradas

- **Mantener el acoplamiento literal (no hacer este ADR).** Descartada: deja al cliente
  sin sistema mientras se terminan piezas que el plan no obliga a tener antes de
  arrancar (3.5a.7.1–5 está bloqueado por una compuerta externa al código, no por el
  código mismo). El Art. 11 pide que la fase cierre cuando algo se usa de verdad en la
  finca; el desacople es lo que permite que ese "se usa de verdad" empiece a ocurrir.
- **Esperar al cierre completo de 3.5a antes de abrir el piloto.** Descartada: 3.5a.7.1–5
  depende de la respuesta del cliente a sec.7-C, y 3.5a.8 es trabajo de código que
  puede avanzarse en paralelo al uso real. Hacer esperar al cliente por una conversación
  que él no pidió es exactamente el patrón que la Fase 3 pagó cerrándose en falso.
- **Acoplar el inicio del piloto a un hito más chiquito arbitrario** (p. ej. "5 features
  mergeadas"). Descartada: la cantidad no es la condición correcta. El sub-criterio
  apunta a las **piezas mínimas** que cubren captura + tratamiento + árbol + visibilidad,
  no a un número mágico.
- **Reabrir la compuerta sec.2.3 para 3.5a.7.1–5 y forzar la implementación.** Descartada:
  la compuerta existe porque la promesa "3 toques / 4 toques" se rompió una vez
  (Fase 3 retrospectiva) y la lección fue que la conversación con el cliente vale más
  que el código temprano. Reabrirla para "avanzar" sería exactamente el error que
  documenta la retrospectiva de Fase 3.

## Consecuencias

### Positivas

- El cliente puede usar el sistema en condiciones reales (captura individual +
  tratamiento + primer nivel del árbol + visibilidad de módulos) sin esperar a que
  3.5a.7.1–5 exista.
- La deuda restante queda **explícita y rastreable** en `BACKLOG.md`, no oculta en
  criterios vagos.
- El desacople tiene un **número de ADR propio** (0024), no se confunde con el 0023 que
  ya está ocupado por eventos-clasificación-por-peso.
- El inicio del piloto se vuelve **una decisión operativa**, no una declaración
  retórica en el ROADMAP. Es lo que el Art. 11 entiende por "se usa de verdad en la
  finca".

### Negativas / costos

- El desacople tiene que **comunicarse** al cliente: él espera que "arrancar el piloto"
  signifique que todo 3.5a esté cerrado. El sub-criterio "Para abrir el piloto real" es
  la herramienta que el dueño de la finca usa para entender qué queda dentro y qué
  queda fuera.
- El flujo viejo (`recordAnimalEvent` con `GroupId` directo) queda habilitado por
  defecto. Es deuda explícita, pero deuda al fin: si el piloto dura más de un ciclo de
  engorde sin que 3.5a.7.1–5 mergee, la fricción se va a acumular.
- La rama tentativa `docs/adr-0023-pilot-decoupling` queda obsoleta. Se documenta su
  existencia para trazabilidad, pero no se reactiva: el contenido que tenía pensado se
  reemplaza por este ADR-0024 con justificación factual actualizada.

### Condición de reversa

Este ADR se reabre si:

1. El cliente, al usar el sistema, reporta que **no puede registrar algo del flujo
   diario** porque la UI específica (3.5a.7.1–5) no existe, y la solución con
   `recordAnimalEvent` + `GroupId` directo le resulta inaceptable.
2. La deuda rastreable crece más allá de 3.5a.7.1–5 + 3.5a.8 + 3.5a.3 — por ejemplo,
   si 3.5a.5 resulta insuficiente en producción y hay que reabrir conversiones de
   unidad, o si 3.5a.6 no cubre magnitudes reales del campo.
3. El piloto corre un ciclo completo de engorde y el FCR (3.5b.4) sale **incorrecto** por
   no tener UI de pesaje muestral del lote. Eso diría que 3.5a.7.1 era más bloqueante
   de lo que este ADR asumió.
4. Una conversación con el cliente (sec.7-C) revela que el orden de actividades del
   sujeto "lote" no es el asumido, y eso fuerza reescribir la UI antes de lo previsto.
5. La rama "Un lote" se habilita por error antes de que 3.5a.7.1–5 esté mergeada y
   probada. Eso diría que la compuerta de ADR-0021 se rompió y este desacople hay que
   revisarlo.
