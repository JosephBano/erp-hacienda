# ADR-0020 — Estado real de Fase 3.5 al cierre del barrido P0/P1 + 3.5a.5 + 3.5a.7

- **Estado:** Reemplazado por ADR-0021
- **Fecha:** 2026-08-07
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina

> **Este ADR quedó reemplazado por [ADR-0021](./0021-cierre-retroactivo-compuerta-3-5a-9-B.md)
> el 2026-08-07**, al aceptar retroactivamente el merge de los PRs #51 (`bc3d315`) y
> #53 (`57cf5a3`) que trajeron 3.5a.9-B a develop. El cuerpo de este ADR se conserva
> intacto como historial: la sec."Decisión" punto 4 reflejaba el estado del repo
> **antes** del descubrimiento de la contradicción con los merges.

## Contexto

Al cierre del barrido integral del 2026-08-07 (los ocho BLOQUE A1→E + ADR), develop
deja de estar rojo: `dotnet format --verify-no-changes` y `dotnet build` pasan;
los tests unitarios del backend (Livestock, Breeding, Inventory, People, Production,
Tasks) están al 100%. Los tests de integración no los pude correr localmente (Docker
del sandbox no soporta veth pairs), así que la verificación end-to-end queda en CI.

Este ADR registra **qué quedó dentro** del barrido, **qué se dejó fuera a propósito**
y **con qué condiciones de reversa**, para que la próxima persona que mire este
código no se pregunte si los huecos son deuda o decisión.

## Decisión

El barrido integral cierra **diez defectos** del informe post-mortem de Fase 3.5
(los P0-1, P0-2, P0-3, P0-4, P0-5 y P1-1, P1-2, P1-3, P1-5, P1-6) y entrega **3.5a.5**
y la tarea 6 de **3.5a.7** (ficha del lote). Quedan explícitamente fuera del alcance:

1. **3.5a.2 (treatment detail)** — sin tocar. El cliente pidió "llegar hasta 3.5a.5 y
   3.5a.7". El plan original dice que sin esta rama no se cumple el criterio de
   salida de 3.5a (registrar tratamientos con vía y motivo); la decisión de parar
   acá es consciente, no por accidente. Si el cliente decide que necesita
   tratamientos con vía antes del piloto, esta rama se reactiva con su propio
   sub-plan (3.5a.2-A/B/C ya están escritos en `docs/spec/sub_planes/`).
2. **3.5a.6 (plausibility ranges)** — la tarea 1 de 3.5a.7 (pesaje muestral del
   lote) requiere rangos de plausibilidad por especie/categoría; sin 3.5a.6,
   validar el pesaje sería un `if` hardcoded por especie (violación del Art. 8).
   Se difiere el pesaje muestral hasta que 3.5a.6 exista. La ficha del lote muestra
   "último pesaje" cuando existe, vacío si no.
