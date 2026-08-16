# tasks.md — Desglose ejecutable

> Checklist de la rama `docs/reestructura-documentacion`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Worktrees

- [ ] **T0.1** `git worktree list` y guardar la salida en el PR como estado de partida.
- [ ] **T0.2** Inspeccionar cada uno de los seis worktrees por cambios sin commitear:
      `git -C .claude/worktrees/<w> status --porcelain`.
      **Terminado:** los seis salen vacíos, **o** el que no salga vacío queda reportado y
      sin borrar.
- [ ] **T0.3** `git worktree remove --force` de los seis limpios, más `git worktree prune`.
- [ ] **T0.4** **Compuerta:** `git worktree list | wc -l` imprime `1`.
      **Si no, detenerse.** Nada más de esta rama vale hasta que esto pase.

---

## Commit 1 — BACKLOG único

- [ ] **T1.1** Medir el estado de partida y anotarlo:
      `wc -l BACKLOG.md docs/BACKLOG.md` y
      `grep -c '^- \|^### ' BACKLOG.md docs/BACKLOG.md`.
- [ ] **T1.2** Construir `docs/BACKLOG.md` con dos secciones de primer nivel:
      `## Deuda abierta por sub-rama` (contenido de la raíz) e
      `## Ideas fuera de la fase actual` (contenido actual de `docs/`).
- [ ] **T1.3** Verificar que **no se perdió ninguna línea de contenido**: los conteos de
      T1.1 se conservan (solo bajan por encabezados consolidados, y se anota cuáles).
- [ ] **T1.4** Anotar con una línea las entradas relacionadas entre ambas secciones.
      **No fusionarlas.**
- [ ] **T1.5** `git rm BACKLOG.md`.
- [ ] **T1.6** `AGENTS.md`, sección "Dónde están las cosas": `BACKLOG.md` → `docs/BACKLOG.md`.
- [ ] **T1.7** Verificar que los tres archivos que este commit toca
      (`docs/BACKLOG.md`, `BACKLOG.md`, `AGENTS.md`) no dejan referencias a la raíz.
      **Terminado:** `grep -n "BACKLOG\.md" AGENTS.md` solo muestra `docs/BACKLOG.md`.

      > **Corrección durante la ejecución (2026-08-16).** La versión original de T1.7 exigía
      > el barrido completo del repositorio, que este commit no está autorizado a hacer: su
      > lista de archivos son tres. El barrido de las 9 referencias externas reales
      > (`ROADMAP.md`, `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` y `sub_planes/`) se movió a
      > **T11.4b**, junto al resto del reapuntado. Las de `docs/adr/` quedan intactas por
      > inmutabilidad de los ADR. Ver `test-e2e.md` V-2.

---

## Commit 2 — Plantillas

- [ ] **T2.1** `git mv docs/adr/TEMPLATE.md docs/plantillas/TEMPLATE-adr.md`, sin tocar el
      contenido.
- [ ] **T2.2** `AGENTS.md`: la ruta de la plantilla de ADR apunta al destino nuevo.
- [ ] **T2.3** `TEMPLATE-spec.md`, calcado de la estructura de este mismo `spec.md`:
      encabezado, índice, por qué existe, hallazgos verificados, decisiones fijadas,
      alcance (entra / no entra), diseño por secciones, riesgos y deuda, criterios de
      aceptación.
- [ ] **T2.4** `TEMPLATE-plan.md`: restricciones globales, secuencia de commits con
      propósito y archivos, orden y dependencias, descripción del PR.
- [ ] **T2.5** `TEMPLATE-tasks.md`: agrupación por commit, convención de marcas y el
      criterio **"Terminado:"** por tarea.
- [ ] **T2.6** `TEMPLATE-test-e2e.md`: precondiciones, escenarios numerados, resultado
      esperado por paso.
- [ ] **T2.7** `TEMPLATE-diagrama.md`: convención de nombre, marcado de lo aspiracional
      (D11), encabezado de fecha de verificación, y cuándo hay que actualizarlo.
