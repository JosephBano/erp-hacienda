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
  | grep -v 'docs/BACKLOG\.md'
```

**Esperado:** cero líneas. Cualquier resultado es una referencia que quedó apuntando a la
raíz.

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

**Esperado:** ninguna por debajo de 20 líneas. Una plantilla de 10 líneas es un título con
viñetas, no una plantilla.

```bash
ls docs/adr/TEMPLATE.md 2>/dev/null
grep -rn "docs/adr/TEMPLATE\.md" --include=*.md . | grep -v node_modules | grep -v '\.claude/'
```

**Esperado:** el archivo ya no existe y ninguna referencia lo menciona.

**Juicio humano:** abrir las seis y confirmar que cada una tiene instrucciones embebidas
**y** un ejemplo corto real de este repositorio (T2.8).

---

## V-4 — Toda sección citada desde el código existe en su destino

**El escenario más importante de todos.** Un reapuntado que deja una cita a
`sec.3.5a.5 task 3` en un documento que no tiene esa sección es peor que la cita vieja:
parece correcta y no lo es. Nada de esto falla en compilación — es prosa en comentarios.

**Paso 1.** Extraer todas las secciones que el código cita en los destinos nuevos:

```bash
grep -rhoE '(docs/planes/fase-3-5/spec\.md|docs/planes/fase-3/spec\.md|docs/PROTOCOLO-DE-TRABAJO\.md) sec\.[0-9a-zA-Z.]+' \
  --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules | sort -u
```

**Paso 2.** Para **cada línea** de esa salida, confirmar que la sección existe en el
documento indicado. Ejemplo con una:

```bash
grep -nE '^#+ .*3\.5a\.5' docs/planes/fase-3-5/spec.md
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

**En código — tolerancia cero:**

```bash
grep -rn "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4" \
  --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules | grep -v '\.claude/'
```

**Esperado:** cero líneas.

**En documentación — solo sobreviven las históricas y deliberadas:**

```bash
grep -rln "PLAN-FASE-3-5-PORCINO\|PLAN-FASE-3-4" --include=*.md . \
  | grep -v node_modules | grep -v '\.claude/'
```

**Esperado:** únicamente archivos de `docs/planes/reestructura-documentacion/` (que
documenta la migración) y los encabezados de procedencia de `fase-3/` y `fase-3-5/`.
Cualquier otro archivo en la lista es un reapuntado olvidado.

**Y los archivos ya no existen:**

```bash
ls docs/planes/PLAN-FASE-3-5-PORCINO.md docs/planes/PLAN-FASE-3-4.md 2>&1
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
ls docs/planes/fase-4/ docs/planes/fase-5/
```

**Esperado:** solo `spec.md` en cada una (D5). Si aparece `plan.md`, `tasks.md` o
`test-e2e.md`, se violó una decisión fijada.

```bash
ls docs/planes/fase-3/ docs/planes/fase-3-5/
```

**Esperado:** los cuatro archivos en cada una.

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
