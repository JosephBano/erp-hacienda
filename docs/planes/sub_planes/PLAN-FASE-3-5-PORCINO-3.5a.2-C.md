# PLAN-FASE-3-5-PORCINO-3.5a.2-C.md — Vacunación como camino separado y UI de campo

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` sec.3.5a.2.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5a.2-A (catálogos y payload) y 3.5a.2-B (lógica de dosis y series).
> Depende de ambas mergeadas.

- **Rama Git:** `feature/field-app-treatment-ui`
- **ADRs que la respaldan:** ninguno nuevo (la separación tratamiento/vacunación
  es una decisión de UX respaldada por `PLAN-FASE-3-4` sec.1 y el principio de los
  tres toques).
- **Pares del split:**
  - [`3.5a.2-A`](./PLAN-FASE-3-5-PORCINO-3.5a.2-A.md) (rama `feature/livestock-treatment-catalog`): catálogos y payload — **requerido**.
  - [`3.5a.2-B`](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md) (rama `feature/livestock-treatment-dose-logic`): lógica de dosis y `TreatmentCourse` — **requerido**.
- **Fuente original:** [`spec-3.5a.md` sec.3.5a.2](../fase-3-5/spec-3.5a.md#35a2--featurelivestock-treatment-detail--estructural--split-en-35a2-a-35a2-b-y-35a2-c)

---

## Por qué existe esta sub-rama

La interfaz de "registrar tratamiento" carga con todo a la vez: producto, vía,
motivo, dosis, lote de inventario, observaciones, período de retiro. Esa pantalla
intenta responder a **dos intenciones distintas** que el cliente separa a la
hora de operar:

- "**Voy a aplicar el cronograma de vacunas de hoy**" — vía casi siempre una
  sola, motivo siempre `Scheduled`, dosis casi siempre `PerHead`, lote
  pre-seleccionado por la lista de pendientes.
- "**Tengo un animal enfermo, voy a tratarlo**" — vía y motivo variables,
  dosis casi siempre `Absolute` o `PerWeight`, lote posiblemente nuevo, notas
  casi siempre obligatorias.

Mezclar las dos en una sola pantalla era exactamente el tipo de fricción que la
regla de los tres toques combate: el caso normal (vacunación de cronograma,
diario) debería sumar tres toques, y debería tener **otra pantalla** que la del
caso raro (tratamiento curativo). Forzar ambas en la misma UI agrega pasos al
caso común para cubrir el infrecuente.

Esta sub-rama separa las dos intenciones en dos rutas, mantiene los toques del
camino "vacunación" en tres, y deja al "tratamiento" con la profundidad que
justifica la menor frecuencia.

## Decisiones tomadas en el macro plan y que aplican a esta sub-rama

- **Tres toques para lo normal, cuatro para lo raro** — la regla del macro
  plan sec.2.3 se aplica literal en esta sub-rama: el camino "vacunar" debe
  resolverse en tres toques, el "tratar un animal enfermo" en cuatro.
- **Pantalla de campo: vía y motivo en la misma pasada, sin sumar toques al
  caso normal.** Si la pantalla de tratamiento pide vía y motivo en una pasada
  separada, suma un toque de más. La forma de evitarlo es presentarlos como un
  único campo combinado o como campos opcionales que se auto-rellenan cuando
  aplica. Esta decisión se fija acá porque depende de la UI concreta, no del
  modelo.
- **Sincronización sin red (Art. 9).** Las pantallas deben funcionar en modo
  avión — los catálogos vienen del pull y la cola del outbox ya existe.

## Tareas

> **Esta sub-rama se divide en C.1 y C.2 dentro del mismo PR.** C.1 es la
> plomería: hace que los catálogos (`administration_routes`, `treatment_reasons`,
> `plausibility_ranges`) lleguen al móvil y sean consultables localmente, sin red,
> exactamente como `mortality_causes` ya lo hace. C.2 son las pantallas que
> consumen esos catálogos. **C.1 debe mergear antes que C.2 dentro del mismo
> PR** (regla "un PR = un propósito" de `PLAN-FASE-3-4.md` sec.1.3 — el
> propósito es "la captura de tratamiento/vacunación funciona en el móvil", y
> los dos pedazos sirven a ese propósito juntos). Si la implementación revela
> que C.1 excede el ~medio día de trabajo, se parte en PRs separados y se anota
> en `docs/BACKLOG.md` (mismo patrón de B si lo excede).
>
> El detalle del contrato de plausibilidad vive en [ADR-0022](../../adr/0022-rangos-plausibilidad.md)
> (Propuesto, 2026-08-07); este sub-plan lo invoca, no lo redefine.

### C.1 — Sync local de catálogos

Hace que los catálogos que el servidor ya entrega via pull estén consultables
desde el cliente sin red, en la misma forma que `mortality_causes` (espejo
local en `clients/field-app/src/database/schema.ts:165`, sembrado por
`migrations.ts:142`).

1. **Tablas espejo locales.** El pull ya entrega `administrationRoutes` y
   `treatmentReasons` ([`SyncPullQueries.cs`](../../src/Hato.Api/Sync/SyncPullQueries.cs)
   líneas 240–241, mismas que `mortalityCauses` en línea 238) detrás del permiso
   `LivestockAnimalsRead`. Las tablas espejo en
   [`schema.ts`](../../clients/field-app/src/database/schema.ts) **no existen
   todavía** — los datos del pull se reciben pero no tienen dónde quedar. Esta
   tarea agrega las dos tablas espejo (mismas columnas que las del servidor,
   más `is_deleted` para lápidas como el resto) y su migración local
   correspondiente. Patrón a copiar: la tabla `mortality_causes` ya en
   `schema.ts:165`. La colección del pull no cambia.
2. **Tabla espejo `plausibility_ranges`** (parte de [3.5a.6](../../adr/0022-rangos-plausibilidad.md),
   gated por ADR-0022). Espejo local de la tabla nueva del servidor, con su
   migración local y su entrada en la colección del pull (a agregar a
   `RequiredPermissionByCollection` y a `SyncCollectionsDto` siguiendo el mismo
   patrón que `mortalityCauses`). **Gating:** si 3.5a.6 aún no mergeó, esta
   tarea queda pendiente y la UI de C.2 muestra un placeholder "rango no
   configurado" sin bloquear — comportamiento fail-open ya definido por
   ADR-0022 sec.3.
3. **Servicios de lectura local.** Funciones que las pantallas (C.2) consumen
   sin red, leídas desde la tabla espejo: `getAdministrationRoutes()`,
   `getTreatmentReasons()`, `getPlausibilityRanges()`. Las tres siguen el
   mismo patrón de `getMortalityCauses()` (consulta local; sin `await fetch`).
   Test unitario: el catálogo llega al espejo tras un pull con una fila nueva
   en el servidor.
4. **Validación local de plausibilidad.** Implementa el contrato del
   [ADR-0022](../../adr/0022-rangos-plausibilidad.md) sec.2: dado un
   `(species_id, category_id, magnitude, valor)`, devuelve uno de
   `{pass, confirm, block}`. La función es **fail-open**: si no hay fila
   para la combinación, devuelve `pass`. Si `plausibility_ranges` no existe
   todavía (3.5a.6 pendiente), devuelve `pass` para todas las magnitudes — sin
   necesidad de un flag ni un `if` que pregunte si la tabla existe. Test
   obligatorio: el caso fail-open no es un caso especial, es el caso base.

### C.2 — UI de campo

Pantallas que consumen los catálogos locales de C.1. Requieren C.1 mergeado
— la rama no compila sin las tablas espejo, así que el orden es mandatorio.

5. **Pantalla `VaccinateScreen` en `field-app`.** Camino principal del
   registro de vacunación:
   - Lote o sujeto (animal / lote) en el primer toque, vía búsqueda o recientes
     (lo provee [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md), que ya debió
     haber mergeado para llegar acá; si no, este PR no arranca).
   - Selección del producto desde el inventario en el segundo toque.
   - Motivo `Scheduled` prefijado, lote de inventario pegado al producto,
     `DoseKind = PerHead` por defecto, vía y cantidad se completan con la
     indicación del producto. Vía y motivo vienen de las listas locales de
     C.1 (`getAdministrationRoutes()`, `getTreatmentReasons()`). **Tercer
     toque: confirmar.**
   - Total: **tres toques** para el caso común.
6. **Pantalla `TreatScreen` en `field-app`.** Camino "curar / prevenir", más
   profundo:
   - Sujeto (animal individual, **no** grupo en esta pantalla inicial), luego
     producto, luego **un único formulario** con vía + motivo + dosis + lote.
     Los defaults razonables (motivo `Curative`, dosis `Absolute`) se sugieren
     pero son editables. Las listas de vía y motivo vienen de C.1.
   - Notas **siempre visibles** y obligatorias sólo cuando `AdministeredDose`
     sea `NULL` (lo decide 3.5a.2-B; la UI sólo refleja la regla).
   - Total: **cuatro toques** para el caso raro (sujeto, producto, formulario,
     confirmar).
7. **Validación local de plausibilidad integrada en las pantallas.** Las dos
   pantallas llaman al servicio de C.1 tarea 4 antes de encolar al outbox:
   - `pass` → encolar.
   - `confirm` → mostrar diálogo de confirmación del ADR-0022 sec.5; si el
     operario confirma, encolar con `is_plausibility_confirmed = true` en el
     payload.
   - `block` → mostrar mensaje de rechazo y no encolar.
   Esta tarea es la integración entre C.1 (servicio) y C.2 (pantalla); sin
   ella, el servicio de C.1 existe pero nadie lo llama. Si 3.5a.6 no
   mergeó, el servicio devuelve `pass` y la pantalla no muestra diálogos —
   comportamiento fail-open explícito.
8. **Camino "Lo que registré hoy":** las dos pantallas empujan sus
   operaciones al `SyncOutbox` con tipo distinto (`vaccination` vs `treatment`)
   para que la lista del día pueda filtrarlas. El estado `cancelled` ya
   existe en `outbox.ts:6` (ADR-0017 ya entregado al nivel de tipos). Este
   sub-cambio se conecta con
   [3.5a.8](../fase-3-5/spec-3.5a.md#35a8--featurefield-app-corrections-adr-0017)
   cuando exista (correcciones; es opcional acá).
9. **`TreatmentCourse` se refleja en la UI como una sola fila de la lista
   del día**, no como N eventos sueltos (decisión de
   [3.5a.2-B](./PLAN-FASE-3-5-PORCINO-3.5a.2-B.md#tareas)); la pantalla hoy no
   lo distingue y eso está bien como primer paso, pero se documenta la
   dirección: en una iteración posterior la lista del día debe agrupar
   aplicaciones del mismo `TreatmentCourse`.
10. **Caminos de salida y "cancelar" explícitos** — si la pantalla se cierra a
    mitad, la cola queda limpia, no queda un estado sucio en
    `VaccinateScreen`/`TreatScreen`. Mismo patrón que
    [`3.5a.0`](../fase-3-5/spec-3.5a.md#35a0--featurefield-app-input-guards--empezar-por-acá)
    fijó para `BirthScreen`.
11. **Pruebas críticas** (detalladas más abajo).

### Tarea derivada: conector con la rama de búsqueda QR / RFID

Cuando el aretado llegue (ADR-0015), un input de búsqueda por RFID acelerará
los dos flujos al primer toque. Esta sub-rama deja la pantalla **lista para**
recibir un input inicial distinto — un `picker` que pueda ser `search` o
`scan`. No se implementa acá.

## Pruebas

Las pruebas mínimas obligatorias, agrupadas por sub-rama:

### Cobertura de C.1 (sync local)

1. **Espejos locales presentes.** El pull entrega `administrationRoutes` y
   `treatmentReasons`; tras un pull, las dos tablas espejo en el cliente
   tienen las filas con `is_deleted` propagado. Misma cobertura que ya
   existe para `mortality_causes`.
2. **Espejo de `plausibility_ranges` presente.** Si 3.5a.6 ya mergeó, el pull
   entrega la nueva colección y la tabla espejo local la recibe. Si 3.5a.6
   aún no mergeó, la colección no aparece en el pull y la UI no se rompe.
3. **Servicio de plausibilidad fail-open.** Una combinación sin fila en
   `plausibility_ranges` devuelve `pass`. No es un caso especial del test;
   es el caso base. La función no tiene rama para "tabla no existe": el
   catálogo se lee y, si está vacío, devuelve `pass`. Si 3.5a.6 no
   mergeó, la tabla espejo no existe y la lectura devuelve `pass` igual.

### Cobertura de C.2 (UI)

4. **Tres toques para vacunación individual.** Sembrar 1 animal con su arete.
   Recorrer `VaccinateScreen` con el formulario completo: el conteo de
   toques observables en el árbol de navegación **debe ser exactamente 3**.
   Si la implementación excede, la prueba falla y la pantalla se corrige
   antes de mergear. Esta es la misma defensa del
   [3.5a.9-B](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md), pero específica de esta
   pantalla.
5. **Cuatro toques para tratamiento curativo.** Recorrer `TreatScreen`
   completo con todos los campos llenos. Conteo exacto = 4.
6. **Sin red total.** Modo avión, recorrer las dos pantallas hasta el
   outbox. La operación queda pendiente con su tipo (`vaccination` /
   `treatment`). Verificar que la cola se reconstruye tras reiniciar la app
   y que las dos pantallas resuelven vía/motivo desde las tablas espejo
   locales.
7. **Plausibilidad end-to-end.** Con rangos configurados, valores
   improbables piden confirmación (y la confirmación persiste en el payload
   como `is_plausibility_confirmed = true`) y valores imposibles se
   rechazan sin entrar al outbox. Sin rangos configurados, valores absurdos
   pasan (fail-open del ADR-0022).
8. **Validación local de admin route inválida.** Intentar enviar un evento
   con `route_id` que no exista o esté `is_active = false` — la app debe
   detectar el problema en el outbox antes de mandar (cliente problema
   visible) y el servidor lo rechaza igual si llegara.
9. **Cancelar no deja estado sucio.** Iniciar una vacunación, cerrarla
   después de seleccionar producto. Reabrir: la pantalla debe arrancar en
   estado limpio (sin sujeto, sin producto preseleccionado).

Adicional recomendado:

- Recorrido por voz / TalkBack: la separación debe leerse correctamente sin
  pista visual.
- Modo oscuro y alto contraste de las dos pantallas.
- Cobertura del flujo de confirmación: tras una confirmación del operario,
  el evento registrado conserva `is_plausibility_confirmed = true` en su
  payload, recuperable después por la UI de "lo que registré hoy".

## Lo que NO incluye (queda para otras ramas)

- Las pantallas de **corrección** sobre tratamientos ya registrados — es
  [`3.5a.8`](../fase-3-5/spec-3.5a.md#35a8--featurefield-app-corrections-adr-0017).
- La **ficha del lote** con el último tratamiento aplicado — eso es
  [`3.5a.7`](../fase-3-5/spec-3.5a.md#35a7--featurefield-app-lot-registration).
- El escaneo **QR/RFID**. La estructura de la pantalla queda lista, pero la
  integración va cuando llegue el aretado.
- **El árbol de actividades completo** que ubica estas pantallas en su
  lugar → [`3.5a.9-B`](./PLAN-FASE-3-5-PORCINO-3.5a.9-B.md).

## Cómo probarlo

```bash
git fetch origin
git switch feature/field-app-treatment-ui
cd clients/field-app && npm run typecheck && npm test
```

Para el flujo manual con la app levantada:

1. Sembrar un animal con arete en el pull.
2. Abrir la app, ir a **Un animal → Vacunar**. Confirmar tres toques para el
   caso común.
3. Ir a **Un animal → Tratar**. Confirmar cuatro toques para el caso raro.
4. Poner modo avión, repetir ambos flujos. Verificar que la cola del outbox
   los persiste con tipo `vaccination` y `treatment` respectivamente.

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| "Vacunar" termina exigiendo más de tres toques | Test #4 obligatorio. Si se excede, se rediseña la pantalla. |
| "Tratar" oculta campos del formulario | El modo `Absolute` con notas se prueba explícitamente; un bug acá se descubre en la primera inspección. |
| El outbox marca ambas operaciones como el mismo tipo | Forzar tipos distintos en `outbox.ts` (test específico). |
| Cancelar deja el sujeto/producto pre-seleccionado | Test #9 obligatorio. |
| La UI ignora `is_active = false` del catálogo local | Sincronización respeta `is_active`; test #8 captura la ruta. |
| C.1 (sync local) termina ocupando más de medio día de trabajo | Se parte en un PR aparte y se anota en `docs/BACKLOG.md` (mismo patrón que B si excede). C.2 no arranca sin C.1 mergeado. |
| La tabla espejo local no existe y el pull entrega datos huérfanos | Cobertura del test #1 explícita: las dos tablas espejo deben existir y la migración local debe preceder al primer pull que las use. |
| El servicio de plausibilidad se implementa con un `if (tabla existe)` en vez de fail-open limpio | Test #3 lo fija como caso base; cualquier rama explícita "tabla no existe" se rechaza en review. |
