# test-e2e.md — Verificación documental extremo a extremo

> **Esto no es una prueba de aplicación.** Esta rama no toca código ejecutable, así que lo
> que hay que verificar no es que algo corra, sino que **la documentación sea cierta**: que
> todo enlace resuelva, que toda cita desde el código apunte a algo que existe, y que
> ningún documento haya quedado huérfano.
>
> Los ocho escenarios se ejecutan **después** del commit 11 y **antes** de abrir el PR. Un
> escenario que falla se reporta con el comando exacto y su salida, no como "no anda".
>
> Cada escenario es un comando con salida esperada. Si un comando exige juicio humano, lo
> dice explícitamente.

**Antes de empezar:**

- Estar en la rama `docs/reestructura-documentacion`, con el commit 11 hecho.
- Árbol de trabajo limpio: `git status --porcelain` vacío.
- Ejecutar todo desde la raíz del repositorio.

**Exclusiones estándar.** Todos los `grep` de este documento excluyen `node_modules` y
`.claude/`. Si la compuerta 0 no se cerró, los resultados no valen nada.

---

## V-1 — No sobrevive ningún worktree de agente

**Por qué:** es la compuerta 0 y la precondición de todos los demás escenarios. Un worktree
vivo multiplica por siete cada resultado y vuelve inútil cualquier verificación por `grep`.

```bash
git worktree list | wc -l
```

**Esperado:** `1`.

```bash
ls .claude/worktrees/ 2>/dev/null | wc -l
```

**Esperado:** `0`, o que el directorio no exista.

---

## V-2 — Hay un solo BACKLOG y no perdió contenido

**Por qué:** es el duplicado que originó todo este trabajo. Fusionar mal y perder deuda
anotada sería peor que haber dejado los dos.

```bash
ls BACKLOG.md 2>/dev/null; echo "---"; ls docs/BACKLOG.md
```

**Esperado:** el primero no existe; el segundo sí.

```bash
grep -c '^- \|^### ' docs/BACKLOG.md
```

**Esperado:** un número **mayor o igual** al que se midió en la tarea T1.1 sumando los dos
archivos originales. Si es menor, se perdió contenido: comparar contra
`git show develop:BACKLOG.md` y `git show develop:docs/BACKLOG.md`.

```bash
grep -rn "BACKLOG\.md" --include=*.md . | grep -v node_modules | grep -v '\.claude/' \
  | grep -v 'docs/BACKLOG\.md' | grep -v 'docs/adr/' \
  | grep -v 'docs/spec/feature-0003-reestructura-documentacion/' \
  | grep -v 'docs/PROTOCOLO-DE-TRABAJO\.md'
```

**Esperado:** cero líneas.

**Tres exclusiones, y por qué.** La versión original de este escenario exigía cero
coincidencias sin excepciones, y era **imposible de cumplir** (hallada durante la ejecución
del commit 1):

- **`docs/adr/`** — siete referencias viven en los ADR 0020, 0024, 0025 y 0026. Un ADR
  **no se edita jamás**: se reemplaza. Es la regla de la taxonomía del `spec.md` sec. 5 y
  la convención del repositorio. Un ADR que menciona `BACKLOG.md` está describiendo el
  mundo tal como era el día que se aceptó, y así debe quedar.
- **`docs/spec/feature-0003-reestructura-documentacion/`** — esta carpeta documenta la fusión; sus
  menciones son deliberadas, igual que la excepción del criterio 7 del spec.
- **`docs/PROTOCOLO-DE-TRABAJO.md`** — su única mención (línea 136) es la fila "Deuda
  técnica arrastrada" de una tabla de riesgos, que escribe `` A `BACKLOG.md` `` sin ruta
  como abreviatura dentro de una celda angosta (el resto del documento sí usa
  `docs/BACKLOG.md` con ruta completa, ya cubierto por la exclusión anterior). Se excluye
  por archivo, igual que `docs/adr/`, en vez de filtrar por el texto de la fila: una
  exclusión por prosa es frágil (cualquier reescritura de esa celda la desactiva, y podría
  además silenciar una ruta rota real que cayera en la misma línea por coincidencia), y
  este documento no tiene más menciones de `BACKLOG.md` que revisar — es nuevo, de esta
  misma rama. Probado: inyectar una ruta rota real (`[BACKLOG.md](../BACKLOG.md)`) en
  cualquier otro archivo de la raíz sigue disparando el escenario; esta exclusión no la
  habría silenciado.

