# spec.md — Reestructuración de la documentación

> **Qué es este documento.** La especificación de diseño de la rama
> `docs/reestructura-documentacion`: qué sistema de documentación se establece, por qué, y
> qué decisiones quedan fijadas. El *cómo* día a día vive en [`plan.md`](./plan.md), el
> desglose ejecutable en [`tasks.md`](./tasks.md) y la verificación en
> [`test-e2e.md`](./test-e2e.md) — que aquí no es una prueba de app sino una **verificación
> documental**: que todo enlace resuelva y que toda cita desde el código apunte a algo que
> existe.
>
> **Por qué esta subcarpeta.** Los cuatro documentos son un solo entregable de una sola
> rama; separarlos en `docs/spec/` los dejaría huérfanos entre sí. Es el mismo motivo y
> el mismo precedente que `field-app-parto-redesign/`.

- **Rama Git:** `docs/reestructura-documentacion` (desde `develop`, en `2988946`).
- **Fecha:** 2026-08-16.
- **Fase del ROADMAP:** transversal. No abre ni cierra fase; ordena el soporte documental
  de todas.
- **ADRs vigentes que respeta:** ADR-0007 (modelo de permisos en BD), ADR-0008 (protocolo
  de sincronización), ADR-0024 (desacople del piloto), ADR-0026 (recepción de inventario
  pre-Purchasing).
