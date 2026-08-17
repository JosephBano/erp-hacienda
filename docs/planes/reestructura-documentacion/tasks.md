# tasks.md — Desglose ejecutable

> Checklist de la rama `docs/reestructura-documentacion`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Worktrees

- [x] **T0.1** `git worktree list` y guardar la salida en el PR como estado de partida.
      El ledger (`.superpowers/sdd/plan/progress.md:67-68`) registra el cierre de esta
      compuerta: "6 worktrees inspeccionados (0 sin commitear, 0 sin push), removidos".
      No hay PR abierto (TC.5 sigue sin hacerse) así que la salida no quedó pegada en
      ningún PR — la evidencia disponible es el ledger, no el artefacto que pedía la letra
      de la tarea. Desviación: se cumplió el propósito (dejar constancia del estado de
      partida), no el medio literal ("en el PR").
- [x] **T0.2** Inspeccionar cada uno de los seis worktrees por cambios sin commitear.
      Mismo respaldo que T0.1: ledger línea 67, "0 sin commitear, 0 sin push". No queda
      rastro de los seis directorios individuales (ya se borraron), así que esto se marca
      contra el registro del ledger, no contra evidencia de archivo:línea del propio
      repositorio — es la excepción de esta compuerta: su evidencia es necesariamente
      anterior al estado actual del árbol.
- [x] **T0.3** `git worktree remove --force` de los seis limpios, más `git worktree prune`.
      `.claude/worktrees/` existe vacío (`ls` no devuelve entradas) y `git worktree list`
      no lista ninguno de los seis — consistente con que se removieron y se podó el
      registro.
- [x] **T0.4** **Compuerta:** `git worktree list | wc -l` imprime `1`.
      Verificado ahora mismo: `git worktree list` devuelve una sola línea (este
      directorio de trabajo, `docs/reestructura-documentacion`).

---

## Commit 1 — BACKLOG único

- [x] **T1.1** Medir el estado de partida y anotarlo:
      `wc -l BACKLOG.md docs/BACKLOG.md` y
      `grep -c '^- \|^### ' BACKLOG.md docs/BACKLOG.md`.
      `.superpowers/sdd/plan/commit-1-report.md:53-66`: 334+192 líneas, 82+24 = 106
      líneas de lista/H3, comando y salida citados literalmente.
- [x] **T1.2** Construir `docs/BACKLOG.md` con dos secciones de primer nivel:
      `## Deuda abierta por sub-rama` (contenido de la raíz) e
      `## Ideas fuera de la fase actual` (contenido actual de `docs/`).
      Commit `36787c6`. Verificado ahora: `grep -n '^## ' docs/BACKLOG.md` muestra ambos
      encabezados de primer nivel.
- [x] **T1.3** Verificar que **no se perdió ninguna línea de contenido**: los conteos de
      T1.1 se conservan (solo bajan por encabezados consolidados, y se anota cuáles).
      `commit-1-report.md:77-104`: 556 líneas después (piso esperado 514, cumplido);
      `grep -c '^- \|^### '` da exactamente 106, igual a la suma original.
- [x] **T1.4** Anotar con una línea las entradas relacionadas entre ambas secciones.
      **No fusionarlas.** `commit-1-report.md:26-44` lista las 5 notas `*(Relacionado:
      ...)*` agregadas como líneas nuevas, sin sustituir contenido.
- [x] **T1.5** `git rm BACKLOG.md`. Confirmado: `ls BACKLOG.md` → no existe;
      `git log --diff-filter=D -- BACKLOG.md` muestra el borrado en `36787c6`.
- [x] **T1.6** `AGENTS.md`, sección "Dónde están las cosas": `BACKLOG.md` → `docs/BACKLOG.md`.
      Verificado ahora: `grep -n "BACKLOG" AGENTS.md` solo devuelve `docs/BACKLOG.md`
      (línea 41, Art. 9, y línea 81, "Dónde están las cosas").
- [x] **T1.7** Verificar que los tres archivos que este commit toca
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

- [x] **T2.1** `git mv docs/adr/TEMPLATE.md docs/plantillas/TEMPLATE-adr.md`, sin tocar el
      contenido. `commit-2-report.md:8-9`: movido con `git mv`, byte-idéntico
      (`diff` contra el `HEAD` previo → `IDENTICAL`). Confirmado ahora: `docs/adr/TEMPLATE.md`
      no existe, `docs/plantillas/TEMPLATE-adr.md` sí (18 líneas).
- [x] **T2.2** `AGENTS.md`: la ruta de la plantilla de ADR apunta al destino nuevo.
      `commit-2-report.md:23-25`: un solo hunk, cambia solo esa línea.
- [x] **T2.3** `TEMPLATE-spec.md`, calcado de la estructura de este mismo `spec.md`:
      encabezado, índice, por qué existe, hallazgos verificados, decisiones fijadas,
      alcance (entra / no entra), diseño por secciones, riesgos y deuda, criterios de
      aceptación. `docs/plantillas/TEMPLATE-spec.md` existe, 121 líneas.
- [x] **T2.4** `TEMPLATE-plan.md`: restricciones globales, secuencia de commits con
      propósito y archivos, orden y dependencias, descripción del PR.
      `docs/plantillas/TEMPLATE-plan.md` existe, 86 líneas.
- [x] **T2.5** `TEMPLATE-tasks.md`: agrupación por commit, convención de marcas y el
      criterio **"Terminado:"** por tarea. `docs/plantillas/TEMPLATE-tasks.md` existe,
      50 líneas.
- [x] **T2.6** `TEMPLATE-test-e2e.md`: precondiciones, escenarios numerados, resultado
      esperado por paso. `docs/plantillas/TEMPLATE-test-e2e.md` existe, 55 líneas.