Las referencias de `PLAN-FASE-3-5-PORCINO.md` y `PLAN-FASE-3-4.md` no necesitan exclusión:
esos archivos dejan de existir en el commit 11.

---

## V-3 — Las plantillas están completas y son usables

**Por qué:** una plantilla vacía o sin ejemplo se llena mal, y este trabajo existe
precisamente para que el próximo documento nazca bien.

```bash
ls docs/plantillas/*.md | wc -l
```

**Esperado:** `6`.

```bash
for f in docs/plantillas/*.md; do
  echo "$f: $(wc -l < "$f") líneas"
done
```

**Esperado:** ninguna por debajo de 20 líneas, **excepto `TEMPLATE-adr.md`** (18 líneas).
Una plantilla de 10 líneas es un título con viñetas, no una plantilla — pero
`TEMPLATE-adr.md` es la excepción deliberada que detalla el siguiente párrafo, no un
descuido.

```bash
ls docs/adr/TEMPLATE.md 2>/dev/null
grep -rn "docs/adr/TEMPLATE\.md" --include=*.md . | grep -v node_modules | grep -v '\.claude/' \
  | grep -v 'docs/spec/feature-0003-reestructura-documentacion/'
```

**Esperado:** el archivo ya no existe y ninguna referencia lo menciona.

**Juicio humano:** abrir las seis y confirmar que cada una tiene instrucciones embebidas
**y** un ejemplo corto real de este repositorio (T2.8) — **con la excepción de
`TEMPLATE-adr.md`**, que solo necesita las instrucciones. T2.1 (D10 del `spec.md`) exige
mover `docs/adr/TEMPLATE.md` **sin cambios de contenido**; T2.8 exigía ejemplo embebido en
las seis. Las dos no pueden cumplirse a la vez sobre el mismo archivo, y el commit
`2155b75` resolvió el conflicto a favor de T2.1: la plantilla de ADR no necesita ejemplo
propio porque tiene 26 ADR reales al lado, en `docs/adr/`, mejor ejemplo del que cabría en
una cita de bloque. Confirmar en las otras cinco que el ejemplo es literal (con elipsis
visible si está recortado), no solo presente.

---

## V-4 — Toda sección citada desde el código existe en su destino

**El escenario más importante de todos.** Un reapuntado que deja una cita a
`sec.3.5a.5 task 3` en un documento que no tiene esa sección es peor que la cita vieja:
parece correcta y no lo es. Nada de esto falla en compilación — es prosa en comentarios.

**Paso 1.** Extraer todas las secciones que el código cita en los destinos nuevos:

```bash
grep -rhoE '(docs/spec/plan-0002-fase-3-5/spec-3\.5a\.md|docs/spec/plan-0002-fase-3-5/spec\.md|docs/spec/plan-0001-fase-3/spec\.md|docs/PROTOCOLO-DE-TRABAJO\.md) sec\.? ?[0-9a-zA-Z.]+' \
  --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules | sort -u
```

**Paso 2.** Para **cada línea** de esa salida, confirmar que la sección existe en el
documento indicado. Ejemplo con una:

```bash
grep -nE '^#+ .*3\.5a\.5' docs/spec/plan-0002-fase-3-5/spec.md
```

**Esperado:** cada sección citada produce al menos una coincidencia en su documento
destino. **Una sola que no aparezca invalida el escenario** y hay que corregirla antes del
PR.

**Nota:** este paso no se puede automatizar del todo porque los formatos de encabezado
varían (`3.5a.5`, `3.5a.5 task 3`, `sec. 2.2`). Es revisión asistida: el paso 1 da la lista
cerrada —son pocas decenas de secciones únicas— y el paso 2 se corre una vez por cada una.

---

## V-5 — No sobrevive ninguna cita a los nombres viejos

**Por qué:** es el criterio 7 del spec, con su excepción explícita.

