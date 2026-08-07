# BACKLOG — ideas, deuda técnica, ítems no bloqueantes

> Este archivo es el parking lot del proyecto. Lo que no entra en un PR porque no es
> bloqueante, o lo que descubrimos mientras hacemos otra cosa, se anota acá.
> Un ítem del BACKLOG no es una tarea: es algo a discutir antes de actuar.

## Pendiente post-barrido 2026-08-07 (ADR-0020)

> El barrido integral cerró los P0/P1 del informe post-mortem y entregó 3.5a.5
> y la tarea 6 de 3.5a.7. Lo que queda fuera del barrido, documentado en
> `docs/adr/0020-…md`, es lo siguiente:

### [3.5a] Reactivar el criterio de salida — sin esto no hay piloto

- **3.5a.2 (treatment detail)** — sub-ramas A/B/C ya escritas en
  `docs/planes/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-{A,B,C}.md`. Sin esta rama
  no se puede registrar un tratamiento con vía y motivo (lo que el cliente
  pidió explícitamente).
- **3.5a.4 task 4 (clasificación por peso)** — sin esto, los lechones no se
  reparten en lotes de engorde por tamaño y la camada deja de seguirse dentro
  del sistema. Pieza que cierra el criterio de salida de 3.5a.
- **3.5a.6 (plausibility ranges)** — sin esta rama, la tarea 1 de 3.5a.7
  (pesaje muestral) no se puede entregar sin un `if` por especie (Art. 8).
- **3.5a.7 tasks 1, 2, 3, 4, 5 (UI del móvil)** — bloqueadas por la compuerta
  sec.2.3 del macro plan: árbol de actividades dibujado y toques contados con
  el cliente antes de escribir cualquier pantalla. Sin esa conversación previa,
  la UI nueva de lote es un menú de botones y la promesa de los 3 toques muere.
- **Disparador**: cuando el cliente pida abrir el piloto real contra el sistema,
  abrir este ADR-0020 y arrancar por 3.5a.2-A. La cadena (3.5a.2 → 3.5a.6 →
  3.5a.7 tasks 1–5) es el orden mínimo que cumple el criterio de salida.

### [UI] admin-web: pantallas de catálogos que faltan

- **Pantalla de especies** (`clients/admin-web/`). El backend tiene CRUD
  completo (incluido `UpdateLactation` que se agregó en este barrido); la UI
  no tiene un componente dedicado para gestionarlas. El operador edita
  parámetros de lactancia con curl hoy.
- **Pantalla de causas de mortalidad** (anotada previamente). El endpoint
  existe; falta el componente Angular.
- **Pantalla de ítems de inventario + conversiones de unidad**. El endpoint
  existe; falta el componente Angular.
- **Disparador**: cuando se priorice trabajo de UI admin-web, agrupar las tres
  en una pantalla genérica "Catálogos" que reutilice un componente tabla
  parametrizable.

### [mobile] Carrera cancelar↔push — la prueba del lado JS

- **Archivo**: `clients/field-app/src/services/outbox.ts:189–202` (mitigación
  WatermelonDB). El servidor garantiza que un `clientOperationId` produce un
  único registro (test `SyncOutboxRaceTests` del barrido 2026-08-07), pero el
  lado JS que decide "pending → cancelled atómicamente" no tiene test propio.
- **Disparador**: cuando los 6 tests UI skipped de MilkingScreen y
  AnimalEditScreen vuelvan a correr (BACKLOG original §tests UI), agregar
  el de outbox al mismo barrido.

### [mobile] UI para corrección cross-module

- **Archivo**: `clients/field-app/src/screens/TodayScreen.tsx:158`. Hoy el
  botón "Corregir" sólo aparece para `recordAnimalEvent`; las entradas de
  `recordBirth` y `recordMilking` quedan con corrección sólo desde el panel
  (decision BLOQUE C). Cuando se implemente la lectura cross-module en 3.5b,
  este filtro se levanta.

## Ítems abiertos (post-piloto Fase 3)

### [cosmético] Regenerar `adaptive-icon.png` con canal alfa

- **Archivo actual**: `clients/field-app/assets/adaptive-icon.png` (4556 bytes, 1024x1024,
  PNG 8-bit RGB sin alfa).
- **Problema**: en Android 8+ el adaptive icon compone el foreground sobre el
  `backgroundColor` con una máscara. Sin alfa, las esquinas del PNG se ven como un
  cuadrado recortado en lugar de integrarse con el fondo.
- **Solución correcta**: regenerar el icono con alfa desde un PNG maestro (margen seguro
  ~33% del lado exterior). Requiere software gráfico (GIMP, Figma, Aseprite) o un
  diseñador.
- **No bloquea el piloto**: la app abre, registra y sincroniza igual. Es puramente
  cosmético.