- [x] **T2.7** `TEMPLATE-diagrama.md`: convención de nombre, marcado de lo aspiracional
      (D11), encabezado de fecha de verificación, y cuándo hay que actualizarlo.
      `docs/plantillas/TEMPLATE-diagrama.md` existe, 56 líneas.
- [x] **T2.8** Cada plantilla lleva instrucciones embebidas **y un ejemplo corto real de
      este repositorio**. Una plantilla sin ejemplo no cuenta como terminada.
      El ejemplo debe ser **literal**: si se recorta, lleva elipsis visible. Un ejemplo
      presentado como completo que omite parte de la fuente es un defecto, no un detalle
      de estilo — es justo lo que esta tarea existe para impedir.
      `commit-2-report.md:27-47` cita la fuente exacta de cada ejemplo embebido en las
      cinco plantillas nuevas. Hubo una ronda de corrección real (`cc837af`): el ejemplo
      E2E-2 de `TEMPLATE-test-e2e.md` faltaba 2 de 5 viñetas de la fuente, sin elipsis —
      exactamente la falla que esta tarea existe para impedir. Se corrigió comparando
      línea por línea contra la fuente, que entonces vivía en la rama
      `feature/field-app-parto-redesign` y hoy vive en el repo:
      `docs/planes/field-app-parto-redesign/test-e2e.md` (la rama se trajo acá y se borró el
      2026-08-17; solo contenía esos cuatro documentos).

      > **Excepción, dictada durante la ejecución (2026-08-16): `TEMPLATE-adr.md`.**
      > T2.1 y el `spec.md` sec. 6 exigen moverla **sin cambios de contenido**; T2.8 exigía
      > un ejemplo embebido. Las dos no pueden cumplirse a la vez. Gana T2.1, porque el
      > spec es la autoridad vinculante y porque la plantilla de ADR **no necesita ejemplo
      > embebido**: tiene 26 ADR reales al lado, en `docs/adr/`, que son mejor ejemplo del
      > que cabría en una cita de bloque. La plantilla se queda tal cual.
- [x] **T2.9** `ls docs/plantillas/*.md | wc -l` imprime `6`. Verificado ahora mismo:
      imprime `6`.
- [x] **T2.10** `grep -rn "docs/adr/TEMPLATE\.md" --include=*.md . | grep -v node_modules |
      grep -v 'docs/planes/reestructura-documentacion/'` devuelve cero.
      La exclusión es la misma de siempre: esta carpeta narra la migración y sus menciones
      a rutas viejas son deliberadas (spec criterio 7, `test-e2e.md` V-2 y V-5).
      Verificado ahora mismo: el comando devuelve cero líneas.

---

## Commit 3 — `DOCUMENTACION.md`

- [x] **T3.1** Tabla de taxonomía con las tres columnas del spec sec. 5: documento,
      pregunta que responde, cuándo cambia. Commit `8786fde`; `commit-3-report.md:20`
      confirma las tres columnas. `docs/DOCUMENTACION.md` existe, 20 entradas en la tabla.
- [x] **T3.2** Verificar cobertura: todo archivo de `ls docs/*.md` y de `ls *.md` aparece
      en la tabla. **Terminado:** la comparación se hace por comando, no a ojo.
      Verificado ahora mismo con el bucle real (`for f in ls *.md docs/*.md; grep -q
      "$base" docs/DOCUMENTACION.md`): cero líneas `HUÉRFANO` (V-6 de `test-e2e.md`).
- [x] **T3.3** Sección de disparadores de actualización (spec sec. 5.1), los cinco.
      `commit-3-report.md:23`, sec. 3 de `docs/DOCUMENTACION.md`.
- [x] **T3.4** Sección de archivado (spec sec. 5.2): un plan de fase cerrada no se borra,
      se marca en el encabezado. `commit-3-report.md:24`. Aplicada en la práctica: el
      encabezado de `docs/planes/fase-3/spec.md` dice "**Documento archivado**" citando
      esta misma regla.
- [x] **T3.5** `AGENTS.md` lo enlaza desde "Dónde están las cosas".
      `commit-3-report.md:25`: un solo hunk, una sola línea. Confirmado ahora:
      `AGENTS.md` línea 78 enlaza `docs/DOCUMENTACION.md`.

---

## Commit 4 — `PROTOCOLO-DE-TRABAJO.md`

- [x] **T4.1** Trasladar sec. 1.1 del plan viejo: ciclo de nueve pasos.
      `commit-4-report.md:14`: trasladado verbatim (desviación 5 declarada: un párrafo
      introductorio cambia "~21 ramas del plan" por "cada rama del proyecto" — cambio de
      fondo correcto, documento transversal ya no describe un plan de 21 ramas).
      `docs/PROTOCOLO-DE-TRABAJO.md` sec. 1.1 existe con los nueve pasos.
- [x] **T4.2** Trasladar sec. 1.2: checklist de autorrevisión, los 10 puntos con sus
      artículos de la Constitución. `commit-4-report.md:15`: verbatim + los 5 disparadores
      agregados. `docs/PROTOCOLO-DE-TRABAJO.md` sec. 1.2 tiene 15 puntos (10 + 5).
- [x] **T4.3** Trasladar sec. 1.3: ritmo y bloques. `commit-4-report.md:16`: trasladado,
      con la ruta `BACKLOG.md` → `docs/BACKLOG.md` corregida (obligatoria por el commit 1).
- [x] **T4.4** Trasladar sec. 2.1: tabla de exigencia de pruebas por capa.
      `commit-4-report.md:17`: verbatim.
- [x] **T4.5** Trasladar sec. 2.3: exigencias de pruebas de dinero.
      `commit-4-report.md:19`: verbatim, numeración D6 preservada (salta de 2.1 a 2.3).
- [x] **T4.6** Trasladar secs. 2.4–2.5: umbrales de salida y refuerzos de CI.
      `commit-4-report.md:20-21`: verbatim.