**En código — tolerancia cero, sin excepciones:**

```bash
grep -rn "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4" \
  --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules | grep -v '\.claude/'
```

**Esperado:** cero líneas.

**Este escenario llevó una exclusión `sub_planes/` y ya no la lleva.** La versión original
exigía cero sin excepciones y era imposible de cumplir: devolvía 24, todas citas a
`docs/spec/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-{A,B,C}.md`. No eran citas olvidadas
—era el nombre de archivos que existían—, así que se excluyeron. La causa real era la
ubicación: esos ocho sub-planes colgaban huérfanos de `docs/spec/` cargando el nombre de
un plan ya borrado. El commit `d9699a4` los movió a `docs/spec/plan-0002-fase-3-5/sub-planes/` y
los renombró a `3.5a.2-A.md` … `3.5b.5-C.md`; las 24 citas quedaron repuntadas y la
exclusión sobra. **Si esta exclusión vuelve a hacer falta, algo se movió mal.**

**En documentación — solo sobreviven las históricas y deliberadas:**

```bash
grep -rln "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4" --include=*.md . \
  | grep -v node_modules | grep -v '\.claude/'
```

**Esperado:** 16 archivos, todos dentro de cinco categorías. Cualquier archivo fuera de
estas cinco es un reapuntado olvidado:

- **`docs/spec/feature-0003-reestructura-documentacion/`** (4: `plan.md`, `spec.md`, `tasks.md`,
  `test-e2e.md`) — esta carpeta documenta la migración; sus menciones son deliberadas.
- **Encabezados de procedencia de `fase-3/` y `fase-3-5/`** (4: `fase-3/plan.md`,
  `fase-3/spec.md`, `fase-3-5/spec.md`, `fase-3-5/test-e2e.md`) — citan el plan viejo como
  su origen histórico, igual que la excepción del criterio 7 del spec.
- **Los 6 ADR que mencionan el plan viejo** (0019, 0021, 0022, 0023, 0024, 0026) — un ADR
  no se edita, se reemplaza (misma regla que la exclusión `docs/adr/` de V-2). `0022` tiene
  el único enlace markdown real y roto (`](../spec/PLAN-FASE-3-5-PORCINO.md)`); los otros
  cinco solo citan el nombre como texto.
- **`docs/BACKLOG.md`** — declara estas deudas explícitamente: la entrada de
  `docs/adr/0022-rangos-plausibilidad.md:11` y la de `0020`/`0021`, que citan la ruta
  `sub_planes/` ya inexistente. Ambas con disparador; no se arreglan en esta rama por la
  regla de "un ADR no se edita".
- **`docs/PROTOCOLO-DE-TRABAJO.md`** — documento nuevo de esta rama; su sec. de riesgos
  cita `PLAN-FASE-3-4.md` sec. 6 como origen histórico del riesgo de deuda técnica
  transversal, no como ruta viva.

**Tres categorías desaparecieron respecto de la versión anterior de este escenario**, que
esperaba 26 archivos en seis categorías:

- Los **8 de `docs/spec/sub_planes/`** ya no existen con ese nombre (commit `d9699a4`).
- `fase-3-5/plan.md` y `fase-3-5/spec-3.5a.md` salieron de la categoría de procedencia: sus
  únicas menciones eran los nombres de archivo de los sub-planes, hoy renombrados.
- `docs/spec/feature-0002-field-app-parto-redesign/spec.md` **nunca estuvo en las seis categorías** y
  sin embargo aparecía en el `grep`: entró con el commit `53bc71b`, después de que se
  escribiera esta lista, citando `PLAN-FASE-3-5-PORCINO sec. 7-C`. Es decir, el conteo real
  era 27, no 26, y el escenario fallaba. Se repuntó a `fase-3-5/spec.md` sec. 7.

**Y los archivos ya no existen:**

```bash
ls docs/spec/PLAN-FASE-3-5-PORCINO.md docs/spec/PLAN-FASE-3-4.md 2>&1
```

**Esperado:** "No such file or directory" para ambos.

---

## V-6 — Ningún documento quedó huérfano de la taxonomía

