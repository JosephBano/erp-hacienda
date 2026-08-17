# plan.md — Ejecución de la rama `<nombre-de-la-rama>`

> **Plantilla.** Calcada de `docs/spec/feature-0002-field-app-parto-redesign/plan.md` y de
> `docs/spec/feature-0003-reestructura-documentacion/plan.md`. Borrá este bloque al usarla.
> Un `plan.md` responde **en qué orden y en qué commits** se hace lo que `spec.md` decidió.
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

> **Qué es este documento.** Una frase que enlace a `spec.md` (qué se decidió), `tasks.md`
> (el desglose ejecutable con casillas) y `test-e2e.md` (la verificación).

**Objetivo:** una frase, la misma idea que el "por qué" de `spec.md` sec. 1, en modo
ejecución.

**Enfoque:** una rama, **N commits secuenciales** por propósito. Si hay compuertas que
detienen el trabajo si su verificación falla, decilo acá. Ejemplo real,
`reestructura-documentacion/plan.md`: *"once commits secuenciales... más dos compuertas que
detienen el trabajo si su verificación falla"*.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

> Copiadas literalmente de las decisiones de `spec.md` que aplican a **todos** los commits,
> no solo a uno. Si `spec.md` no tiene ninguna transversal, borrá esta sección entera — no la
> dejes vacía con un placeholder.

- `<restricción, copiada del spec, con referencia a la decisión que la origina>`.

---

## Índice

1. [Commit 1 — `<propósito>`](#commit-1--propósito)
2. `<...>`
3. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
4. [Descripción del PR](#descripción-del-pr)

---

## Commit 1 — `<propósito corto>`

> Mensaje de commit en **inglés**, formato Conventional Commits. Ejemplo real,
> `reestructura-documentacion/plan.md` commit 2: `` `docs: add the document templates under
> docs/plantillas` ``.

`<tipo>(<alcance opcional>): <mensaje en inglés, Conventional Commits>`

**Por qué acá:** por qué este commit va en esta posición de la secuencia y no en otra —
normalmente porque otro commit depende de que este exista primero.

**Archivos:**
- Crear/Modificar/Borrar: `<ruta>` — qué cambia y por qué.

**Verificación:** el comando exacto y su salida esperada. Ejemplo real,
`reestructura-documentacion/plan.md` commit 3: *"la tabla lista todos los `.md` de `docs/` y
de la raíz. Comprobar con `ls docs/*.md` y `ls *.md` que ninguno queda fuera de la
taxonomía."*

## Orden, dependencias y puntos de no retorno

> Diagrama de dependencias entre commits, aunque sea en texto plano con indentación. Marcá
> explícitamente cuál commit es el punto de no retorno, si lo hay — el que hace `git revert`
> costoso o imposible.

```
Commit 1
  └─ Commit 2 ── bloquea 3, 4 (razón)
```

## Descripción del PR

> Lo que va en el cuerpo del PR cuando se abra: qué, por qué, qué decisiones (enlazando a
> `spec.md` sec. 3), qué NO incluye, cómo probarlo (enlazando a `test-e2e.md`), riesgo
> declarado.

**Título:** `<mensaje corto, bajo 70 caracteres>`

**Cuerpo:**

- **Qué:** ...
- **Por qué:** ...
- **Decisiones:** ...
- **Qué NO incluye:** ...
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