- [x] **T4.7** **Agregar** los cinco disparadores documentales a la checklist de
      autorrevisión de T4.2. Hecho, con una desviación real y ya corregida: el commit
      original (`ceeffc9`) y su informe afirmaban que los 5 disparadores eran copia
      **literal** de `DOCUMENTACION.md` sec. 3; no lo eran (3 de 5 cambian de forma
      afirmativa a interrogativa). Corregido en la ronda 1 (`f2a714c`):
      `commit-4-report.md:65-76` declara la adaptación y agrega una línea en
      `PROTOCOLO-DE-TRABAJO.md` fijando `DOCUMENTACION.md` sec. 3 como fuente canónica.
- [x] **T4.8** `AGENTS.md` conserva su resumen de cinco líneas y **enlaza** aquí.
      **Terminado:** `AGENTS.md` no repite los nueve pasos ni la tabla de pruebas (D1).
      `commit-4-report.md:91-92`: confirmado con lectura completa del diff, solo 6 líneas
      agregadas. Verificado ahora: `AGENTS.md` no contiene la tabla de exigencia de
      pruebas ni los nueve pasos, solo el enlace.

---

## Commit 5 — `fase-3/`

- [x] **T5.1** `spec.md`: encabezado declarando procedencia (`PLAN-FASE-3-4.md` secs. 2.2
      y 3) y estado de la fase. Confirmado: `docs/planes/fase-3/spec.md` abre con
      "**Documento archivado**" y cita su procedencia.
- [x] **T5.2** Absorber sec. 2.2 — los 10 escenarios obligatorios de sincronización —
      **conservando el número `2.2`** (D6). Verificado ahora:
      `grep -nE '^#+ .*2\.2' docs/planes/fase-3/spec.md` → línea 66.
- [x] **T5.3** Absorber sec. 3 — bloques 3.A–3.C — **conservando los números `3.x`** (D6).
      Verificado ahora: bloques 3.A (línea 108), 3.B (196), 3.C (270) presentes.
- [x] **T5.4** Incorporar el estado y la retrospectiva de `ROADMAP.md:33-56` (el cierre
      revertido y los tres defectos que lo motivaron) y `ROADMAP.md:109-142` (objetivo,
      criterio de salida y pendiente para cerrar).
      Corregido en la ronda 1 (`a596cad`): el puntero original citaba solo 109-142 para
      ambos contenidos (error del brief del coordinador, `commit-5-report.md:213-214`
      y `commit-5b-report.md`); ahora cita los dos rangos correctos, verificados leyendo
      `docs/ROADMAP.md:33-56` y `:109-142` directamente.
- [x] **T5.5** `plan.md`: la secuencia de ~11 ramas, marcada como ejecutada.
      `docs/planes/fase-3/plan.md` existe con la secuencia y su estado de ejecución.
- [x] **T5.6** `tasks.md`: todo `[x]` salvo el piloto real, que queda `[ ]` con la cita de
      `ROADMAP.md:121-125`. Verificado ahora: `docs/planes/fase-3/tasks.md` tiene 23
      tareas `[x]` y una sola `[ ]` (**T3C.2**, el piloto real), con la cita literal de
      `ROADMAP.md:121-125` en el texto de la tarea.
- [x] **T5.7** `test-e2e.md`: los 10 escenarios de sync como guion ejecutable.
      `commit-5-report.md:16`: `SYNC-1`…`SYNC-11` (10 escenarios + prueba de
      convergencia), `docs/planes/fase-3/test-e2e.md` existe.
- [x] **T5.8** Marcar la carpeta como **archivada** en el encabezado, según la regla T3.4.
      Verificado ahora: encabezado de `fase-3/spec.md` dice "**Documento archivado**"
      citando la regla de `DOCUMENTACION.md` sec. 4.
- [x] **T5.9** Verificar que las secciones `2.2` y `3.x` existen con esos números exactos.
      **Terminado:** `grep -nE '^#+ .*(2\.2|3\.[ABC])' docs/planes/fase-3/spec.md` las
      encuentra. Ejecutado ahora mismo: devuelve las cuatro líneas esperadas.
      Desviación declarada en `commit-5-report.md:38-51`: la numeración de **nivel 1**
      del documento (1–7) no hereda la del `PLAN-FASE-3-4.md` original — solo las
      subsecciones `2.2`/`3.A/B/C`, que es lo que D6 exige preservar.

---

## Commit 6 — `fase-3-5/`