- [ ] **T2.8** Cada plantilla lleva instrucciones embebidas **y un ejemplo corto real de
      este repositorio**. Una plantilla sin ejemplo no cuenta como terminada.
      El ejemplo debe ser **literal**: si se recorta, lleva elipsis visible. Un ejemplo
      presentado como completo que omite parte de la fuente es un defecto, no un detalle
      de estilo — es justo lo que esta tarea existe para impedir.

      > **Excepción, dictada durante la ejecución (2026-08-16): `TEMPLATE-adr.md`.**
      > T2.1 y el `spec.md` sec. 6 exigen moverla **sin cambios de contenido**; T2.8 exigía
      > un ejemplo embebido. Las dos no pueden cumplirse a la vez. Gana T2.1, porque el
      > spec es la autoridad vinculante y porque la plantilla de ADR **no necesita ejemplo
      > embebido**: tiene 26 ADR reales al lado, en `docs/adr/`, que son mejor ejemplo del
      > que cabría en una cita de bloque. La plantilla se queda tal cual.
- [ ] **T2.9** `ls docs/plantillas/*.md | wc -l` imprime `6`.
- [ ] **T2.10** `grep -rn "docs/adr/TEMPLATE\.md" --include=*.md . | grep -v node_modules |
      grep -v 'docs/planes/reestructura-documentacion/'` devuelve cero.
      La exclusión es la misma de siempre: esta carpeta narra la migración y sus menciones
      a rutas viejas son deliberadas (spec criterio 7, `test-e2e.md` V-2 y V-5).

---

## Commit 3 — `DOCUMENTACION.md`

- [ ] **T3.1** Tabla de taxonomía con las tres columnas del spec sec. 5: documento,
      pregunta que responde, cuándo cambia.
- [ ] **T3.2** Verificar cobertura: todo archivo de `ls docs/*.md` y de `ls *.md` aparece
      en la tabla. **Terminado:** la comparación se hace por comando, no a ojo.
- [ ] **T3.3** Sección de disparadores de actualización (spec sec. 5.1), los cinco.
- [ ] **T3.4** Sección de archivado (spec sec. 5.2): un plan de fase cerrada no se borra,
      se marca en el encabezado.
- [ ] **T3.5** `AGENTS.md` lo enlaza desde "Dónde están las cosas".

---

## Commit 4 — `PROTOCOLO-DE-TRABAJO.md`

- [ ] **T4.1** Trasladar sec. 1.1 del plan viejo: ciclo de nueve pasos.
- [ ] **T4.2** Trasladar sec. 1.2: checklist de autorrevisión, los 10 puntos con sus
      artículos de la Constitución.
- [ ] **T4.3** Trasladar sec. 1.3: ritmo y bloques.
- [ ] **T4.4** Trasladar sec. 2.1: tabla de exigencia de pruebas por capa.
- [ ] **T4.5** Trasladar sec. 2.3: exigencias de pruebas de dinero.
- [ ] **T4.6** Trasladar secs. 2.4–2.5: umbrales de salida y refuerzos de CI.
- [ ] **T4.7** **Agregar** los cinco disparadores documentales a la checklist de
      autorrevisión de T4.2.
- [ ] **T4.8** `AGENTS.md` conserva su resumen de cinco líneas y **enlaza** aquí.
      **Terminado:** `AGENTS.md` no repite los nueve pasos ni la tabla de pruebas (D1).

---

## Commit 5 — `fase-3/`

- [ ] **T5.1** `spec.md`: encabezado declarando procedencia (`PLAN-FASE-3-4.md` secs. 2.2
      y 3) y estado de la fase.
- [ ] **T5.2** Absorber sec. 2.2 — los 10 escenarios obligatorios de sincronización —
      **conservando el número `2.2`** (D6).
