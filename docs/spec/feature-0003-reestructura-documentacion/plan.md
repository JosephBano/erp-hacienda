# plan.md — Ejecución de la rama `docs/reestructura-documentacion`

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA — usar
> `superpowers:subagent-driven-development` (recomendada) o
> `superpowers:executing-plans` para ejecutar este plan tarea por tarea. El desglose con
> casillas está en [`tasks.md`](./tasks.md).
>
> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué verificación exige y cómo se
> mergea. Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec
> primero.

**Objetivo:** convertir la documentación del proyecto en un sistema con taxonomía, ciclo de
vida y plantillas, y absorber los dos planes de fase heredados en carpetas por fase.

**Enfoque:** una rama, **once commits secuenciales** por propósito, más **dos compuertas**
que detienen el trabajo si su verificación falla. El borrado de los planes viejos es el
último commit y el único punto de no retorno.

**Herramientas:** Markdown, Mermaid, `git worktree`, `grep`, `sed`. Cero cambios de código
ejecutable — el único código que se toca son comentarios reapuntados.

**Spec:** [`spec.md`](./spec.md), aprobado y commiteado en `839630a`.

---

## Restricciones globales

Aplican a **todos** los commits de esta rama. Copiadas del spec.

- **La numeración de secciones de los planes viejos se preserva íntegra** (D6).
  `sec.3.5a.5 task 3` sigue llamándose así en su documento destino. Si una sección tuviera
  que renumerarse, el mapeo viejo → nuevo se documenta en `tasks.md` antes de tocar nada.
- **Un documento existe si responde una pregunta que ningún otro responde** (D1).
- **Todo elemento aspiracional de un diagrama se marca o no entra** (D11).
- **Idioma:** español para documentación y dominio; inglés para código y mensajes de commit
  (`AGENTS.md`, Conventional Commits).
- **No se toca código ejecutable.** Solo comentarios, y solo para reapuntar citas.
- **No se abren PRs intermedios.** El conjunto entra como un PR único; los commits permiten
  revisarlo por partes.
- **Fechas en los documentos:** `AAAA-MM-DD`, absolutas, nunca relativas.

---

## Índice