- [x] **T6.1** Repartir las 833 líneas por naturaleza: decisiones y hallazgos → `spec.md`;
      secuencia → `plan.md`; checklist → `tasks.md`; verificación → `test-e2e.md`.
      Hecho con una desviación real que el propio revisor marcó como incumplimiento
      inicial (`spec ❌`) y que se corrigió en la ronda 1 (`e893425`): la primera versión
      del reparto atribuía a `InventoryItem.cs` una frase ("Deprecado cuando llegue
      Purchasing") que en realidad vive en ROADMAP.md/ADR-0026, y esa cita falsa se había
      propagado a tres archivos, incluida `TEMPLATE-spec.md`. Corregido y verificado
      abriendo `InventoryItem.cs:188-189` (`commit-6-report.md:10-45`).
- [x] **T6.2** **Conservar toda la numeración** `3.5a.0`–`3.5a.9` y `3.5b.x` (D6).
      **Terminado:** existe un listado de las secciones citadas desde el código y cada una
      aparece con el mismo número en el destino.
      `.superpowers/sdd/plan/secciones-citadas-3-5.txt` es el listado. Verificado ahora
      (V-4 de `test-e2e.md`): las 7 formas de sección citadas desde el código (`2.3`,
      `3.5a.{3,4,5,7,8}`, `7`) resuelven en `spec.md`/`spec-3.5a.md` con el número exacto.
- [x] **T6.3** Sección **"Adelantos de Fase 4 que viven aquí"** con las cinco piezas del
      spec 2.9, cada una con archivo, línea y contrato de caducidad literal (D7).
      Existe en `docs/planes/fase-3-5/spec.md` sec. 2.9. Corregida en la ronda 1 tras el
      hallazgo Crítico de T6.1 (fila 1 de la tabla citaba mal el código).
- [x] **T6.4** Incorporar el estado de `ROADMAP.md:146-217`, incluidos ADR-0024, ADR-0021 y
      la recepción de inventario del PR #94. `commit-6-report.md:59`: trasladado a
      `plan.md` sec. 3 "Estado real contra develop", verbatim.
- [x] **T6.5** **Enlazar** los ocho `sub_planes/`; no absorberlos (fuera de alcance).
      Verificado ahora: `docs/planes/fase-3-5/plan.md:131-138` enlaza los ocho, y los
      ocho archivos existen sin diffs contra su versión original (confirmado en
      `commit-6-report.md:64`).
- [x] **T6.6** `tasks.md` con todas las tareas de 3.5a y 3.5b, **todas en `[ ]`**. Las
      marca el agente en la compuerta A, no este commit. `commit-6-report.md:140`: ~50
      tareas, ninguna marcada al momento del commit `746e557`. (Las marcó después la
      Compuerta A, ver T6.6 vs. estado actual en Compuerta A / T7 abajo.)
- [x] **T6.7** `test-e2e.md` con el criterio de salida de 3.5a y 3.5b como guion.
      `commit-6-report.md:63`: escenarios E2E-1/2/3, `docs/planes/fase-3-5/test-e2e.md`
      existe.
- [x] **T6.8** Verificar que ningún archivo de la carpeta pasa de ~400 líneas. Si `spec.md`
      se pasa, partirlo por bloque `3.5a` / `3.5b`.
      Desviación declarada y aceptada (Ruling 12 del ledger): `spec.md` (833 líneas
      originales) se partió en `spec.md` (413 líneas) + `spec-3.5a.md` (302 líneas) —
      ambos ligeramente sobre o cerca de 400, `commit-6-report.md:76-97` documenta el
      motivo (recortar más exigía sacrificar contenido real). `plan.md` 138,
      `tasks.md` 174, `test-e2e.md` 205 líneas — todos bajo el límite.

---

## Compuerta A — Agente auditor

- [x] **TA.1** Lanzar el agente de contexto limpio, solo lectura, con la regla D13
      explícita en el prompt: `[x]` **solo** contra archivo y línea.
      `.superpowers/sdd/plan/progress.md:281-293`: "COMPUERTA A — auditoría de la Fase
      3.5: SUPERADA", "Repositorio intacto (solo lectura)".
- [x] **TA.2** El agente entrega `tasks.md` marcado. `.superpowers/sdd/plan/
      auditoria-3-5-tasks.md` es la copia marcada que entregó el auditor; aplicada a
      `docs/planes/fase-3-5/tasks.md` en el commit 7 (`0f7aab9`).
- [x] **TA.3** El agente entrega la lista de discrepancias documento-vs-código.
      `.superpowers/sdd/plan/auditoria-3-5-hallazgos.md` (137 líneas) + resumen de 3
      discrepancias en `progress.md:286-293` (D-1 unificación de `EventType`, D-2 bloque
      4.2-4.7 sin código, D-3 ficha del lote incompleta).
- [x] **TA.4** **Compuerta:** si la lista de discrepancias viene **vacía**, no aceptarla.
      No vino vacía: 3 discrepancias reales, con archivo:línea cada una. Compuerta
      superada según su propio criterio.
- [x] **TA.5** Verificar por muestreo **tres** marcas `[x]` del agente abriendo el archivo y
      la línea que citó. **Terminado:** las tres se sostienen.
      `progress.md:283-285` cita las tres: `TrackingMode.cs:14`,
      `LiveHeadCountCalculator.cs:10`, `SyncPullQueries.cs:32`. Re-verificadas ahora mismo
      contra `docs/planes/fase-3-5/tasks.md` (líneas 42, 57, 74): las tres citas de
      evidencia existen y apuntan a los archivos reales.

---

## Commit 7 — Marcas verificadas

- [x] **T7.1** Aplicar las marcas del agente a `docs/planes/fase-3-5/tasks.md`.
      Commit `0f7aab9`. Verificado ahora mismo: 61 `[x]` / 23 `[ ]` (84 tareas), igual al
      resultado de la auditoría.
- [x] **T7.2** Cada `[x]` cita archivo y línea. **Terminado:** no queda ningún `[x]` sin
      evidencia. `commit-7-report.md:5-6` y verificación del revisor de tarea
      (`progress.md:302-304`): "comparó las 84 marcas programáticamente... coinciden
      exactamente", "verificó 8 evidencias contra el código real". Confirmado por muestreo
      propio en TA.5.
- [x] **T7.3** Las discrepancias que no se resuelven acá se anotan en `docs/BACKLOG.md`.
      D-2 (bloque 4.2-4.7 sin código) y D-3 (ficha del lote, `lastVaccinationAt`) están en
      `docs/BACKLOG.md` (verificado con grep: menciones de `lastVaccinationAt` y "Health
      Plans" en la sección de deuda de 3.5b). D-1 no fue al BACKLOG porque tenía
      resolución directa: se reflejó como nota en `spec-3.5a.md` (ver T7.4).
- [x] **T7.4** Las discrepancias que contradigan el `ROADMAP.md` se reportan al dueño antes
      de continuar. **No se corrige el ROADMAP en esta rama** (regla 9).
      Ninguna de las 3 discrepancias contradice `ROADMAP.md` — verificado explícitamente
      por el auditor (`commit-7-report.md:8-12`: "ninguna de las tres discrepancias lo
      contradice... el vacío entre plan y código es honesto"). `ROADMAP.md` no se tocó.

---

## Commit 8 — `fase-4/` y `fase-5/`

- [x] **T8.1** `fase-4/spec.md` parte 1: objetivo y criterio de salida de
      `ROADMAP.md:220-231`. `docs/planes/fase-4/spec.md` existe, 150 líneas
      (`commit-8-report.md:5`), sec. 1 con objetivo/criterio.
- [x] **T8.2** Parte 2: deuda heredada, enlazando la sección de adelantos de
      `fase-3-5/spec.md`, con las cinco piezas y sus contratos de caducidad.
      `commit-8-report.md:33-37`: paráfrasis fiel (declarada explícitamente, no copia
      literal), la tabla original se enlaza, no se copia.
- [x] **T8.3** Parte 3: preguntas abiertas — proveedor autorizado del SRI, plan de cuentas,
      qué acepta el contador. Presente en `docs/planes/fase-4/spec.md`.
- [x] **T8.4** Parte 4: apéndice **"Planificación previa (2026-08-02): insumo, no
      compromiso"** con los bloques 4.A–4.D (D8). `commit-8-report.md:9`: apéndice
      rotulado así, confirmado por el revisor de tarea. Desviación menor declarada: el
      apéndice tiene **12** ramas reales, no las "~11" que decía el brief del coordinador
      (`commit-8-report.md:19-23`; error del brief, no del implementador — corregido y
      verificadas las 12 en la revisión, `progress.md:318-321`).
- [x] **T8.5** Verificar que las exigencias de pruebas de dinero **no** están acá: ya viven
      en `PROTOCOLO-DE-TRABAJO.md` desde T4.5. `commit-8-report.md:39-40`: no se
      repitieron, se enlaza a `PROTOCOLO-DE-TRABAJO.md` sec. 2.3–2.5.
- [x] **T8.6** `fase-5/spec.md`: objetivo, criterio de salida (`ROADMAP.md:235-243`) y
      preguntas abiertas — ARCSA, el caso del queso fresco, alcance de Grazing.
      `docs/planes/fase-5/spec.md` existe, 54 líneas (`commit-8-report.md:5`).
- [x] **T8.7** **Verificar que no se creó `plan.md`, `tasks.md` ni `test-e2e.md`** en
      ninguna de las dos carpetas (D5).
      **Terminado:** `ls docs/planes/fase-4 docs/planes/fase-5` muestra solo `spec.md`.
      Ejecutado ahora mismo: confirmado, solo `spec.md` en cada carpeta.

---

## Commit 9 — `SEGURIDAD.md`

- [x] **T9.1** Auditar el modelo de autenticación: emisión, vigencia, refresco y cierre de
      sesión, contra `people.refresh_tokens` y los endpoints reales.
      `docs/SEGURIDAD.md` cubre login/refresh/logout con la cadena JWT verificada de
      forma independiente por el coordinador (`progress.md:326-333`: JWT de 480 min,
      refresh de 30 días con rotación, hueco real del `docker-compose.yml` forzando
      `Development`).
- [x] **T9.2** Auditar el modelo de autorización: permisos en BD (ADR-0007) y roles.
      Presente en `docs/SEGURIDAD.md`. Confirmado por el coordinador (`progress.md:345-347`,
      Ruling 13): `BreedingEndpoints.cs` y `MilkingEndpoints.cs` con 0
      `RequirePermission` (24 usos de `RequireAuthorization()` sin permiso en total),
      hallazgo central verificado independientemente.
- [x] **T9.3** Generar la tabla **endpoint → permiso exigido** leyendo los 19 archivos de
      `src/Hato.Api/Endpoints/`.
      **Terminado:** todo archivo de `Endpoints/*.cs` aparece en la tabla.
      `commit-9-report.md:10-14`: 19/19 confirmados. La revisión final de rama (opus,
      `progress.md:424-426`) volvió a muestrear los 19 archivos **completos** (no solo 8):
      "111 endpoints + health = 112 filas. 0 permisos mal atribuidos, 0 omitidos, 0
      secretos."
- [x] **T9.4** Documentar la autorización del pull de sincronización
      (`RequiredPermissionByCollection`), que es donde un error entrega datos a quien no
      debe. Verificado ahora: `grep -c "RequiredPermissionByCollection" docs/SEGURIDAD.md`
      → 2.
- [x] **T9.5** Documentar el manejo de secretos, incluido el precedente de GitGuardian de
      `ROADMAP.md:11`. `commit-9-report.md:46-47`: verificado que `.env` local solo trae
      `POSTGRES_PASSWORD` (redactada, no transcrita).
- [x] **T9.6** Documentar la superficie expuesta: CORS (`Program.cs:12-42`) y los puertos
      publicados por `docker-compose.yml`. Verificado ahora: `docs/SEGURIDAD.md:466-478`
      documenta CORS citando `Program.cs:12-42` y la política `AdminWebCors`.
- [x] **T9.7** Sección de huecos conocidos, con fecha y su entrada en `docs/BACKLOG.md`.
      `commit-9-report.md:16-39`: 8 huecos con fecha (2026-08-16), 7 entradas nuevas en
      `docs/BACKLOG.md` (el octavo reutiliza la entrada BJ-04 ya existente).
- [x] **T9.8** **Si algo resulta explotable: detenerse y avisar al dueño.** No se arregla en
      esta rama, pero tampoco se publica sin avisar.
      `commit-9-report.md:41-48`: "Ninguno. Los ocho huecos... están todos dentro del
      límite de 'usuario autenticado'... nada que avisar al dueño." La sospecha inicial de
      una clave commiteada resultó infundada, verificado explícitamente
      (`progress.md:334-335`).

---

## Commit 10 — Diagramas

- [x] **T10.1** Auditar `der-1-identidad.mermaid` contra el esquema real.
      `commit-10-report.md:17-28`: contra `*ModelSnapshot.cs`, columnas inexistentes
      quitadas (`SPECIES.code`, `ANIMALS.origin/status/...`), reales agregadas
      (`SPECIES.is_milkable`, `ANIMALS.birth_weight_kg`...).
- [x] **T10.2** Auditar `der-2-eventos.mermaid`. `commit-10-report.md:30-50`: `EVENT_TYPES`
      retirada (no planificada, D11), `ATTACHMENTS` marcada `FUTURO — Fase 4`. Hallazgo
      colateral anotado: el CHECK XOR animal/group que `DATA-MODEL.md` decía "no
      implementado" sí está implementado (`CK_AnimalEvent_AnimalXorGroup`) — no corregido
      ahí (fuera de alcance), sí anotado en el informe y en `docs/BACKLOG.md`
      (commit 10b).
- [x] **T10.3** Auditar `der-3-grupos-inventario.mermaid`: quitar o marcar `PADDOCKS`,
      `GRAZING_MOVEMENTS` y `UNITS`; **agregar `FEED_STAGES`**.
      Verificado ahora: `PADDOCKS`/`GRAZING_MOVEMENTS` marcadas `%% FUTURO — Fase 5`
      (líneas 4-5), `UNITS` retirada como tabla (queda solo de comentario explicativo),
      `FEED_STAGES` presente (líneas 7, 29, 76).
- [x] **T10.4** Auditar `der-4-reproduccion.mermaid`. `commit-10-report.md:74-91`:
      columnas corregidas, `NURSING_COHORTS` agregada. Hallazgo que no se forzó a encajar:
      `LACTATIONS` existe como tabla pero ningún comando la invoca (`grep` sobre `src/`
      sin resultados) — documentado como nota explícita, no como "existe" ni "FUTURO".
- [x] **T10.5** Auditar `der-5-produccion-leche.mermaid`. `commit-10-report.md:93-109`: el
      más reescrito — el diseño que `DATA-MODEL.md` documentaba (por grupo/tanque,
      `lactation_id` autoresuelto, `MILK_QUALITY_TESTS`) nunca se construyó; retirado por
      D11 (sin fase que lo comprometa, se borra en vez de marcarse FUTURO).
- [x] **T10.6** Encabezado en los cinco con fecha de verificación y migración de
      referencia. Verificado ahora: `grep -L "Verificado el" docs/diagramas/*.mermaid` →
      vacío, los cinco `der-*` lo tienen (fecha 2026-08-16).
- [x] **T10.7** Marcar todo lo aspiracional con `%% FUTURO — Fase N` (D11).
      **Terminado:** no queda ninguna entidad sin correspondencia en la base que no esté
      marcada. Revisión de tarea confirmó T10.1-T10.9 y T10.11 ✅
      (`progress.md:365-367`): "las 4 marcas FUTURO respaldadas por ROADMAP y specs de
      fase".
- [x] **T10.8** `flujo-sincronizacion.mermaid`: push/pull con idempotencia y cursor.
      `commit-10-report.md:116-123`, contra `src/Hato.Api/Sync/*.cs` y
      `syncEngine/outbox/syncApi.ts` reales.
- [x] **T10.9** `flujo-autenticacion.mermaid`: login, refresco, expiración.
      `commit-10-report.md:124-131`, contra `PeopleEndpoints.cs`/`JwtTokenGenerator.cs`
      reales.
- [x] **T10.10** `flujo-consumo-alimento.mermaid`: conversión de unidad y descuento FIFO.
      Falló parcialmente en la primera revisión (`progress.md:365,369-384`): el diagrama
      afirmaba que `batch_id` queda en "el primer lote que perdió stock" (copiando el
      comentario XML de la línea 180 de `RecordGroupFeedConsumptionCommand.cs`), pero el
      código real resuelve `ResolveConsumptionBatchId` **después** de la deducción FIFO,
      sobre cantidades ya mutadas — es un **bug real de producción**, no solo un error de
      diagrama. Corregido en `c1361f5` (fix round 1) con un nodo de decisión de 3 salidas
      y entrada nueva en `docs/BACKLOG.md`; no se tocó el código `.cs` (regla 9).
      Re-revisión confirmó que el diagrama corregido no exagera el bug.
- [x] **T10.11** Los tres flujos renderizan sin error de sintaxis Mermaid.
      `commit-10-report.md:9-11`: verificado con `@mermaid-js/mermaid-cli` real (`mmdc`)
      contra `chrome-headless-shell`, no a ojo. Verificado ahora: `ls
      docs/diagramas/flujo-*.mermaid | wc -l` → 3.

---

## Commit 11 — Reapuntado y borrado · PUNTO DE NO RETORNO

- [x] **T11.1** Reapuntar **a mano** las 2 citas a `PLAN-FASE-3-4 sec.2.1` →
      `docs/PROTOCOLO-DE-TRABAJO.md`. **Antes** de cualquier `sed`.
      **Terminado:** `grep -rn "PLAN-FASE-3-4 sec\.2\.1"` en código devuelve cero.
      Ejecutado ahora mismo: devuelve cero.
- [x] **T11.2** `sed` de las 85 citas de `PLAN-FASE-3-5-PORCINO` en código, **en dos pasos y
      en este orden**: primero las ~40 citas a `sec.3.5a.*` → `spec-3.5a.md`, después el
      resto (`sec.2.3`, `sec.7`, menciones sin sección) → `spec.md`. Invertir el orden manda
      todo a `spec.md` y rompe 40 referencias en silencio.
      El commit 6 partió el spec en dos para no recrear el archivo inmanejable que el
      `spec.md` sec. 13 advertía; los comandos exactos están en `plan.md` commit 11.
      **Terminado:** `grep -rn "spec-3.5a.md sec\.3\.5a"` en código devuelve ~40, y ninguna
      cita a `3.5a` quedó apuntando a `spec.md`.
      Ejecutado ahora mismo: 67 coincidencias, todas a `spec-3.5a.md` (número real más
      alto que el "~40" estimado por el brief; ninguna a `spec.md`, ver V-2 en
      `commit-11-report.md:48-59`). Desviación real detectada y corregida en la ronda 1
      (`987d325`): un bug de orden del `sed` mutiló 24 citas con guion pegado (formas
      `PLAN-FASE-3-5-PORCINO-3.5a.2-{A,B,C}`, que son **nombres de archivo** de
      `sub_planes/`, no secciones) a `spec.md-3.5a.2-A`; y la primera corrección las mandó
      al destino incorrecto (`spec-3.5a.md sec.3.5a.2-C`, que no existe) antes de
      reapuntarlas bien a `docs/planes/sub_planes/` (`commit-11-report.md:8-38`).
- [x] **T11.3** `sed` de las 21 citas restantes de `PLAN-FASE-3-4` en código.
      Ejecutado ahora mismo: `grep -rn "PLAN-FASE-3-4"` en código devuelve cero.
- [x] **T11.4** Reapuntar las 130 citas en `.md`, respetando las excepciones del criterio 7
      del spec. Alcance real mayor al estimado por el brief (26 archivos `.md` sobreviven
      con menciones deliberadas hoy, no 30 "corregidos"; ver `commit-11-report.md:111-125`,
      desviación 4: el criterio 7 excluye `sub_planes/` por completo, y el alcance de
      T11.4b resultó más amplio — 8 archivos adicionales con menciones bare de
      `BACKLOG.md`). Verificado ahora (V-5 de `test-e2e.md`): 26 archivos `.md` mencionan
      los nombres viejos, todos dentro de las 4 categorías declaradas (carpeta de este
      plan, encabezados de procedencia de fase-3/fase-3-5, los 6 ADR que no se editan,
      `PROTOCOLO-DE-TRABAJO.md` y los 8 `sub_planes/`).
- [x] **T11.4b** Reapuntar las 9 referencias a `BACKLOG.md` de la raíz que quedaron del
      commit 1: `docs/ROADMAP.md` (1), `docs/planes/PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` (4) y
      `docs/planes/sub_planes/` (4) → `docs/BACKLOG.md`.
      **No tocar `docs/adr/`** (7 referencias): un ADR no se edita, se reemplaza.
      **Terminado:** el comando de `test-e2e.md` V-2, con sus dos exclusiones, devuelve cero.
      Ejecutado ahora mismo con las exclusiones vigentes de `test-e2e.md` V-2 (tres, tras
      la corrección más reciente que agrega `docs/PROTOCOLO-DE-TRABAJO.md:136`): devuelve
      cero líneas. El implementador corrigió además 8 archivos `.md` adicionales con
      menciones bare de `BACKLOG.md` no listados en el enunciado original de esta tarea
      (`commit-11-report.md:111-125`), y dos enlaces relativos rotos a la raíz
      (`commit-11-report.md:127-130`).
- [x] **T11.5** `git diff --stat` revisado archivo por archivo: **ningún cambio fuera de
      comentarios**. Un `sed` que tocó código ejecutable se revierte entero.
      `commit-11-report.md:61-75`: 87 archivos, 108 inserciones/108 eliminaciones, todas
      dentro de comentarios (`//`, `///`, `/**`, `*`), verificado línea por línea.
- [x] **T11.6** `git rm docs/planes/PLAN-FASE-3-5-PORCINO.md docs/planes/PLAN-FASE-3-4.md`.
      Verificado ahora mismo: `ls docs/planes/PLAN-FASE-3-5-PORCINO.md
      docs/planes/PLAN-FASE-3-4.md` → "No such file or directory" para ambos.
- [x] **T11.7** `AGENTS.md`: la advertencia sobre citas en prosa se actualiza con los
      nombres y el conteo reales. Verificado ahora: `AGENTS.md:106-109` cita conteos
      reales medidos el 2026-08-16 (`spec-3.5a.md` en 59 archivos, `fase-3/spec.md` en 18,
      `fase-3-5/spec.md` en 15, `PROTOCOLO-DE-TRABAJO.md` en 2).

---

## Cierre

- [x] **TC.0** Marcar las casillas de **este mismo archivo** en una sola pasada, contra el
      historial de la rama. Se hace acá y no commit a commit a propósito: marcar sobre la
      marcha metería una edición de `tasks.md` en cada diff y ensuciaría la revisión de
      cada commit. El seguimiento durante la ejecución vive en el ledger.
      Esta pasada, hecha ahora (2026-08-17), es la ejecución de TC.0: cada `[x]` de este
      archivo cita su evidencia (informe del ledger con línea, o comando corrido en este
      mismo momento contra el árbol de trabajo real), nunca la palabra de un documento.
      Nota de proceso: la propia revisión final de rama (`progress.md:427-429`) señaló que
      diferir el marcado sin una compuerta que lo verificara fue un olvido — "un
      diferimiento sin compuerta es un olvido programado" — y que TC.0–TC.4 nunca habían
      corrido antes de esta pasada.
- [x] **TC.0b** Anotar en `docs/BACKLOG.md` la deuda preexistente detectada en el commit 4:
      el texto heredado cita `AGENTS.md sec.5` y `sec.2`, pero `AGENTS.md` dejó de numerar
      sus encabezados. Las citas no resuelven. No se arregla en esta rama (regla 9: es otro
      propósito), pero deja de ser invisible.
      Ya está anotada en `docs/BACKLOG.md` (cambio sin commitear, verificado con `git diff
      -- docs/BACKLOG.md`): entrada nueva que cita `docs/PROTOCOLO-DE-TRABAJO.md:78` y
      `:103`, confirma con `grep -n "^#" AGENTS.md` que ninguna sección numerada resuelve,
      y da el disparador ("cualquier edición... debería resolver la cita").
- [x] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo, los ocho escenarios.
      Los ocho escenarios se corrieron ahora mismo, comando por comando, contra el árbol
      de trabajo real (no solo leídos): V-1 (`git worktree list`→1, worktrees/→0), V-2
      (un solo `BACKLOG.md`, conteo de líneas de lista/H3 conservado, grep de referencias
      con las tres exclusiones vigentes→0), V-3 (6 plantillas, ninguna bajo 20 líneas
      salvo la excepción declarada `TEMPLATE-adr.md`, `docs/adr/TEMPLATE.md` no existe),
      V-4 (las 7 formas de sección citadas desde el código resuelven en su destino exacto),
      V-5 (cero en código con la exclusión `sub_planes/`; 26 archivos `.md` sobreviven,
      todos dentro de las 4 categorías declaradas; los dos planes viejos no existen),
      V-6 (cero huérfanos de la taxonomía; `fase-4`/`fase-5` solo `spec.md`; `fase-3`
      cuatro archivos, `fase-3-5` cinco), V-7 (los 5 DER llevan "Verificado el"; `PADDOCKS`
      /`GRAZING_MOVEMENTS`/`UNITS` marcadas o retiradas, `FEED_STAGES` presente; 3 flujos),
      V-8 (19/19 endpoints documentados, `RequiredPermissionByCollection` presente).
      Los ocho dieron verde. `test-e2e.md` mismo estaba, al momento de esta verificación,
      con una redacción más completa que la que documentaba el ledger original (otro
      agente corrigió en paralelo V-2/V-3/V-5/V-6 para declarar exclusiones que ya eran
      necesarias en la práctica — `docs/PROTOCOLO-DE-TRABAJO.md:136`, la excepción de
      `TEMPLATE-adr.md`, las 4 categorías completas de V-5, y `spec-3.5a.md` en V-6); la
      verificación de arriba se hizo contra el estado del árbol, no contra la redacción,
      así que sostiene independientemente de en qué momento terminó de escribirse el
      documento.
- [x] **TC.2** Los 14 criterios de aceptación del `spec.md` sec. 14, uno por uno.
      1. ✅ `git worktree list` → 1 línea.
      2. ✅ Único `docs/BACKLOG.md`; conteo de contenido conservado (T1.3).
      3. ✅ 6 plantillas con instrucciones y ejemplo, salvo `TEMPLATE-adr.md` (excepción
         declarada por conflicto con T2.1, D10).
      4. ✅ Existen `DOCUMENTACION.md`, `PROTOCOLO-DE-TRABAJO.md`, `SEGURIDAD.md`.
      5. ✅ `fase-3/` (4 archivos), `fase-3-5/` (5, con `spec-3.5a.md`), `fase-4/` y
         `fase-5/` (`spec.md` cada una) — el propio criterio 5 ya documenta el split.
      6. ✅ Los dos planes viejos no existen.
      7. ⚠️ **Cumplido en sustancia, con la redacción del criterio desactualizada.** El
         `grep` en código da cero (con la exclusión `sub_planes/`, ya escrita en el
         criterio). Pero el texto del criterio, en `.md`, solo declara dos categorías de
         excepción ("esta carpeta" + "encabezados de procedencia de fase-3/fase-3-5");
         en la práctica sobreviven **26** archivos `.md`, en **4** categorías (esas dos,
         más los 6 ADR inmutables y `docs/PROTOCOLO-DE-TRABAJO.md`/`sub_planes/`) — el
         mismo conjunto que `test-e2e.md` V-5 ya enumera completo tras su corrección más
         reciente. La sustancia del criterio (nada "olvidado") se sostiene; su redacción
         no se actualizó al mismo nivel de detalle que V-5. No se corrige acá: no puedo
         editar `spec.md` en esta tarea.
      8. ✅ Toda sección citada existe en su destino con el mismo número (V-4).
      9. ✅ Los 5 DER auditados, aspiracional marcado.
      10. ✅ Los 3 flujos nuevos existen.
      11. ✅ Tabla endpoint→permiso, 19/19 archivos.
      12. ✅ `fase-3-5/tasks.md` auditado (61/23 marcadas con evidencia); discrepancias
          documentadas (D-1 en `spec-3.5a.md`, D-2/D-3 en `docs/BACKLOG.md`) — "resueltas"
          en el sentido de que ninguna quedó invisible, no en el sentido de que se
          corrigió código (regla 9 lo prohíbe).
      13. ✅ `dotnet test` y `npm test` en verde (ver TC.3/TC.4).
      14. ✅ `AGENTS.md` sin referencias a archivos borrados (verificado con grep dirigido
          a `PLAN-FASE-3-4`, `PLAN-FASE-3-5-PORCINO`, `docs/adr/TEMPLATE.md`, `BACKLOG.md`
          bare — todas resuelven a rutas vivas).
- [x] **TC.3** `dotnet test` en verde. Esta rama no toca código ejecutable: cualquier fallo
      es una regresión ajena que hay que detectar antes del merge.
      Corrido ahora mismo: `Failed: 0` en las 10 suites de test (unitarias e
      integración), 0 fallos.
- [x] **TC.4** `npm test` en verde en `clients/field-app` y `clients/admin-web`.
      Corrido ahora mismo: `clients/field-app` → 36 suites, 220 pasan, 6 skip, 0 fallos.
      `clients/admin-web` → 17 archivos, 102 pasan, 0 fallos.
- [ ] **TC.5** Abrir el PR con la descripción de [`plan.md`](./plan.md) sec. 15.
      No ejecutada: instrucción explícita de esta pasada de cierre fue no abrir ningún PR.