- [ ] **T5.3** Absorber sec. 3 — bloques 3.A–3.C — **conservando los números `3.x`** (D6).
- [ ] **T5.4** Incorporar el estado y la retrospectiva de `ROADMAP.md:33-56` (el cierre
      revertido y los tres defectos que lo motivaron) y `ROADMAP.md:109-142` (objetivo,
      criterio de salida y pendiente para cerrar).
- [ ] **T5.5** `plan.md`: la secuencia de ~11 ramas, marcada como ejecutada.
- [ ] **T5.6** `tasks.md`: todo `[x]` salvo el piloto real, que queda `[ ]` con la cita de
      `ROADMAP.md:121-125`.
- [ ] **T5.7** `test-e2e.md`: los 10 escenarios de sync como guion ejecutable.
- [ ] **T5.8** Marcar la carpeta como **archivada** en el encabezado, según la regla T3.4.
- [ ] **T5.9** Verificar que las secciones `2.2` y `3.x` existen con esos números exactos.
      **Terminado:** `grep -nE '^#+ .*(2\.2|3\.[ABC])' docs/planes/fase-3/spec.md` las
      encuentra.

---

## Commit 6 — `fase-3-5/`

- [ ] **T6.1** Repartir las 833 líneas por naturaleza: decisiones y hallazgos → `spec.md`;
      secuencia → `plan.md`; checklist → `tasks.md`; verificación → `test-e2e.md`.
- [ ] **T6.2** **Conservar toda la numeración** `3.5a.0`–`3.5a.9` y `3.5b.x` (D6).
      **Terminado:** existe un listado de las secciones citadas desde el código y cada una
      aparece con el mismo número en el destino.
- [ ] **T6.3** Sección **"Adelantos de Fase 4 que viven aquí"** con las cinco piezas del
      spec 2.9, cada una con archivo, línea y contrato de caducidad literal (D7).
- [ ] **T6.4** Incorporar el estado de `ROADMAP.md:146-217`, incluidos ADR-0024, ADR-0021 y
      la recepción de inventario del PR #94.
- [ ] **T6.5** **Enlazar** los ocho `sub_planes/`; no absorberlos (fuera de alcance).
- [ ] **T6.6** `tasks.md` con todas las tareas de 3.5a y 3.5b, **todas en `[ ]`**. Las
      marca el agente en la compuerta A, no este commit.
- [ ] **T6.7** `test-e2e.md` con el criterio de salida de 3.5a y 3.5b como guion.
- [ ] **T6.8** Verificar que ningún archivo de la carpeta pasa de ~400 líneas. Si `spec.md`
      se pasa, partirlo por bloque `3.5a` / `3.5b`.

---

## Compuerta A — Agente auditor

- [ ] **TA.1** Lanzar el agente de contexto limpio, solo lectura, con la regla D13
      explícita en el prompt: `[x]` **solo** contra archivo y línea.
- [ ] **TA.2** El agente entrega `tasks.md` marcado.
- [ ] **TA.3** El agente entrega la lista de discrepancias documento-vs-código.
- [ ] **TA.4** **Compuerta:** si la lista de discrepancias viene **vacía**, no aceptarla.
      Revisar el prompt y relanzar. Un repositorio de este tamaño con cero discrepancias
      significa que el agente no miró, no que todo esté bien.
- [ ] **TA.5** Verificar por muestreo **tres** marcas `[x]` del agente abriendo el archivo y
      la línea que citó. **Terminado:** las tres se sostienen.

---

## Commit 7 — Marcas verificadas

- [ ] **T7.1** Aplicar las marcas del agente a `docs/planes/fase-3-5/tasks.md`.
- [ ] **T7.2** Cada `[x]` cita archivo y línea. **Terminado:** no queda ningún `[x]` sin
      evidencia.
- [ ] **T7.3** Las discrepancias que no se resuelven acá se anotan en `docs/BACKLOG.md`.
- [ ] **T7.4** Las discrepancias que contradigan el `ROADMAP.md` se reportan al dueño antes
      de continuar. **No se corrige el ROADMAP en esta rama** (regla 9).