3. **3.5a.4 task 4 (clasificación por peso)** — sin tocar. Sigue siendo la pieza
   que cierra el criterio de salida de 3.5a ("seguir la camada hasta su
   clasificación por peso"). Sin 3.5a.2 (vía/motivo), sin 3.5a.4.4 (clasificación)
   y sin UI para casi nada de esto, el criterio de salida **es inalcanzable hoy**
   y el piloto no se puede abrir contra el sistema. Esto está escrito en
   `BACKLOG.md` (sección pendiente de actualizar) y en `ROADMAP.md` al cerrar
   3.5a — si reabrís 3.5a, este ADR es el primer lugar donde leer qué se hizo
   y qué no.
4. **3.5a.9-B (activity tree con sujeto como primer nivel)** — sin tocar. El plan
   exige (sec.2.3 y sec.7-C del macro plan) que la compuerta "árbol dibujado y
   toques contados con el cliente" se cierre **antes** de escribir cualquier
   pantalla de 3.5a.7. No hay evidencia de que esa compuerta se haya cerrado
   con el cliente en este repositorio. Cualquier UI nueva de 3.5a.7 que dependa
   del árbol se queda para entonces. El endpoint `/api/v1/animal-groups/{id}/summary`
   sí se entrega porque es read-side y no navega.
5. **UI admin-web para Species, InventoryItem, MortalityCause** — no hay pantalla
   dedicada para ninguno de estos catálogos hoy en `clients/admin-web/`. El
   endpoint existe y es alcanzable por curl / swagger; las pantallas se agregan
   en un PR aparte cuando se prioricen.
6. **Sub-rama de corrección cross-module para `recordBirth` y `recordMilking`**
   — HoyScreen sólo muestra el botón "Corregir" para `recordAnimalEvent`. Las
   otras operaciones del móvil se redirigen al panel de admin-web para corregir.
   El servidor responde con un mensaje claro cuando alguien fuerza un kind no
   soportado ("La corrección de partos desde el teléfono se habilita en 3.5b").

## Alternativas consideradas

- **Hacer todo 3.5a de una sola pasada (3.5a.0–3.5a.9)**. Descartada por
  duración: la rama 3.5a.2 sola son tres sub-ramas con 11 tareas, y la
  compuerta sec.2.3 (árbol con el cliente) es una conversación, no un commit.
  Forzar la pasada entera habría producido PRs de cientos de archivos sin
  posibilidad de revisar con criterio.
- **No tocar nada hasta tener el árbol con el cliente**. Descartada porque
  develop llevaba cuatro merges fallidos de CI y los features "cerrados"
  (3.5a.1, 3.5a.3, 3.5a.4) tenían bugs que perdían datos en producción
  (peso al nacer, DisposedAt, parámetros de lactancia inaccesibles). Era
  peor dejar el código en ese estado que entrar a arreglarlo.
- **Tocar 3.5a.9-B "como estaba".** Descartada: el plan es explícito en que
  la compuerta del árbol bloquea la rama; respetarla es el precio de
  coherencia.

## Consecuencias

- **Positivas**:
  - develop vuelve a estar verde; los cuatro merges fallidos de CI están
    documentados y arreglados.
  - Los cinco features que perdieron datos en producción (peso al nacer,
    DisposedAt, parámetros de lactancia, LiveHeadCount, JSON de corrección)
    ya pierden datos en silencio.
  - 3.5a.5 (unit conversions) está completa y testeada: el "bug del saco"
    deja de ser un problema.
  - 3.5a.7 tarea 6 (ficha del lote) está lista para que el móvil la consuma
    cuando se cierre la compuerta del árbol.
- **Negativas / costos**:
  - El criterio de salida de 3.5a sigue inalcanzable (3.5a.2, 3.5a.4.4 y UI).
    El piloto real no se puede abrir contra el sistema hoy. Esto es honesto
    y debe estar en el ROADMAP al cierre de 3.5a.
  - Las pantallas admin-web de catálogos (Species, MortalityCause, InventoryItem)
    no existen. Sin panel, las correcciones de partos/ordeños que el móvil no
    soporta requieren un script o curl. Se documenta en BACKLOG.
  - La carrera cancelar↔push del móvil no está probada del lado JS — la prueba
    .NET cubre la invariante del servidor (un clientOperationId produce un solo
    registro bajo cualquier orden de llegada). La mitigación WatermelonDB de
    `outbox.ts:189–202` queda sin test en su propio lado.
- **Condición de reversa**: este ADR se reabre si (a) el cliente decide
  reanudar el piloto y pide reactivar 3.5a.2 + 3.5a.4.4 + UI, o (b) un nuevo
  defecto aparece en código que este barrido tocó (los tests de integración
  son la red que debería detectarlo).

## Lo que hay en cada commit

| Commit | BLOQUE | Qué arregla / agrega |
|---|---|---|
| `0551eeb` | A1 | P0-1 whitespace + P1-1 weanedCount=BornAlive |
| `edb4b66` | A2 | P0-2 LiveHeadCount negativo + P1-6 CHECK XOR cubre las dos mitades |
| `0ab13f7` | B1 | P0-3 peso al nacer end-to-end |
| `22abf8c` | B2 | P0-5 DisposedAt en baja individual |
| `226805c` | B3 | P0-4 parámetros de lactancia configurables |
| `ec0774e` | C  | P1-2 JSON seguro, P1-3 filtro, P1-5 causa en grupo, P1-4 carrera |
| `f807b0d` | D  | 3.5a.5 unit conversions + GroupFeedConsumption con ambas cantidades |
| `6eef997` | E  | 3.5a.7 task 6: ficha del lote |
| (este ADR) | —   | documenta qué entra y qué queda fuera |