**Por qué:** si `DOCUMENTACION.md` no menciona un documento, ese documento no tiene dueño ni
disparador de actualización, y en seis meses vuelve a estar obsoleto. Es la causa raíz.

```bash
for f in $(ls *.md docs/*.md); do
  base=$(basename "$f")
  grep -q "$base" docs/DOCUMENTACION.md || echo "HUÉRFANO: $f"
done
```

**Esperado:** ninguna línea `HUÉRFANO`.

```bash
ls docs/spec/plan-0003-fase-4/ docs/spec/plan-0004-fase-5/
```

**Esperado:** solo `spec.md` en cada una (D5). Si aparece `plan.md`, `tasks.md` o
`test-e2e.md`, se violó una decisión fijada.

```bash
ls docs/spec/plan-0001-fase-3/ docs/spec/plan-0002-fase-3-5/
```

**Esperado:** `fase-3/` con sus cuatro archivos (`spec.md`, `plan.md`, `tasks.md`,
`test-e2e.md`); `fase-3-5/` con esos mismos cuatro más `spec-3.5a.md` —cinco en total—. El
commit 6 partió el spec de fase 3.5 en dos (`spec.md` y `spec-3.5a.md`, tarea T11.2) para no
recrear el archivo de 833 líneas que la sec. 13 del `spec.md` de esta carpeta advertía como
inmanejable. La versión original de este escenario pedía "los cuatro archivos" en ambas
carpetas por igual, sin contemplar la partición: era imposible de cumplir en `fase-3-5/` sin
deshacer esa decisión.

---

## V-7 — Los diagramas no mienten

**Por qué:** el hallazgo 2.5 del spec. Un DER que dibuja `PADDOCKS` sin decir que no existe
miente con autoridad, y es el tipo de error que otro agente propaga al código.

```bash
grep -L "Verificado el" docs/diagramas/*.mermaid
```

**Esperado:** ninguna salida. Todo diagrama lleva encabezado con fecha de verificación
(T10.6).

**Comprobación puntual del caso ya diagnosticado:**

```bash
grep -nE 'PADDOCKS|GRAZING_MOVEMENTS|UNITS' docs/diagramas/der-3-grupos-inventario.mermaid
grep -n 'FEED_STAGES' docs/diagramas/der-3-grupos-inventario.mermaid
```

**Esperado:** las tres primeras, o ausentes, o precedidas de `%% FUTURO — Fase N`.
`FEED_STAGES`, presente.

```bash
ls docs/diagramas/flujo-*.mermaid | wc -l
```

**Esperado:** `3`.

**Juicio humano:** pegar los tres flujos nuevos en un visor de Mermaid y confirmar que
renderizan sin error de sintaxis (T10.11).

---

## V-8 — `SEGURIDAD.md` cubre toda la superficie

**Por qué:** un documento de seguridad incompleto es peor que ninguno, porque quien lo lee
asume que lo que no está listado no existe.

```bash
for f in src/Hato.Api/Endpoints/*.cs; do
  base=$(basename "$f" .cs)
  grep -q "$base" docs/SEGURIDAD.md || echo "SIN DOCUMENTAR: $base"
done
```

**Esperado:** ninguna línea `SIN DOCUMENTAR`. Son 19 archivos de endpoints.

```bash
grep -c "RequiredPermissionByCollection" docs/SEGURIDAD.md
```

**Esperado:** al menos `1`. Es el punto donde un error entrega datos a quien no debe
(T9.4).

**Juicio humano:** confirmar que la sección de huecos conocidos existe, que cada hueco tiene
fecha, y que cada uno tiene su entrada correspondiente en `docs/BACKLOG.md` (T9.7).

---

## Cierre de la verificación

Los ocho escenarios en verde **no** cierran la rama por sí solos. Falta:

- Los 14 criterios de aceptación del [`spec.md`](./spec.md) sec. 14.
- `dotnet test` y `npm test` en verde (TC.3, TC.4). Esta rama no toca código ejecutable,
  así que un fallo aquí es una regresión ajena — hay que detectarla antes del merge, no
  después.

Si un escenario falla **después** del commit 11, recordar que ese commit es el punto de no
retorno: la corrección se hace hacia adelante, con un commit nuevo, nunca reescribiendo el
11.