---

## Commit 8 — `fase-4/` y `fase-5/`

- [ ] **T8.1** `fase-4/spec.md` parte 1: objetivo y criterio de salida de
      `ROADMAP.md:220-231`.
- [ ] **T8.2** Parte 2: deuda heredada, enlazando la sección de adelantos de
      `fase-3-5/spec.md`, con las cinco piezas y sus contratos de caducidad.
- [ ] **T8.3** Parte 3: preguntas abiertas — proveedor autorizado del SRI, plan de cuentas,
      qué acepta el contador.
- [ ] **T8.4** Parte 4: apéndice **"Planificación previa (2026-08-02): insumo, no
      compromiso"** con los bloques 4.A–4.D (D8).
- [ ] **T8.5** Verificar que las exigencias de pruebas de dinero **no** están acá: ya viven
      en `PROTOCOLO-DE-TRABAJO.md` desde T4.5.
- [ ] **T8.6** `fase-5/spec.md`: objetivo, criterio de salida (`ROADMAP.md:235-243`) y
      preguntas abiertas — ARCSA, el caso del queso fresco, alcance de Grazing.
- [ ] **T8.7** **Verificar que no se creó `plan.md`, `tasks.md` ni `test-e2e.md`** en
      ninguna de las dos carpetas (D5).
      **Terminado:** `ls docs/planes/fase-4 docs/planes/fase-5` muestra solo `spec.md`.

---

## Commit 9 — `SEGURIDAD.md`

- [ ] **T9.1** Auditar el modelo de autenticación: emisión, vigencia, refresco y cierre de
      sesión, contra `people.refresh_tokens` y los endpoints reales.
- [ ] **T9.2** Auditar el modelo de autorización: permisos en BD (ADR-0007) y roles.
- [ ] **T9.3** Generar la tabla **endpoint → permiso exigido** leyendo los 19 archivos de
      `src/Hato.Api/Endpoints/`.
      **Terminado:** todo archivo de `Endpoints/*.cs` aparece en la tabla.
- [ ] **T9.4** Documentar la autorización del pull de sincronización
      (`RequiredPermissionByCollection`), que es donde un error entrega datos a quien no
      debe.
- [ ] **T9.5** Documentar el manejo de secretos, incluido el precedente de GitGuardian de
      `ROADMAP.md:11`.
- [ ] **T9.6** Documentar la superficie expuesta: CORS (`Program.cs:12-42`) y los puertos
      publicados por `docker-compose.yml`.
- [ ] **T9.7** Sección de huecos conocidos, con fecha y su entrada en `docs/BACKLOG.md`.
- [ ] **T9.8** **Si algo resulta explotable: detenerse y avisar al dueño.** No se arregla en
      esta rama, pero tampoco se publica sin avisar.

---

## Commit 10 — Diagramas

- [ ] **T10.1** Auditar `der-1-identidad.mermaid` contra el esquema real.
- [ ] **T10.2** Auditar `der-2-eventos.mermaid`.
- [ ] **T10.3** Auditar `der-3-grupos-inventario.mermaid`: quitar o marcar `PADDOCKS`,
      `GRAZING_MOVEMENTS` y `UNITS`; **agregar `FEED_STAGES`**.
- [ ] **T10.4** Auditar `der-4-reproduccion.mermaid`.
- [ ] **T10.5** Auditar `der-5-produccion-leche.mermaid`.
- [ ] **T10.6** Encabezado en los cinco con fecha de verificación y migración de
      referencia.
- [ ] **T10.7** Marcar todo lo aspiracional con `%% FUTURO — Fase N` (D11).
      **Terminado:** no queda ninguna entidad sin correspondencia en la base que no esté
      marcada.
- [ ] **T10.8** `flujo-sincronizacion.mermaid`: push/pull con idempotencia y cursor.
- [ ] **T10.9** `flujo-autenticacion.mermaid`: login, refresco, expiración.
- [ ] **T10.10** `flujo-consumo-alimento.mermaid`: conversión de unidad y descuento FIFO.
- [ ] **T10.11** Los tres flujos renderizan sin error de sintaxis Mermaid.