- **Owner**: humano (la IA puede sugerir comandos pero no puede diseñar el logo).

### [tests UI] Rehabilitar los 6 tests skipped tras el SDK upgrade

- **Archivos**: `clients/field-app/tests/MilkingScreen.test.tsx` (3) y
  `tests/AnimalEditScreen.test.tsx` (3).
- **Causa**: combinación React 19 + @testing-library/react-native@14 async render() +
  WatermelonDB LokiJSAdapter sin cleanup explícito entre tests. Síntomas: "Unable to find
  an element with testID" tras un `fireEvent.press`, y `daily-total` queda en `0 L` en
  lugar del valor esperado.
- **Cobertura alternativa**: las 12 suites `logic/*.test.ts` ejercen los mismos flujos
  (recordMilking, updateAnimal, herdQueries, syncEngine) contra el esquema real de
  WatermelonDB. Los tests UI son nice-to-have, no cobertura única.
- **Trabajo a hacer**: agregar `afterEach(async () => cleanup())` que sí espere el
  render async + drop explícito de IndexedDB antes del próximo test (o usar
  `fake-indexeddb` con reset). El tester ya intentó `setupFilesAfterEach` y no fue
  suficiente; el problema es LokiJSAdapter que retiene state entre tests.
- **Disparador**: cuando WatermelonDB publique una versión con cleanup nativo entre
  adapters, o cuando se decida migrar los tests UI a `@testing-library/react-native`
  `screen.concurrent` patterns.

### [deuda] `--legacy-peer-deps` en field-app-ci

- **Archivo**: `.github/workflows/ci.yml:55`.
- **Causa**: `@nozbe/with-observables@1.6.0` declara peer `@types/react ^16||^17||^18`.
  El proyecto está en React 19 desde el SDK upgrade, así que `npm ci` falla ERESOLVE.
  El flag es informativo; los tipos son estructuralmente compatibles en runtime.
- **Workaround actual**: `npm ci --legacy-peer-deps` en CI.
- **Solución correcta**: pinear `@nozbe/with-observables` a una versión que soporte
  React 19 cuando WatermelonDB lo publique, o migrar a un wrapper casero fino sobre
  `with-observables` (bajo costo si el patrón es estable). Volver a `npm ci` sin flag
  cuando se resuelva.
- **Riesgo de no actuar**: ninguno operacional. Es deuda visible en el workflow.

### [deuda] `AnimalEvent` grupal aún no viaja en el pull (3.5a.1)

- **Archivos**: `src/Hato.Api/Sync/SyncPullQueries.cs`, `clients/field-app/src/services/syncEngine.ts`.
- **Causa**: 3.5a.1 agrega el mecanismo de evento grupal (push, dominio, CHECK de BD) pero
  ningún flujo del móvil todavía necesita leer el historial de eventos de un lote — la
  compuerta de `PLAN-FASE-3-5-PORCINO.md` sec.2.3 bloquea las pantallas nuevas hasta que
  el árbol de actividades esté cerrado con el cliente (3.5a.7/3.5a.9-B). Agregar una
  colección `groupEvents`/`animalEvents` al pull ahora sería construir sin consumidor.
- **Trabajo a hacer**: cuando 3.5a.7 escriba la ficha del lote ("última vacunación,
  alimento del período"), agregar `SyncAnimalEventDto` + `ReadAsync` en
  `SyncPullQueries.cs` y su entrada en `TABLE_BY_COLLECTION`.
- **Disparador**: arranque de 3.5a.7 (`feature/field-app-lot-registration`).

### [docs] `mortality_causes` sin pantalla de administración en admin-web (3.5a.3)

- **Archivos**: `src/Hato.Api/Endpoints/MortalityCausesEndpoints.cs` (backend completo:
  crear, listar, desactivar) — ningún componente Angular lo consume todavía.
  Same situation as `Breed`/`AnimalCategory`: ninguno de los catálogos de referencia
  tiene hoy una pantalla de administración dedicada en `admin-web`.
- **Trabajo a hacer**: cuando se construya una pantalla genérica de catálogos (o una
  específica), agregar `mortality_causes` a esa pantalla.
- **Disparador**: cuando el cliente pida ajustar la lista de causas (sec.7-B del plan
  dice que su lista puede diferir de la semilla estándar) y editarla vía API directa
  deje de ser suficiente.

## Reglas para este archivo

- Cada ítem lleva un prefijo `[categoría]` (cosmético / tests / deuda / docs / ops).
- Cada ítem tiene: archivos afectados, causa raíz, trabajo a hacer, disparador.
- Un ítem pasa a un PR solo cuando alguien dice "este sí".
- Lo que ya está en un ADR vigente no se duplica acá — se referencia.