1. [Compuerta 0 — worktrees](#compuerta-0--worktrees)
2. [Commit 1 — BACKLOG único](#commit-1--backlog-único)
3. [Commit 2 — Plantillas](#commit-2--plantillas)
4. [Commit 3 — `DOCUMENTACION.md`](#commit-3--documentacionmd)
5. [Commit 4 — `PROTOCOLO-DE-TRABAJO.md`](#commit-4--protocolo-de-trabajomd)
6. [Commit 5 — `fase-3/`](#commit-5--fase-3)
7. [Commit 6 — `fase-3-5/`](#commit-6--fase-3-5)
8. [Compuerta A — auditoría de la Fase 3.5](#compuerta-a--auditoría-de-la-fase-35)
9. [Commit 7 — marcas verificadas](#commit-7--marcas-verificadas)
10. [Commit 8 — `fase-4/` y `fase-5/`](#commit-8--fase-4-y-fase-5)
11. [Commit 9 — `SEGURIDAD.md`](#commit-9--seguridadmd)
12. [Commit 10 — Diagramas](#commit-10--diagramas)
13. [Commit 11 — Reapuntado y borrado](#commit-11--reapuntado-y-borrado)
14. [Orden, dependencias y puntos de no retorno](#14-orden-dependencias-y-puntos-de-no-retorno)
15. [Descripción del PR](#15-descripción-del-pr)

---

## Compuerta 0 — worktrees

**Nada de lo que sigue se ejecuta hasta cerrar esto** (D14). Sin la limpieza, todo `grep`
de verificación de esta misma rama devuelve siete copias de cada resultado y las
verificaciones de los commits siguientes no valen nada.

No produce commit: los worktrees no están versionados (`.git/info/exclude:11`).

```bash
for w in agent-a6c890111a45ff939 agent-a8555d67733b8cc70 agent-a8c9f14ca85bc27ad \
         agent-acb103415e30d2b2a agent-ad5f1515cadb3383e agent-aed17ae3da9b4cc92; do
  git worktree remove --force ".claude/worktrees/$w"
done
git worktree prune
```

**Verificación (bloqueante):**

```bash
git worktree list | wc -l   # debe imprimir 1
```

Si imprime más de 1, **detenerse**: un worktree con cambios sin commitear puede contener
trabajo real de otra sesión. Inspeccionarlo antes de forzar.

## Commit 1 — BACKLOG único

`docs: merge the two backlogs into a single docs/BACKLOG.md`

**Por qué primero:** es el duplicado que el dueño detectó por su cuenta y el ejemplo más
puro de la falla que D1 corrige. Cerrarlo temprano da una victoria verificable antes de los
commits grandes.

**Archivos:**
- Modificar: `docs/BACKLOG.md` — recibe el contenido de ambos.
- Borrar: `BACKLOG.md` (raíz).
- Modificar: `AGENTS.md` — la sección "Dónde están las cosas" apunta a `BACKLOG.md`;
  pasa a `docs/BACKLOG.md`.

**Cómo se funde (D9):** `docs/BACKLOG.md` queda con dos secciones de primer nivel que
conservan **íntegro** lo que hoy vive en cada archivo:

```markdown
## Deuda abierta por sub-rama      <- las 334 líneas de la raíz
## Ideas fuera de la fase actual   <- las 192 líneas de docs/
```

**No se fusionan ítems por parecerse.** Si dos entradas hablan de lo mismo, se dejan las
dos y se anota la relación con una línea; decidir que son la misma es una decisión de
producto, no de formato.

**Verificación:** el conteo de líneas del resultado es ≥ 334 + 192 menos los encabezados
que se consolidan, y `grep -c '^- \|^### ' docs/BACKLOG.md` no es menor que la suma de los
mismos conteos en los dos originales (medir **antes** de borrar).

## Commit 2 — Plantillas

`docs: add the document templates under docs/plantillas`

**Por qué acá:** los commits 5–8 escriben documentos nuevos. Si las plantillas no existen
antes, esos documentos fijan por accidente una convención distinta a la que este spec
define.

**Archivos:**
- Crear: `docs/plantillas/TEMPLATE-spec.md`, `TEMPLATE-plan.md`, `TEMPLATE-tasks.md`,
  `TEMPLATE-test-e2e.md`, `TEMPLATE-diagrama.md`.
- Mover: `docs/adr/TEMPLATE.md` → `docs/plantillas/TEMPLATE-adr.md` (`git mv`, contenido
  sin cambios).
- Modificar: `AGENTS.md` — la convención de ADRs dice *"La plantilla es
  `docs/adr/TEMPLATE.md`"*; pasa a la ruta nueva.

Cada plantilla lleva instrucciones embebidas en citas de bloque y **un ejemplo corto real
de este repositorio**. La razón de la subcarpeta, en la línea que exige la convención de
`docs/`: *"las plantillas se buscan en un solo lugar"*.

**Verificación:** `ls docs/plantillas/*.md | wc -l` imprime 6, y
`grep -rn "docs/adr/TEMPLATE.md" --include=*.md .` devuelve cero.

## Commit 3 — `DOCUMENTACION.md`

`docs: define the documentation system and its update triggers`

**Archivos:** crear `docs/DOCUMENTACION.md`. Modificar `AGENTS.md` para enlazarlo desde
"Dónde están las cosas".

Contenido: la tabla de taxonomía de la sec. 5 del spec (20 documentos × pregunta que
responde × cuándo cambia), los disparadores de actualización de la sec. 5.1, y la regla de
archivado de la sec. 5.2.

**Verificación:** la tabla lista todos los `.md` de `docs/` y de la raíz. Comprobar con
`ls docs/*.md` y `ls *.md` que ninguno queda fuera de la taxonomía.

## Commit 4 — `PROTOCOLO-DE-TRABAJO.md`

`docs: extract the cross-cutting working protocol from the phase plan`

**Archivos:** crear `docs/PROTOCOLO-DE-TRABAJO.md`. Modificar `AGENTS.md`.

**Origen exacto** (secciones de `PLAN-FASE-3-4.md`, que en este commit todavía existe):

| Origen | Contenido |
|---|---|
| sec. 1.1 | Ciclo de nueve pasos |
| sec. 1.2 | Checklist de autorrevisión, 10 puntos |
| sec. 1.3 | Ritmo y bloques |
| sec. 2.1 | Exigencia de pruebas por capa |
| sec. 2.3 | Exigencias de pruebas de dinero (llegan de Fase 4) |
| sec. 2.4–2.5 | Umbrales de salida y refuerzos de CI |

Se agrega una sección nueva: **los disparadores de actualización documental** de la sec.
5.1 del spec, incorporados a la checklist de autorrevisión.

`AGENTS.md` conserva su resumen de cinco líneas y **enlaza** aquí. No se duplica (D1).

**Verificación:** las secciones 1.1–1.3 y 2.1–2.5 del plan viejo tienen contraparte, y
`AGENTS.md` no repite su contenido.

## Commit 5 — `fase-3/`

`docs(planes): convert phase 3 into its own folder`

**Archivos:** crear `docs/spec/plan-0001-fase-3/{spec,plan,tasks,test-e2e}.md`.

**Origen:** `PLAN-FASE-3-4.md` secs. 2.2 (los 10 escenarios obligatorios de
sincronización) y 3 (bloques 3.A–3.C, ~11 ramas), más el estado de la fase que vive en
`ROADMAP.md:33-56` (el cierre revertido y los tres defectos que lo motivaron) y
`ROADMAP.md:109-142` (objetivo, criterio de salida y pendiente para cerrar).

**Numeración (D6):** `sec.2.2` y `sec.3.x` **conservan su número** dentro de
`fase-3/spec.md`, porque 21 citas del código apuntan ahí. El encabezado del archivo declara
de qué documento provienen.

**`tasks.md` de esta carpeta:** todo `[x]` salvo el piloto real, que queda `[ ]` con la
nota de `ROADMAP.md:121-125` — *"sólo falta una cosa y es deliberadamente ajena al código:
el piloto real"*.

**Verificación:** `grep -c 'sec\.2\.2\|sec\.3\.' docs/spec/plan-0001-fase-3/spec.md` > 0 y las
secciones existen con esos números.

## Commit 6 — `fase-3-5/`

`docs(planes): convert phase 3.5 into its own folder`

El commit más grande. **Archivos:** crear `docs/spec/plan-0002-fase-3-5/{spec,plan,tasks,test-e2e}.md`.

**Origen:** las 833 líneas de `PLAN-FASE-3-5-PORCINO.md`, repartidas según qué es cada
cosa: decisiones y hallazgos a `spec.md`, secuencia a `plan.md`, checklist a `tasks.md`,
verificación manual a `test-e2e.md`. Más el estado de `ROADMAP.md:146-217` y los ocho
sub-planes, que **se enlazan, no se absorben** — en este commit todavía desde
`docs/spec/sub_planes/`; el commit 14 los mueve a `fase-3-5/sub-planes/`.

**Numeración (D6):** `3.5a.0` … `3.5a.9` y `3.5b.1` … conservan su número. 85 citas del
código dependen de esto.

**Sección obligatoria — "Adelantos de Fase 4 que viven aquí"** (D7): la tabla del hallazgo
2.9 del spec, con las cinco piezas, dónde viven y su contrato de caducidad literal.

**Verificación:** para cada sección citada desde el código, existe con el mismo número.
El comando exacto está en [`test-e2e.md`](./test-e2e.md) V-4.

## Compuerta A — auditoría de la Fase 3.5

**Se lanza solo cuando `docs/spec/plan-0002-fase-3-5/tasks.md` existe** (commit 6 hecho).

Un agente de **contexto limpio**, **solo lectura**, con la regla dura D13: marca `[x]`
únicamente contra evidencia de código —archivo y línea—, nunca contra lo que un documento
afirme. Si `ROADMAP.md` dice que algo se mergeó y el código no lo respalda, **lo reporta
como discrepancia**.

**Entregables:** `tasks.md` con marcas, y una lista de discrepancias documento-vs-código.

**Compuerta:** un informe **sin ninguna discrepancia** en un repositorio de este tamaño es
sospechoso, no tranquilizador. Si vuelve vacío, revisar el prompt del agente antes de
aceptarlo.

## Commit 7 — marcas verificadas

`docs(planes): mark phase 3.5 tasks verified against the codebase`

**Archivos:** modificar `docs/spec/plan-0002-fase-3-5/tasks.md` con las marcas del agente. Anotar
las discrepancias en `docs/BACKLOG.md` si no se resuelven en esta rama.

Este commit va **separado** del 6 a propósito: el diff muestra exactamente qué se dio por
hecho y contra qué evidencia, en vez de esconderlo dentro de un archivo nuevo de 300
líneas.

## Commit 8 — `fase-4/` y `fase-5/`

`docs(planes): spec phases 4 and 5 without planning them`

**Archivos:** crear `docs/spec/plan-0003-fase-4/spec.md` y `docs/spec/plan-0004-fase-5/spec.md`. **Solo el
spec** en ambos casos (D5).

**`fase-4/spec.md`** — cuatro partes, en este orden:
1. Objetivo y criterio de salida (`ROADMAP.md:220-231`).
2. **Deuda heredada de la Fase 3.5**: enlace a la sección de adelantos de
   `fase-3-5/spec.md`, con las cinco piezas y sus contratos de caducidad. Es lo que la
   Fase 4 debe jubilar antes de cerrarse.
3. Preguntas abiertas previas a planificar: proveedor autorizado del SRI, plan de cuentas,
   qué acepta realmente el contador.
4. **Apéndice — "Planificación previa (2026-08-02): insumo, no compromiso"** con los
   bloques 4.A–4.D (D8).

Las exigencias de pruebas de dinero **no** van aquí: ya se fueron al
`PROTOCOLO-DE-TRABAJO.md` en el commit 4.

**`fase-5/spec.md`** — objetivo, criterio de salida (`ROADMAP.md:235-243`) y preguntas
abiertas: registro sanitario ARCSA, el caso real del queso fresco, si Grazing entra
completo o recortado. Nada más.

## Commit 9 — `SEGURIDAD.md`

`docs: document the security model from a code audit`

**Archivos:** crear `docs/SEGURIDAD.md`. Modificar `docs/BACKLOG.md` con los huecos
hallados.

Se escribe **auditando el código** (D12), no copiando ADR-0007 y ADR-0008. Las seis
secciones están en la sec. 9 del spec. La más laboriosa y la más valiosa: la tabla
**endpoint → permiso exigido**, generada leyendo los 19 archivos de
`src/Hato.Api/Endpoints/`.

**Regla:** lo que la auditoría encuentre roto **se documenta, no se arregla acá** (regla 9).
Si algo resulta explotable, se abre rama propia con prioridad y se avisa al dueño antes de
seguir.

**Verificación:** todo archivo de `src/Hato.Api/Endpoints/*.cs` aparece en la tabla.

## Commit 10 — Diagramas

`docs(diagramas): audit the ERDs against the real schema and add flow diagrams`

**Archivos:** modificar los cinco `docs/diagramas/der-*.mermaid`. Crear
`flujo-sincronizacion.mermaid`, `flujo-autenticacion.mermaid`,
`flujo-consumo-alimento.mermaid`.

**Auditoría de los DER:** contrastar cada entidad contra el esquema real. `der-3` ya tiene
diagnóstico (spec 2.5): sobran `PADDOCKS`, `GRAZING_MOVEMENTS` y `UNITS`; falta
`FEED_STAGES`. Los otros cuatro se auditan igual.

**Marcado (D11):** lo que no existe se marca `%% FUTURO — Fase N` justo encima de la
entidad, o se borra. Cada archivo gana un encabezado con fecha de verificación y contra qué
migración se verificó.

**Los tres flujos nuevos:** push/pull de sincronización con idempotencia y cursor;
autenticación con login, refresco y expiración; consumo de alimento con conversión de
unidad y descuento FIFO por lote.

## Commit 11 — Reapuntado y borrado

`docs: repoint all citations and remove the superseded phase plans`

**Punto de no retorno.** Va último a propósito: hasta acá los planes viejos conviven con
las carpetas nuevas y el diff es auditable (spec sec. 13).

**Enrutamiento de las citas.** Verificado el 2026-08-16:

| Origen | Veces en código | Destino |
|---|---|---|
| `PLAN-FASE-3-5-PORCINO(.md) sec.3.5a.*` | 40 | `docs/spec/plan-0002-fase-3-5/spec-3.5a.md` |
| `PLAN-FASE-3-5-PORCINO(.md)` — resto (`sec.2.3`, `sec.7`) | 45 | `docs/spec/plan-0002-fase-3-5/spec.md` |
| `PLAN-FASE-3-4 sec.2.2` | 12 | `docs/spec/plan-0001-fase-3/spec.md` |
| `PLAN-FASE-3-4 sec.3.x` | 8 | `docs/spec/plan-0001-fase-3/spec.md` |
| `PLAN-FASE-3-4 sec.2.1` | **2** | `docs/PROTOCOLO-DE-TRABAJO.md` |
| `PLAN-FASE-3-4 sec.4` | **0** | — |

**Las dos citas a `sec.2.1` se reapuntan a mano y primero**, porque van a otro documento y
un `sed` global las mandaría al destino equivocado. Localizarlas con:

```bash
grep -rn "PLAN-FASE-3-4 sec\.2\.1" --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules
```

**Después** el reemplazo mecánico del resto. El orden importa: la variante con `.md` va
antes que la desnuda, si no la desnuda deja un `.md` colgando.

**El commit 6 partió el spec de la Fase 3.5 en dos** (`spec.md` y `spec-3.5a.md`) para no
recrear el archivo inmanejable que la sec. 13 del spec advertía. Consecuencia directa: el
reemplazo **no puede ser un patrón único**. Se enruta por la sección citada, y el orden
importa — las citas a `3.5a` primero, porque el patrón general las capturaría.

```bash
# Paso 1: las 40 citas a secciones 3.5a.* -> spec-3.5a.md
grep -rl "PLAN-FASE-3-5-PORCINO" --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules \
  | xargs sed -i -E 's#PLAN-FASE-3-5-PORCINO(\.md)? (sec\.? ?3\.5a)#docs/spec/plan-0002-fase-3-5/spec-3.5a.md \2#g'

# Paso 2: el resto (sec.2.3, sec.7 y menciones sin sección) -> spec.md
grep -rl "PLAN-FASE-3-5-PORCINO" --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules \
  | xargs sed -i 's#PLAN-FASE-3-5-PORCINO\.md#docs/spec/plan-0002-fase-3-5/spec.md#g;
                  s#PLAN-FASE-3-5-PORCINO#docs/spec/plan-0002-fase-3-5/spec.md#g'

grep -rl "PLAN-FASE-3-4" --include=*.cs --include=*.ts --include=*.tsx . \
  | grep -v node_modules \
  | xargs sed -i 's#PLAN-FASE-3-4\.md#docs/spec/plan-0001-fase-3/spec.md#g;
                  s#PLAN-FASE-3-4#docs/spec/plan-0001-fase-3/spec.md#g'
```

Después las 130 citas en `.md`, con el mismo enrutamiento y respetando las excepciones del
criterio 7 del spec (esta carpeta y los encabezados de procedencia).

**Y recién entonces:**

```bash
git rm docs/spec/PLAN-FASE-3-5-PORCINO.md docs/spec/PLAN-FASE-3-4.md
```

**Verificación completa:** [`test-e2e.md`](./test-e2e.md), los ocho escenarios.

## Commits 12–17 — correcciones y alcance añadido después del no retorno

El commit 11 fijó la estructura, pero no la dejó cerrada. Lo que vino después no estaba en
el plan original y se documenta acá para que la secuencia real sea auditable:

| # | Commit | Qué |
|---|---|---|
| 12 | `987d325` | Corrección del propio commit 11: 24 citas del código habían quedado apuntando a `spec.md-3.5a.2-A`, una ruta inexistente producto del `sed` masivo. Se repuntaron a `sub_planes/`. |
| 13 | `8cc7a2c` | Rescate de secciones huérfanas de `PLAN-FASE-3-4.md` (ADR, riesgos, resumen de ramas) que el borrado se llevó por delante. |
| 14 | `53bc71b`, `215105c` | Se trae el plan de rediseño de parto de `field-app` y se alinea con las plantillas. |
| 15 | `875c65f` | **Alcance añadido a pedido del dueño:** `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` → `docs/spec/feature-0001-admin-web-animal-groups/`, y la convención de carpeta escrita en `AGENTS.md`. |
| 16 | `d9699a4` | **Alcance añadido a pedido del dueño:** `docs/spec/sub_planes/` → `docs/spec/plan-0002-fase-3-5/sub-planes/`, con los ocho archivos renombrados a `3.5a.2-A.md` … `3.5b.5-C.md` y las 24 citas del código repuntadas otra vez, ahora a la ruta definitiva. |
| 17 | este | Alcance y verificaciones alineados con 15 y 16: el criterio 7 pierde la exclusión `sub_planes/` y baja de 26 a 16 archivos `.md`; V-3 de `fase-3-5` y V-5 de esta carpeta se reescriben. |

Los commits 12 y 13 son la evidencia de por qué el 11 se declaró punto de no retorno: un
`sed` sobre 238 citas deja residuo, y el residuo sólo aparece al releer el resultado.

## 14. Orden, dependencias y puntos de no retorno

```
Compuerta 0 (worktrees)  ── bloquea todo
  └─ Commit 1  BACKLOG
     └─ Commit 2  Plantillas ── bloquea 5, 6, 8 (fijan la convención)
        └─ Commit 3  DOCUMENTACION
           └─ Commit 4  PROTOCOLO ── destino de 2 citas del commit 11
              ├─ Commit 5  fase-3   ── destino de 21 citas del commit 11
              └─ Commit 6  fase-3-5 ── destino de 85 citas del commit 11
                 └─ Compuerta A (agente auditor)
                    └─ Commit 7  marcas verificadas
                       └─ Commit 8  fase-4 y fase-5
                          └─ Commit 9  SEGURIDAD
                             └─ Commit 10 Diagramas
                                └─ Commit 11 REAPUNTADO Y BORRADO  ← no retorno
```

**Reversible hasta el commit 10 inclusive**: los planes viejos siguen ahí y basta
`git revert` de los commits nuevos. **Desde el commit 11 no**: la vuelta atrás exige
restaurar dos archivos y deshacer 238 reemplazos.

**Los commits 9 y 10 son independientes entre sí** y del 8. Si hay que recortar alcance por
tiempo, son los candidatos a salir a rama aparte — pero no pueden quedarse a medias: un
`SEGURIDAD.md` incompleto es peor que ninguno.

## 15. Descripción del PR

**Título:** `docs: restructure project documentation into a system`

**Cuerpo:**

- **Qué:** taxonomía de 19 documentos con su ciclo de vida, seis plantillas, protocolo de
  trabajo extraído a documento propio, cuatro carpetas por fase, documentación de seguridad
  desde auditoría, diagramas corregidos y tres flujos nuevos.
- **Por qué:** la documentación dejó de ser navegable. Dos BACKLOG, diagramas que mezclan
  tablas inexistentes con tablas faltantes, cero documentación de seguridad, y un archivo
  llamado "plan de las fases 3 y 4" que contenía el manual de trabajo del proyecto.
- **Decisiones:** las 14 del spec, con quién decidió cada una.
- **Qué NO incluye:** ningún cambio de código ejecutable —salvo comentarios reapuntados— y
  los `plan.md`/`tasks.md`/`test-e2e.md` de las Fases 4 y 5 (D5).
  `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` y los ocho `sub_planes/` **sí entraron**, a pedido del
  dueño: hoy son `docs/spec/feature-0001-admin-web-animal-groups/` y
  `docs/spec/plan-0002-fase-3-5/sub-planes/`. Con eso `docs/spec/` no tiene ningún `.md` suelto.
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md), los ocho escenarios.
- **Riesgo declarado:** el commit 11 es punto de no retorno; hasta el 10 todo es
  `git revert`.