---

## Commit 11 — Reapuntado y borrado · PUNTO DE NO RETORNO

- [ ] **T11.1** Reapuntar **a mano** las 2 citas a `PLAN-FASE-3-4 sec.2.1` →
      `docs/PROTOCOLO-DE-TRABAJO.md`. **Antes** de cualquier `sed`.
      **Terminado:** `grep -rn "PLAN-FASE-3-4 sec\.2\.1"` en código devuelve cero.
- [ ] **T11.2** `sed` de las 85 citas de `PLAN-FASE-3-5-PORCINO` en código, **en dos pasos y
      en este orden**: primero las ~40 citas a `sec.3.5a.*` → `spec-3.5a.md`, después el
      resto (`sec.2.3`, `sec.7`, menciones sin sección) → `spec.md`. Invertir el orden manda
      todo a `spec.md` y rompe 40 referencias en silencio.
      El commit 6 partió el spec en dos para no recrear el archivo inmanejable que el
      `spec.md` sec. 13 advertía; los comandos exactos están en `plan.md` commit 11.
      **Terminado:** `grep -rn "spec-3.5a.md sec\.3\.5a"` en código devuelve ~40, y ninguna
      cita a `3.5a` quedó apuntando a `spec.md`.
- [ ] **T11.3** `sed` de las 21 citas restantes de `PLAN-FASE-3-4` en código.
- [ ] **T11.4** Reapuntar las 130 citas en `.md`, respetando las excepciones del criterio 7
      del spec.
- [ ] **T11.4b** Reapuntar las 9 referencias a `BACKLOG.md` de la raíz que quedaron del
      commit 1: `docs/ROADMAP.md` (1), `docs/planes/PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` (4) y
      `docs/planes/sub_planes/` (4) → `docs/BACKLOG.md`.
      **No tocar `docs/adr/`** (7 referencias): un ADR no se edita, se reemplaza.
      **Terminado:** el comando de `test-e2e.md` V-2, con sus dos exclusiones, devuelve cero.
- [ ] **T11.5** `git diff --stat` revisado archivo por archivo: **ningún cambio fuera de
      comentarios**. Un `sed` que tocó código ejecutable se revierte entero.
- [ ] **T11.6** `git rm docs/planes/PLAN-FASE-3-5-PORCINO.md docs/planes/PLAN-FASE-3-4.md`.
- [ ] **T11.7** `AGENTS.md`: la advertencia sobre citas en prosa se actualiza con los
      nombres y el conteo reales.

---

## Cierre

- [ ] **TC.0** Marcar las casillas de **este mismo archivo** en una sola pasada, contra el
      historial de la rama. Se hace acá y no commit a commit a propósito: marcar sobre la
      marcha metería una edición de `tasks.md` en cada diff y ensuciaría la revisión de
      cada commit. El seguimiento durante la ejecución vive en el ledger.
- [ ] **TC.0b** Anotar en `docs/BACKLOG.md` la deuda preexistente detectada en el commit 4:
      el texto heredado cita `AGENTS.md sec.5` y `sec.2`, pero `AGENTS.md` dejó de numerar
      sus encabezados. Las citas no resuelven. No se arregla en esta rama (regla 9: es otro
      propósito), pero deja de ser invisible.
- [ ] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo, los ocho escenarios.
- [ ] **TC.2** Los 14 criterios de aceptación del `spec.md` sec. 14, uno por uno.
- [ ] **TC.3** `dotnet test` en verde. Esta rama no toca código ejecutable: cualquier fallo
      es una regresión ajena que hay que detectar antes del merge.
- [ ] **TC.4** `npm test` en verde en `clients/field-app` y `clients/admin-web`.
- [ ] **TC.5** Abrir el PR con la descripción de [`plan.md`](./plan.md) sec. 15.