- **Reglas duras que gobiernan este trabajo:** `AGENTS.md` regla 4 (rama desde `develop`),
  regla 9 (un PR, un propósito) y la convención de `docs/` — citada literal en sec. 2.2.

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: taxonomía de la documentación](#5-diseño-taxonomía-de-la-documentación)
6. [Diseño: plantillas](#6-diseño-plantillas)
7. [Diseño: conversión de las fases a carpetas](#7-diseño-conversión-de-las-fases-a-carpetas)
8. [Diseño: `PROTOCOLO-DE-TRABAJO.md`](#8-diseño-protocolo-de-trabajomd)
9. [Diseño: `SEGURIDAD.md`](#9-diseño-seguridadmd)
10. [Diseño: diagramas](#10-diseño-diagramas)
11. [Diseño: BACKLOG único y limpieza de worktrees](#11-diseño-backlog-único-y-limpieza-de-worktrees)
12. [El agente auditor de la Fase 3.5](#12-el-agente-auditor-de-la-fase-35)
13. [Riesgos y deuda](#13-riesgos-y-deuda)
14. [Criterios de aceptación](#14-criterios-de-aceptación)

---

## 1. Por qué existe este spec

La documentación del proyecto dejó de ser navegable. No por falta de contenido — hay mucho
y bueno — sino porque **nada define qué documento responde qué pregunta, ni qué obliga a
actualizarlo**. Los síntomas son medibles y están en la sec. 2: dos BACKLOG con contenido
distinto, diagramas que mezclan tablas que no existen con tablas que faltan, cero
documentación de seguridad, y planes de ejecución que crecieron hasta las 833 líneas
mezclando protocolo de trabajo, fases cerradas y fases futuras en el mismo archivo.

El objetivo declarado por el dueño del proyecto es **tener una estructura de documentación
más limpia**. Este spec la define y la impone; no se limita a mover archivos.

Hay una causa raíz detrás de casi todos los síntomas: **la documentación no tiene ciclo de
vida**. Un DER se escribe una vez y nadie lo vuelve a mirar cuando entra una migración.
Un plan se escribe para una fase y sigue ahí cuando la fase cierra. Una idea se anota en
un BACKLOG y otra persona anota la siguiente en el otro. El sistema que define este spec
ataca esa causa, no solo los síntomas.

## 2. Hallazgos verificados

Todo lo que sigue se comprobó ejecutando comandos contra este repositorio el 2026-08-16,
no por suposición.

### 2.1 La plantilla de cuatro archivos ya existe y funciona

`docs/spec/feature-0002-field-app-parto-redesign/` contiene `spec.md` (396 líneas), `plan.md` (227),
`tasks.md` (167) y `test-e2e.md` (273) — medido el 2026-08-16, cuando la carpeta vivía en la
rama `feature/field-app-parto-redesign`. El 2026-08-17 se trajo al repo y se alineó con las
plantillas que salieron de ella, así que hoy son 409 / 269 / 168 / 282. Es el único lugar
donde un trabajo
tiene decisión, secuencia, checklist y verificación separadas y enlazadas entre sí. **Este
spec no inventa una convención nueva: promueve esa a estándar.**

Su `tasks.md` fija además la convención de marcas que se reutiliza:
`[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

### 2.2 La convención de `docs/` está escrita y se está incumpliendo

`AGENTS.md` dice, literal:

> - **Planes de ejecución** → `docs/spec/`. Uno por fase.
> - **El resto de los documentos vive en la raíz de `docs/`**, en `MAYÚSCULAS.md`. No se
>   crean subcarpetas nuevas sin una razón que se pueda escribir en una línea.
> - Al mover o renombrar un documento, **arreglá las referencias en el mismo commit**. Ojo
>   con las citas en prosa desde el código (`PLAN-FASE-3-4 sec.2.2` aparece en ~14 archivos
>   de `src/`, `tests/` y `clients/`): son por nombre, no por ruta, así que mover no las
>   rompe pero **renombrar sí**.

Dos cosas de aquí importan. La primera: la regla de "uno por fase" hoy no se cumple —
`PLAN-FASE-3-4.md` cubre **dos** fases y además el protocolo de trabajo de todo el
proyecto. La segunda: el propio `AGENTS.md` **subestima el conteo de citas** ("~14
archivos"). El número real está en sec. 2.3.

### 2.3 Las citas desde el código son 238, no 14

| Documento | `.cs` | `.ts` | `.tsx` | **Código** | `.md` | **Total** |
|---|---|---|---|---|---|---|
| `PLAN-FASE-3-5-PORCINO` | 61 | 8 | 16 | **85** (72 archivos) | 113 (20 archivos) | 198 |
| `PLAN-FASE-3-4` | 14 | 7 | 2 | **23** (20 archivos) | 17 (11 archivos) | 40 |
| | | | | **108** | **130** | **238** |

Las citas son por **nombre y sección** (`PLAN-FASE-3-5-PORCINO.md sec.3.5a.5 task 3`), no
por ruta. Esto tiene una consecuencia de diseño directa, recogida en la decisión **D6**: si
la numeración de secciones se preserva, el reapuntado es un reemplazo mecánico verificable
por `grep`, no 238 decisiones editoriales.

### 2.4 Hay dos BACKLOG con contenido real y distinto

- `BACKLOG.md` (raíz, 334 líneas): *"el parking lot del proyecto"*. Deuda organizada por
  sub-rama, con ítems abiertos declarados no bloqueantes.
- `docs/BACKLOG.md` (192 líneas): *"ideas fuera de la fase actual"*, organizadas por fase,
  invocando la regla anti-estancamiento #2 del `ROADMAP.md`.

Ninguno es basura y ninguno menciona al otro. `AGENTS.md` sec. "Dónde están las cosas"
apunta solo al de la raíz.

### 2.5 Los diagramas mezclan lo real con lo aspiracional, sin marcarlo

Los cinco archivos de `docs/diagramas/` tienen la **misma fecha de modificación:
2026-08-06**. Auditado `der-3-grupos-inventario.mermaid`, declara diez entidades, entre
ellas:

- `PADDOCKS` y `GRAZING_MOVEMENTS` — pertenecen a **Fase 5**, no existen en la base.
- `UNITS` — una tabla global de unidades que **nunca se construyó**. Lo que sí existe es
  `unit_conversions` por ítem.

Y **le falta** `feed_stages`, que existe en la base desde la migración
`20260809050404_AddFeedStages`. El diagrama es simultáneamente aspiracional y obsoleto, y
no dice cuál parte es cuál. Los otros cuatro DER quedan pendientes de la misma auditoría
(tarea del `tasks.md`, no supuesto de este spec).

**No existe ningún diagrama de flujo.** Ni sincronización, ni autenticación, ni el consumo
de alimento con descuento FIFO — los tres procesos con más partes móviles del sistema.

### 2.6 No existe documentación de seguridad

No hay `SEGURIDAD.md` ni equivalente. Lo que hay está repartido y es parcial: ADR-0007
(modelo de permisos en BD), ADR-0008 (protocolo de sincronización, que incluye la
autorización del pull), `LEGAL-ECUADOR.md` (cumplimiento, no seguridad técnica).
`src/Hato.Api/Program.cs` configura una política CORS (`AdminWebCors`, líneas 12–42) y
monta `UseAuthentication()` / `UseAuthorization()` (líneas 44–45), pero **ningún documento
describe el modelo de autenticación de punta a punta**: emisión y expiración de tokens,
tokens de refresco, hashing de contraseñas, qué endpoint exige qué permiso, ni dónde están
los huecos conocidos.

### 2.7 Seis worktrees de agente ensucian toda búsqueda en el editor

`git worktree list` reporta seis worktrees vivos bajo `.claude/worktrees/`, todos apuntando
a ramas feature ya mergeadas, más uno *prunable* en `/tmp`:

```
agent-a6c890111a45ff939  feature/livestock-treatment-dose-logic
agent-a8555d67733b8cc70  feature/field-app-plausibility-wiring
agent-a8c9f14ca85bc27ad  feature/field-app-lot-registration
agent-acb103415e30d2b2a  feature/sync-animal-event-pull
agent-ad5f1515cadb3383e  feature/field-app-treatment-ui
agent-aed17ae3da9b4cc92  feature/inventory-feed-stage
```

No están versionados — los excluye `.git/info/exclude:11` —, pero cada uno contiene una
copia completa del repositorio. El efecto práctico: **una búsqueda de `ROADMAP.md` en el
editor devuelve siete resultados y solo uno es real.** Este hallazgo salió de una pregunta
directa del dueño del proyecto ("no sé por qué hay 2 roadmap"), y la respuesta es que solo
hay uno: `docs/ROADMAP.md`.

### 2.8 `PLAN-FASE-3-4.md` contiene cuatro cosas distintas

| Sección | Contenido | Naturaleza |
|---|---|---|
| 1 | Protocolo de feature: ciclo de 9 pasos, checklist de autorrevisión de 10 puntos atada a artículos de la Constitución, ritmo y bloques | **Transversal** |
| 2.1 | Exigencia de pruebas por capa (Domain, Application, Infra, Angular, React Native) | **Transversal** |
| 2.2 | Los 10 escenarios obligatorios de sincronización | Fase 3 |
| 2.3 | Exigencias especiales de pruebas de dinero | Fase 4 |
| 2.4–2.5 | Umbrales de salida y refuerzos de CI | Por fase |
| 3 | Fase 3 completa: bloques 3.A–3.C, ~11 ramas | Fase 3 |
| 4 | Fase 4 completa: bloques 4.A–4.D, ~11 ramas | Fase 4 |
| 5–6 | ADRs a escribir, riesgos y frenos de emergencia | Mixto |

Un archivo llamado "plan de las fases 3 y 4" es, en realidad, el manual de trabajo del
proyecto con dos planes adentro. Esa es la razón por la que la regla "uno por fase" de
`AGENTS.md` nunca pudo cumplirse.

### 2.9 Cinco piezas de Fase 4 ya están construidas dentro de la Fase 3.5

Verificado leyendo el código y ADR-0026:

| Adelanto | Dónde vive | Contrato de caducidad declarado |
|---|---|---|
| Recepción pre-Purchasing (ADR-0026) | Contrato declarado en `docs/adr/0026-recepcion-inventario-minima-pre-purchasing.md:456` y `docs/ROADMAP.md:167`. Campos en código: `SupplierLabel`, `InvoiceReference`, `ReceivedAt` (`InventoryItem.cs:186-234`) | ADR/ROADMAP: "deprecado cuando llegue Purchasing (Fase 4)". El código no repite esa frase: el comentario de `SupplierLabel` (líneas 188-189) dice *"Will become an FK to suppliers in Fase 4 (Purchasing); the label is preserved on existing rows as historical truth."* |
| Aviso en la UI | `inventory-batches-section.component.ts:25` | La interfaz **le dice al usuario**: "Cuando llegue Purchasing (Fase 4), este flujo se reemplazará por Recibir orden de compra" |
| Evento sin outbox | `Events/InventoryReceptionRecorded.cs:10` | "reopens if Fase 4" |
| FK cross-schema diferida | `AnimalEvent.cs:78`, `AnimalEventConfiguration.cs:51` | "Las FKs estrictas llegan en Fase 4" |
| Subida de fotos | `eventService.ts:163` | "Needs the attachments module (Fase 4)" |

Estos contratos de caducidad viven hoy **solo en comentarios de código**. El día que se
abra Purchasing, nadie tiene una lista de qué jubilar.

## 3. Decisiones fijadas

Cada decisión es una elección tomada, no una opción a evaluar. Las que fueron consultadas
con el dueño del proyecto lo indican.

- **D1 — Un documento existe si responde una pregunta que ningún otro responde.**
  Es la regla que gobierna toda la taxonomía de la sec. 5. Crear un documento que solapa
  con otro es la falla que produjo los dos BACKLOG.

- **D2 — Los planes por fase se absorben; los archivos viejos se borran.**
  *(Decidido por el dueño: "Absorber por completo".)* `PLAN-FASE-3-5-PORCINO.md` y
  `PLAN-FASE-3-4.md` desaparecen. Su contenido se reorganiza en carpetas por fase. Se
  evaluaron y descartaron: complementar (dos fuentes de verdad que divergen) y migrar con
  stub (mantiene un archivo puntero sin valor propio).

- **D3 — Lo transversal sube a la raíz de `docs/`; la Fase 3 recibe su propia carpeta.**
  *(Decidido por el dueño.)* Las secciones 1 y 2.1 de `PLAN-FASE-3-4.md` no pertenecen a
  ninguna fase y no pueden morir con el archivo. Ver sec. 8.

- **D4 — La profundidad se calibra por cercanía.**
  *(Decidido por el dueño.)* Un `tasks.md` de micro-tareas para una fase sin ADR ni código
  es ficción que envejece mal y da falsa sensación de plan. Ver la tabla de la sec. 7.1.

- **D5 — Las Fases 4 y 5 reciben únicamente `spec.md`.**
  *(Decidido por el dueño: "que llegue cuando deba llegar".)* Sin `plan.md`, sin
  `tasks.md`, sin `test-e2e.md`. Se escriben cuando la fase se abra, y con el
  `PROTOCOLO-DE-TRABAJO.md` y las plantillas ya disponibles el costo de escribirlos
  entonces es bajo.

- **D6 — La numeración de secciones de los planes viejos se preserva íntegra.**
  Requisito duro, derivado del hallazgo 2.3. `sec.3.5a.5 task 3` debe seguir llamándose
  `3.5a.5 task 3` dentro de `fase-3-5/spec.md`. Esto convierte el reapuntado de 238 citas
  en un reemplazo de nombre verificable por `grep` en vez de 238 juicios editoriales. **Si
  una sección tuviera que renumerarse, se documenta el mapeo viejo → nuevo en
  `tasks.md`.**

- **D7 — Los adelantos de Fase 4 se documentan en la Fase 3.5, y la Fase 4 queda virgen.**
  *(Decidido por el dueño.)* Las cinco piezas del hallazgo 2.9 se documentan en
  `fase-3-5/spec.md` con su contrato de caducidad explícito. `fase-4/spec.md` declara el
  objetivo limpio y enlaza esa lista como deuda heredada, en vez de nacer contaminado con
  medio Purchasing ya construido.

- **D8 — La planificación previa de Fase 4 se conserva como insumo, no como compromiso.**
  *(Recomendación aceptada por el dueño.)* Los bloques 4.A–4.D con sus ~11 ramas van a un
  apéndice de `fase-4/spec.md` rotulado **"Planificación previa (2026-08-02) — insumo, no
  compromiso"**. El objetivo queda virgen arriba; el trabajo de diseño no se tira.

- **D9 — Un solo BACKLOG: `docs/BACKLOG.md`.**
  *(Recomendación aceptada.)* Se funden conservando ambas estructuras como secciones. La
  raíz queda con `README.md`, `AGENTS.md` y `LICENSE` únicamente.

- **D10 — Una sola carpeta de plantillas: `docs/plantillas/`.**
  *(Recomendación aceptada.)* Incluye mover `docs/adr/TEMPLATE.md` allí y actualizar su
  referencia en `AGENTS.md`. La razón que exige la convención de `docs/`, en una línea:
  *"las plantillas se buscan en un solo lugar".*

- **D11 — Todo elemento aspiracional de un diagrama se marca o no entra.**
  Un DER que dibuja `PADDOCKS` sin decir que no existe es peor que no tenerlo: miente con
  autoridad. Ver sec. 10.

- **D12 — `SEGURIDAD.md` se escribe auditando el código, no copiando los ADR.**
  Y lo que la auditoría encuentre roto va al documento y a `docs/BACKLOG.md`, no a un
  comentario. Ver sec. 9.

- **D13 — El agente auditor marca `[x]` solo contra código verificado.**
  Nunca contra lo que un documento afirme. Ver sec. 12.

- **D14 — Los worktreees viejos se limpian antes de todo lo demás.**
  Es el primer commit. Sin eso, cualquier `grep` de verificación de esta misma rama
  devuelve siete copias de cada resultado.

## 4. Alcance

### Entra

- Limpieza de los seis worktrees de agente y el prunable.
- Fusión de los dos BACKLOG en `docs/BACKLOG.md`.
- `docs/plantillas/` con seis plantillas.
- `docs/DOCUMENTACION.md` — el sistema: taxonomía, ciclo de vida, convenciones.
- `docs/PROTOCOLO-DE-TRABAJO.md` — extraído de `PLAN-FASE-3-4.md` secs. 1 y 2.1.
- `docs/SEGURIDAD.md` — nuevo, desde auditoría del código.
- `docs/spec/plan-0001-fase-3/` — cuatro archivos, histórico.
- `docs/spec/plan-0002-fase-3-5/` — cuatro archivos, fidelidad máxima.
- `docs/spec/plan-0003-fase-4/spec.md` — solo el spec.
- `docs/spec/plan-0004-fase-5/spec.md` — solo el spec.
- Borrado de `PLAN-FASE-3-5-PORCINO.md` y `PLAN-FASE-3-4.md`.
- Reapuntado de las 238 citas.
- Corrección de los cinco DER y tres diagramas de flujo nuevos.
- Actualización de `AGENTS.md` en lo que este spec cambia.

### No entra

- **Reescribir `SOUL.md`, `CONSTITUTION.md`, `GLOSSARY.md`, `ARCHITECTURE.md`,
  `DATA-MODEL.md`, `LEGAL-ECUADOR.md`, `BACKUPS.md` ni `ROADMAP.md`.** Se les actualizan
  las referencias que cambien y nada más. Su contenido es bueno; tocarlo aquí sería un
  segundo propósito en el mismo PR (regla 9).
- **`docs/spec/sub_planes/` y `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` estaban fuera de alcance por
  este motivo**, y el dueño del proyecto pidió meter los dos el 2026-08-17, ya con las
  plantillas disponibles. Hoy `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` es
  `docs/spec/feature-0001-admin-web-animal-groups/`, con los cuatro documentos y marcado como archivado;
  y los ocho sub-planes son `docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-A.md` …
  `3.5b.5-C.md`, bajo el dueño que les corresponde y sin el prefijo
  `PLAN-FASE-3-5-PORCINO-` heredado del plan borrado. Ninguno de los archivos sueltos
  existe ya. Ver sec. 13.
- **`field-app-parto-redesign/`**. Es el modelo, no el objeto.
- **Cualquier cambio de código que no sea un comentario reapuntado.** En particular, la
  normalización de unidades de inventario va en su propia rama,
  `feature/inventory-unit-normalisation`.
- **Escribir `plan.md`/`tasks.md`/`test-e2e.md` de las Fases 4 y 5** (D5).

## 5. Diseño: taxonomía de la documentación

El corazón del sistema. Cada documento se define por **la pregunta que responde**; si dos
documentos responden la misma, uno sobra (D1).

| Documento | Pregunta que responde | Cambia cuando |
|---|---|---|
| `README.md` | ¿Cómo levanto y corro esto? | Cambia el arranque, el stack o las pruebas. |
| `SOUL.md` | ¿Por qué existe este proyecto? | Casi nunca. |
| `CONSTITUTION.md` | ¿Qué reglas no se rompen? | Con un ADR que la enmiende. |
| `AGENTS.md` | ¿Cómo trabaja un agente aquí? | Cambia una convención. |
| `PROTOCOLO-DE-TRABAJO.md` | ¿Cómo se lleva una rama de la idea al merge? | Cambia el ciclo o la exigencia de pruebas. |
| `DOCUMENTACION.md` | ¿Dónde va lo que voy a escribir? | Nace un tipo de documento nuevo. |
| `ROADMAP.md` | ¿En qué fase estamos y qué la cierra? | Se abre o cierra una fase. |
| `ARCHITECTURE.md` | ¿Cómo está partido el sistema? | Nace un módulo o cambia un límite. |
| `DATA-MODEL.md` | ¿Qué datos existen y por qué así? | Entra una migración estructural. |
| `SEGURIDAD.md` | ¿Cómo se protege y qué está expuesto? | Cambia auth, permisos o se halla un hueco. |
| `GLOSSARY.md` | ¿Cómo se llama esto en español y en código? | Aparece un término de dominio nuevo. |
| `LEGAL-ECUADOR.md` | ¿Qué exige la ley ecuatoriana? | Cambia la norma. |
| `BACKUPS.md` | ¿Cómo se respalda y se restaura? | Cambia la estrategia. |
| `BACKLOG.md` | ¿Qué sabemos que falta y decidimos no hacer ahora? | Continuamente. |
| `adr/NNNN-*.md` | ¿Por qué se decidió esto y qué se descartó? | Nunca: un ADR se reemplaza, no se edita. |
| `spec/<x>/spec.md` | ¿Qué se construye y qué queda fijado? | Antes de implementar. |
| `spec/<x>/plan.md` | ¿En qué orden y en qué commits? | Al replanificar. |
| `spec/<x>/tasks.md` | ¿Qué falta exactamente? | Continuamente, durante la ejecución. |
| `spec/<x>/test-e2e.md` | ¿Cómo compruebo a mano que funciona? | Cambia el flujo de usuario. |
| `diagramas/*.mermaid` | ¿Cómo se ve esto? | Entra una migración o cambia un flujo. |

### 5.1 Ciclo de vida — la regla que faltaba

La causa raíz de la sec. 1 se ataca con **disparadores explícitos**, que se agregan a la
checklist de autorrevisión del `PROTOCOLO-DE-TRABAJO.md`:

- **Entra una migración** → se revisa el DER del núcleo afectado y `DATA-MODEL.md`.
- **Nace un endpoint** → se revisa `SEGURIDAD.md` (qué permiso exige).
- **Nace un término de dominio** → `GLOSSARY.md` (ya es la regla del Art. 20).
- **Se cierra una fase** → retrospectiva en `ROADMAP.md`, `tasks.md` de la fase cerrado, y
  revisión del `BACKLOG.md` para promover o descartar.
- **Se decide algo estructural** → ADR, antes de implementar.

### 5.2 Archivado

Un plan de una fase cerrada **no se borra**: su carpeta permanece con el `tasks.md`
completo como registro de lo que costó. Es el motivo por el que `fase-3/` existe (D3).
Lo que se archiva se marca en el encabezado, no se mueve de lugar.

## 6. Diseño: plantillas

`docs/plantillas/` con seis archivos. Cada plantilla lleva **instrucciones embebidas** en
citas de bloque y **un ejemplo corto real** del propio repositorio, porque una plantilla
vacía se llena mal.

| Archivo | Base |
|---|---|
| `TEMPLATE-spec.md` | Estructura de `field-app-parto-redesign/spec.md`: encabezado con rama/fecha/fase/ADRs, índice, por qué existe, hallazgos verificados, decisiones fijadas, alcance (entra/no entra), diseño por secciones, riesgos y deuda, criterios de aceptación. |
| `TEMPLATE-plan.md` | Secuencia de commits, cada uno con propósito y criterio de terminado. |
| `TEMPLATE-tasks.md` | Checklist agrupada por commit, con la convención `[ ]` / `[x]` / `[!]` y el criterio **"Terminado:"** verificable por tarea. |
| `TEMPLATE-test-e2e.md` | Guion de verificación manual: precondiciones, pasos numerados, resultado esperado por paso. |
| `TEMPLATE-adr.md` | El actual `docs/adr/TEMPLATE.md`, movido sin cambios de contenido (D10). |
| `TEMPLATE-diagrama.md` | No es un esqueleto sino la **convención**: cómo se nombra, cómo se marca lo aspiracional (D11), y cuándo hay que actualizarlo. |

## 7. Diseño: conversión de las fases a carpetas

### 7.1 Profundidad por fase (D4, D5)

| Carpeta | `spec.md` | `plan.md` | `tasks.md` | `test-e2e.md` | Origen |
|---|---|---|---|---|---|
| `fase-3/` | Sí | Sí | Sí, casi todo `[x]` | Sí | `PLAN-FASE-3-4.md` secs. 2.2 y 3 |
| `fase-3-5/` | Sí, partido en `spec.md` + `spec-3.5a.md` (sec. 7.3) | Sí | Sí, micro-tareas | Sí | `PLAN-FASE-3-5-PORCINO.md` (833 líneas) |
| `fase-4/` | Sí | — | — | — | `PLAN-FASE-3-4.md` secs. 2.3 y 4 |
| `fase-5/` | Sí | — | — | — | `ROADMAP.md:235-243` |

### 7.2 `fase-3/` — histórico

Fase técnicamente terminada con un pendiente que no es código: **el piloto real**, una
semana de registros hechos por un empleado desde un teléfono, tal como exige el criterio de
salida. Su `tasks.md` refleja eso: todo `[x]` salvo esa línea.

Absorbe los 10 escenarios obligatorios de sincronización (sec. 2.2 del plan viejo), que
siguen siendo la referencia de qué debe probar cualquier cambio de sync.

### 7.3 `fase-3-5/` — fidelidad máxima

Es la fase en curso y la única con código que auditar. Absorbe las 833 líneas del plan
porcino **preservando su numeración** (D6): `3.5a.0` … `3.5a.9`, `3.5b.1` … y sus `task N`.

Su `spec.md` gana una sección propia, **"Adelantos de Fase 4 que viven aquí"**, con la
tabla del hallazgo 2.9: qué se construyó, por qué se adelantó y **cuál es su contrato de
caducidad**. Así, el día que se abra Purchasing, la lista de lo que hay que jubilar ya está
escrita y no depende de que alguien recuerde haber leído un comentario (D7).

Su `tasks.md` es el que audita el agente de la sec. 12.

**Partición del spec (desviación ejecutada, commit 091e94c).** Las 833 líneas no entraban en
un solo archivo sin recrear el riesgo que la sec. 13 anticipaba ("`fase-3-5/spec.md` hereda
833 líneas y vuelve a ser inmanejable"). Se partió en dos: `spec.md` se queda con las
decisiones (sec. 2), el bloque 3.5b y el resto de la fase; el bloque 3.5a —sus nueve ramas y
la numeración de tarea que 85 citas del código usan para apuntar— se mueve a
`spec-3.5a.md`. `plan.md`, `tasks.md` y `test-e2e.md` no se tocan: la partición es solo del
spec. Consecuencia documentada en T11.2 de `tasks.md`: el reapuntado de citas ya no puede
ser un único patrón, tiene que rutear `3.5a.*` a `spec-3.5a.md` antes que el resto a
`spec.md`.

### 7.4 `fase-4/` — objetivo virgen

Estructura:

1. Objetivo y criterio de salida, tomados del `ROADMAP.md:220-231`.
2. **Deuda heredada de la Fase 3.5**: enlace a la sección de adelantos de `fase-3-5/spec.md`,
   con las cinco piezas y sus contratos de caducidad. Esto es lo que la Fase 4 debe jubilar
   antes de considerarse cerrada.
3. Preguntas abiertas que hay que responder **antes** de poder planificar: proveedor
   autorizado del SRI, plan de cuentas, y qué acepta realmente el contador.
4. **Apéndice — "Planificación previa (2026-08-02): insumo, no compromiso"** con los
   bloques 4.A–4.D (D8).

Las exigencias de pruebas de dinero (sec. 2.3 del plan viejo) **no** van aquí: van a
`PROTOCOLO-DE-TRABAJO.md`, donde vive el resto de la exigencia de pruebas.

### 7.5 `fase-5/` — objetivo virgen

Objetivo, criterio de salida y las preguntas abiertas que hay que responder antes de
planificar (registro sanitario ARCSA, el caso real del queso fresco, si Grazing entra
completo o recortado). Nada más. Es deliberado: no hay ADR, no hay código, y el
`ROADMAP.md:271` manda recortar alcance antes que extender plazos.

## 8. Diseño: `PROTOCOLO-DE-TRABAJO.md`

Documento nuevo en la raíz de `docs/`, con lo transversal rescatado de `PLAN-FASE-3-4.md`:

1. El ciclo de nueve pasos, de `develop` actualizado a merge.
2. La checklist de autorrevisión de diez puntos, atada a artículos de la Constitución.
3. Ritmo y bloques: ramas secuenciales dentro de un bloque, hito de validación entre
   bloques, y la regla de partir una feature que pase de una semana.
4. La exigencia de pruebas por capa (tabla de sec. 2.1 del plan viejo).
5. Las exigencias especiales de pruebas de dinero (sec. 2.3), que llegan de la Fase 4.
6. **Nuevo:** los disparadores de actualización documental de la sec. 5.1 de este spec.

`AGENTS.md` conserva su resumen de cinco líneas y **enlaza** aquí para el detalle. No se
duplica el contenido: `AGENTS.md` responde "cómo trabaja un agente", este responde "cómo se
lleva una rama al merge" (D1).

## 9. Diseño: `SEGURIDAD.md`

Se escribe **auditando el código** (D12). Contenido:

1. **Modelo de autenticación**: emisión de token, vigencia, refresco, cierre de sesión.
   Verificado contra `people.refresh_tokens` y los endpoints reales.
2. **Modelo de autorización**: permisos en BD (ADR-0007), roles, y la tabla
   **endpoint → permiso exigido**, generada leyendo los `Endpoints/*.cs`.
3. **Autorización del pull de sincronización**: `RequiredPermissionByCollection`, que es el
   punto donde un error entrega datos a quien no debe.
4. **Secretos**: qué vive en `.env`, qué en variables de entorno, y el precedente
   documentado en `ROADMAP.md:11` — GitGuardian atrapó una contraseña commiteada en Fase 0.
5. **Superficie expuesta**: CORS (`Program.cs:12-42`), puertos publicados por
   `docker-compose.yml`, y qué queda accesible en la red de la finca.
6. **Huecos conocidos**, con fecha y su entrada correspondiente en `docs/BACKLOG.md`.

Lo que la auditoría encuentre roto se documenta; no se arregla en esta rama (regla 9).

## 10. Diseño: diagramas

### 10.1 Convención (D11)

- Un DER por núcleo de `DATA-MODEL.md`, nombre `der-N-<tema>.mermaid`.
- **Todo elemento que no existe en la base se marca** con un comentario Mermaid
  `%% FUTURO — Fase N` inmediatamente encima de la entidad, o no se dibuja. Sin excepción.
- Encabezado obligatorio en cada archivo: fecha de última verificación y contra qué
  migración se verificó.

### 10.2 Trabajo concreto

- Auditar los cinco DER contra el esquema real y corregirlos. `der-3` ya tiene diagnóstico
  (hallazgo 2.5); los otros cuatro se auditan igual.
- Tres diagramas de flujo nuevos, que hoy no existen y son los procesos con más partes
  móviles: **push/pull de sincronización** (incluye idempotencia y cursor),
  **autenticación** (login, refresco, expiración) y **consumo de alimento** (conversión de
  unidad + descuento FIFO por lote).

## 11. Diseño: BACKLOG único y limpieza de worktrees

**BACKLOG (D9).** `docs/BACKLOG.md` queda como único, con dos secciones de primer nivel que
conservan íntegro lo que hoy vive en cada archivo: *"Deuda abierta por sub-rama"* (del de la
raíz) e *"Ideas fuera de la fase actual"* (del de `docs/`). No se pierde ni una línea; no se
fusionan ítems por parecerse. `BACKLOG.md` de la raíz se borra y se actualizan las
referencias en `AGENTS.md` y en los `tasks.md` que lo nombran.

**Worktrees (D14).** Primer commit de la rama. `git worktree remove` de los seis, más
`git worktree prune`. Verificación: `git worktree list` devuelve una sola línea.

## 12. El agente auditor de la Fase 3.5

Se lanza **después** de que exista `docs/spec/plan-0002-fase-3-5/tasks.md`, no antes.

- **Contexto limpio**, sin la conversación que produjo este spec. Su trabajo es mirar el
  repositorio con ojos nuevos, no confirmar lo que ya creemos.
- **Solo lectura.** No edita código. Su entregable es el `tasks.md` marcado más un informe.
- **Regla dura (D13):** marca `[x]` **únicamente** contra evidencia de código —archivo y
  línea—, jamás contra lo que un documento afirme. Si `ROADMAP.md` dice que algo se mergeó
  y el código no lo respalda, **lo reporta como discrepancia** en vez de marcarlo. Esa
  discrepancia es información valiosa: significa que un documento miente.
- **Entregable:** `tasks.md` con marcas, y una lista de discrepancias documento-vs-código
  que se resuelven antes de cerrar la rama.

## 13. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| **El reapuntado de 238 citas rompe referencias en silencio.** Una cita mal reemplazada no falla en compilación: es prosa en un comentario. | D6 preserva la numeración, así que el cambio es mecánico. `test-e2e.md` incluye un `grep` que falla si sobrevive cualquier mención a los nombres viejos, y otro que verifica que cada sección citada existe en el destino. |
| **`fase-3-5/spec.md` hereda 833 líneas y vuelve a ser inmanejable.** | Se parte el spec por bloque: `3.5a` (nueve ramas, la numeración de tarea citada 85 veces desde el código) se va a `spec-3.5a.md`; `3.5b` y el resto de la narrativa se quedan en `spec.md`, junto con las decisiones. Lo puramente ejecutable —secuencia, checklist— ya vivía aparte, en `plan.md` y `tasks.md`. |
| **El agente auditor marca de más por optimismo.** | D13 y la exigencia de archivo:línea en cada `[x]`. Las discrepancias son entregable obligatorio: un informe sin discrepancias en un repositorio de este tamaño es sospechoso, no tranquilizador. |
| **La auditoría de seguridad encuentra algo grave.** | Se documenta y se abre entrada en `docs/BACKLOG.md`; si es explotable, se corrige en rama propia y con prioridad, no dentro de este PR. |
| **Se pierde contenido al absorber.** | El borrado de los dos planes viejos ocurre en el **último** commit de la rama, no en el primero. Hasta entonces conviven con las carpetas nuevas y el diff es auditable. |

**Deuda que esta rama declaró y luego cerró:** la reorganización de `sub_planes/` (8 archivos)
y la conversión de `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` se anotaron primero como deuda en
`docs/BACKLOG.md`, por la regla de un PR = un propósito. El dueño del proyecto pidió meter
ambas dentro de la rama el 2026-08-17: las plantillas y la convención de carpeta ya existían
para entonces, así que el trabajo era aplicar una regla recién escrita, no abrir un segundo
propósito. Los sub-planes son hoy `docs/spec/plan-0002-fase-3-5/sub-planes/` (ver sec. 4).

Meter los sub-planes tuvo un efecto sobre las verificaciones que conviene dejar explícito:
el criterio 7 llevaba una **exclusión** `sub_planes/` porque esos ocho archivos cargaban el
nombre del plan borrado en el suyo propio y hacían imposible el "cero coincidencias". Al
renombrarlos, la exclusión sobra y el criterio pasa a cero real, sin excepciones. Esa
exclusión fue justamente el síntoma que destapó el problema de ubicación.

**Deuda que esta rama sí deja abierta:** los ADR que citan rutas viejas (0020, 0021, 0022).
No se editan retroactivamente; quedan declarados en `docs/BACKLOG.md` con su disparador.

## 14. Criterios de aceptación

1. `git worktree list` devuelve **una sola línea**.
2. Existe un único BACKLOG, en `docs/BACKLOG.md`, y ninguna línea de los dos originales se
   perdió.
3. `docs/plantillas/` tiene las seis plantillas, cada una con instrucciones y ejemplo.
4. Existen `docs/DOCUMENTACION.md`, `docs/PROTOCOLO-DE-TRABAJO.md` y `docs/SEGURIDAD.md`.
5. Existe `fase-3/` con sus cuatro archivos (`spec.md`, `plan.md`, `tasks.md`,
   `test-e2e.md`); `fase-3-5/` con esos mismos cuatro más `spec-3.5a.md` —cinco en
   total—, producto de partir el spec en dos para no recrear el archivo de 833 líneas
   (sec. 7.3, sec. 13 fila "hereda 833 líneas"), más su subcarpeta `sub-planes/` con los
   ocho sub-planes de las tres secciones partidas; y `fase-4/` y `fase-5/` con su `spec.md`.
   `docs/spec/` no contiene ningún `.md` suelto: todo plan es una carpeta.
6. `PLAN-FASE-3-5-PORCINO.md` y `PLAN-FASE-3-4.md` **no existen**.
7. `grep -rn "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4"` **en código** (`.cs`, `.ts`, `.tsx`),
   excluyendo `node_modules` y `.claude/`, devuelve **cero resultados, sin excepciones**.
   Este criterio llevaba una exclusión `sub_planes/` mientras esos ocho sub-planes
   conservaron el nombre del plan borrado en el suyo propio; al moverlos a
   `docs/spec/plan-0002-fase-3-5/sub-planes/` y renombrarlos a `3.5a.2-A.md` … `3.5b.5-C.md`, las
   24 citas del código quedaron repuntadas y la exclusión sobra (sec. 13).
   En `.md`, `grep -rln "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4" --include=*.md .` (mismas
   exclusiones) devuelve **16 archivos**, todos dentro de cinco categorías deliberadas
   (detalle y conteo por categoría en `test-e2e.md` V-5): esta carpeta
   (`reestructura-documentacion/`, 4), los encabezados de procedencia de `fase-3/` y
   `fase-3-5/` (4), los 6 ADR que citan el plan viejo como texto o enlace (un ADR no se
   edita, se reemplaza), `docs/BACKLOG.md` (declara esas deudas de enlace roto de forma
   explícita) y `docs/PROTOCOLO-DE-TRABAJO.md` (cita el plan viejo como origen histórico de
   un riesgo, no como ruta viva). Cualquier archivo `.md` fuera de estas cinco categorías, o
   cualquier conteo que no cierre en 16, es un reapuntado olvidado y falla el criterio.
8. Toda sección citada desde el código existe con el mismo número en su documento destino.
9. Los cinco DER están auditados contra el esquema real y todo elemento aspiracional está
   marcado.
10. Existen los tres diagramas de flujo nuevos.
11. `SEGURIDAD.md` incluye la tabla endpoint → permiso, generada leyendo los endpoints
    reales.
12. `fase-3-5/tasks.md` está auditado por el agente y sus discrepancias, resueltas.
13. `dotnet test` y `npm test` en verde — esta rama no toca código ejecutable, así que
    cualquier fallo es una regresión ajena que hay que detectar antes del merge.
14. `AGENTS.md` refleja las convenciones nuevas y ninguna de sus referencias apunta a un
    archivo borrado.
