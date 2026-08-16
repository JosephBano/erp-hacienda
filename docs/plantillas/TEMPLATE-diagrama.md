# TEMPLATE-diagrama.md — Convención de diagramas

> Esto **no es un esqueleto para copiar y pegar** como los otros cuatro. Es la convención que
> gobierna todo archivo de `docs/diagramas/`: cómo se nombra, cómo se marca lo aspiracional,
> qué encabezado lleva y cuándo hay que tocarlo. Fuente: `spec.md` sec. 10 de
> `docs/planes/reestructura-documentacion/` (decisión D11).

## Nombre

- DER (entidad-relación) de un núcleo de `DATA-MODEL.md`: `der-N-<tema>.mermaid`. Ejemplos
  reales ya en el repo: `der-3-grupos-inventario.mermaid`, `der-4-reproduccion.mermaid`.
- Diagrama de flujo de un proceso: `flujo-<proceso>.mermaid` (p. ej.
  `flujo-sincronizacion.mermaid`).
- Los diagramas embebidos dentro de un `.md` (una tabla ASCII de ejemplo, un mini-flujo en
  medio de una sección) se quedan donde están; acá van solo los completos por núcleo o por
  proceso.

## Encabezado obligatorio

Todo archivo `.mermaid` empieza con un comentario Mermaid de dos líneas:

```mermaid
%% Verificado el AAAA-MM-DD contra la migración <NombreDeLaMigración>
%% <qué cambia y obliga a revisar este diagrama — ver sec. "Cuándo actualizarlo">
```

## Marcado de lo aspiracional (D11) — sin excepción

**Todo elemento que no existe en la base real se marca, o no se dibuja.** Un DER que dibuja
una entidad futura sin decir que no existe miente con autoridad: parece verificado y no lo
está.

```mermaid
%% FUTURO — Fase 5
PADDOCKS ||--o{ GRAZING_MOVEMENTS : "recibe"
```

**Ejemplo real de la falla que esta convención existe para evitar.** Al 2026-08-16,
`docs/diagramas/der-3-grupos-inventario.mermaid` declara `PADDOCKS`, `GRAZING_MOVEMENTS` y
`UNITS` **sin ningún comentario `%% FUTURO`** — son de Fase 5 y nunca se construyeron (`UNITS`
en particular: lo que existe de verdad es `unit_conversions` por ítem). El mismo archivo
**le falta** `FEED_STAGES`, que sí existe desde la migración `20260809050404_AddFeedStages`.
Es simultáneamente aspiracional y obsoleto, y no dice cuál parte es cuál — el diagnóstico
completo está en `docs/planes/reestructura-documentacion/spec.md` sec. 2.5. Corregirlo es
trabajo del commit 10 de esa rama, no de esta plantilla.

## Cuándo actualizarlo

- **Entra una migración que toca el núcleo del diagrama** → se revisa antes de cerrar la
  rama que trae la migración (disparador de `docs/DOCUMENTACION.md` sec. 5.1).
- **Un elemento marcado `%% FUTURO — Fase N` se construye** → se le quita la marca en el
  mismo commit que lo construye, no después.
- **Cambia un flujo** (por ejemplo, el protocolo de sincronización gana un paso) → se
  actualiza el diagrama de flujo correspondiente y se reescribe la fecha del encabezado.
- Un diagrama con fecha de verificación de más de una fase de antigüedad es candidato a
  auditoría antes de confiar en él para diseño nuevo.
