# spec.md — &lt;Título corto de lo que se construye o cambia&gt;

> **Plantilla.** Calcada de la estructura de `docs/planes/field-app-parto-redesign/spec.md`
> y de `docs/planes/reestructura-documentacion/spec.md`. Borrá este bloque de cita al usarla.
> Un `spec.md` responde **qué se construye, por qué, y qué decisiones quedan fijadas**. El
> *cómo* día a día va en `plan.md`, el desglose ejecutable en `tasks.md`, la verificación en
> `test-e2e.md`.

> **Qué es este documento.** Una frase que diga qué decide este spec y enlace a los otros
> tres documentos de la carpeta.
>
> **Por qué esta subcarpeta.** Si el trabajo es de una sola rama con estos cuatro documentos,
> explicá por qué no van sueltos en `docs/planes/` (huérfanos entre sí).

- **Rama Git:** `<nombre-de-la-rama>` (desde `develop`, en `<hash-corto>`).
- **Fecha:** AAAA-MM-DD.
- **Fase del ROADMAP:** `<fase>` — o "transversal" si no abre ni cierra ninguna.
- **ADRs vigentes que respalda o respeta:** `<ADR-NNNN (tema), ...>`.
- **Reglas duras que gobiernan este trabajo:** citar `AGENTS.md` por número de regla.

---

## Índice

> Un ítem por sección de diseño real. No hay un número fijo de secciones de diseño — las
> secciones 5+ se nombran según lo que este spec decida (p. ej. "Diseño: capa de
> sincronización", "Diseño: plantillas"), no según un molde genérico.

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: `<tema 1>`](#5-diseño-tema-1)
6. [Riesgos y deuda](#6-riesgos-y-deuda)
7. [Criterios de aceptación](#7-criterios-de-aceptación)

---

## 1. Por qué existe este spec

> El problema real, con sus síntomas. Si viene de un reporte de cliente o del dueño del
> proyecto, decilo tal cual — es evidencia, no relleno.

## 2. Hallazgos verificados

> **Todo lo que sigue se comprobó ejecutando algo contra este repositorio**, no por
> suposición: leer código, correr una consulta SQL, un `grep`, un comando. Cada hallazgo cita
> archivo y línea, o el comando exacto y su salida.
>
> **Si este spec es el registro de una fase ya terminada** (documento archivado, según la
> regla de `docs/DOCUMENTACION.md` sec. 4), esta sección y la sec. 3 pueden no aplicar — no
> hay hallazgo que verificar ni decisión que fijar sobre trabajo ya hecho. Omitilas y decilo
> en el encabezado, como hace `docs/planes/fase-3/spec.md`.

### 2.1 `<título del hallazgo>`

> Ejemplo real, de `docs/planes/reestructura-documentacion/spec.md` sec. 2.9:
>
> | Adelanto | Dónde vive | Contrato de caducidad declarado |
> |---|---|---|
> | Recepción pre-Purchasing (ADR-0026) | Contrato declarado en `docs/adr/0026-...md:456` y `docs/ROADMAP.md:167`. Campos en código: `SupplierLabel`, `InvoiceReference`, `ReceivedAt` (`InventoryItem.cs:186-234`) | ADR/ROADMAP: "deprecado cuando llegue Purchasing (Fase 4)". El código no repite esa frase: el comentario de `SupplierLabel` dice *"Will become an FK to suppliers in Fase 4 (Purchasing)..."* |
>
> Cita archivo y línea, no "en algún lugar del módulo de inventario". **Y no le atribuyas al
> código una frase que sólo vive en un ADR o en el ROADMAP** — verificá cada cita abriendo el
> archivo, no copiándola de un brief.

## 3. Decisiones fijadas

> Cada decisión es una elección tomada, no una opción a evaluar. Marcá las que consultaste
> con el dueño del proyecto. Numeralas `D1`, `D2`, ... — el resto del documento y de `plan.md`
> las cita por número.

- **D1 — `<decisión en una frase>`.**
  *(Decidido por el dueño: "&lt;cita literal, si la hay&gt;".)* Justificación. Alternativas
  evaluadas y por qué se descartaron, si las hubo.

## 4. Alcance

> **Si este spec es una fase futura, no iniciada, sin ADR ni código propio** (p. ej.
> `docs/planes/fase-4/spec.md`, `docs/planes/fase-5/spec.md`), esta sección y las secs. 5
> ("Diseño"), 6 ("Riesgos y deuda") y 7 ("Criterios de aceptación") no aplican — no hay
> alcance que fijar, diseño que documentar, riesgo real que mitigar ni criterio verificable
> sobre trabajo que todavía no existe. Rellenarlas sería inventar un plan de mentira.
> Omitilas y decilo en el encabezado, como hacen esos dos ejemplos: en su lugar llevan
> "Objetivo y criterio de salida" y "Preguntas abiertas antes de planificar" — las únicas
> secciones que se pueden escribir honestamente sin ADR ni código.

### Entra

- Lista concreta de lo que este spec cubre.

### No entra

> Tan importante como "Entra". Explicá por qué algo cercano queda afuera — evita que otro
> agente lo interprete como olvido. Ejemplo real, `reestructura-documentacion/spec.md` sec. 4:
> *"Cualquier cambio de código que no sea un comentario reapuntado. En particular, la
> normalización de unidades de inventario va en su propia rama,
> `feature/inventory-unit-normalisation`."*

## 5. Diseño: `<tema 1>`

> Una sección por decisión de diseño no trivial. El título real reemplaza `<tema 1>` — nunca
> queda literalmente "Diseño 1", "Diseño 2".

## 6. Riesgos y deuda

> Tabla de dos columnas: riesgo concreto y su mitigación concreta. Si esta rama crea deuda
> deliberada, declarala acá y decí en qué documento queda anotada (normalmente
> `docs/BACKLOG.md`).

| Riesgo | Mitigación |
|---|---|
| `<riesgo>` | `<mitigación>` |

## 7. Criterios de aceptación

> Lista numerada, cada uno verificable por comando o por lectura directa — no por opinión.
> Ejemplo real, `reestructura-documentacion/spec.md` sec. 14, criterio 3:
> *"`docs/plantillas/` tiene las seis plantillas, cada una con instrucciones y ejemplo."*

1. `<criterio verificable>`.
