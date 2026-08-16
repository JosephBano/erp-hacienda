# BACKLOG — deuda técnica e ideas fuera de fase

> Este archivo fusiona el parking lot de deuda técnica por sub-rama con las ideas que
> (regla ROADMAP.md, "Reglas anti-estancamiento" #2) no entran en la fase en curso. Un
> ítem del BACKLOG no es una tarea: es algo a discutir antes de actuar. Las ideas se
> revisan al cerrar cada fase — algunas suben a la fase siguiente, otras se descartan
> con una línea explicando por qué.

## Deuda abierta por sub-rama

## Pendiente 3.5a.2-C — sub-rama cerrada, ítems abiertos

> La sub-rama 3.5a.2-C mergeó a `integration/fase-3-5-wave-1` el 2026-08-09
> (commit `5a54e1d` → `90ca99b` vía `feature/field-app-treatment-ui`,
> `TreatScreen` + `VaccinateScreen` + sync pull). Estos dos ítems son mejoras
> de UX que quedaron abiertas a propósito — ninguna es bloqueante para el
> criterio de salida de 3.5a.2-C.

### [mobile] `VaccinateScreen` asume la primera vía activa en vez de la del producto

`InventoryItem` no declara todavía una vía de administración preferida por
producto — ese campo pertenece al módulo Inventory (Art. 6, fuera de alcance
de esta sub-rama). Mientras tanto `VaccinateScreen` (3.5a.2-C) toma la primera
fila activa de `administration_routes` como default, no editable, para no
sumar un cuarto toque. Cuando Inventory agregue "vía preferida" al ítem, este
default debería leerlo de ahí en vez de la primera fila del catálogo.

### [mobile] `AnimalSubjectScreen` no pre-selecciona el animal al abrir `TreatScreen`

El botón "Tratamiento (animal enfermo)" de `AnimalSubjectScreen` sigue
enrutando a `TreatScreen`, pero `TreatScreen` es autocontenida (tiene su
propio picker de animal, necesario para que su prueba de "cuatro toques" sea
aislable) y no acepta todavía un `initialAnimalId` como sí hace
`EventsScreen`. El operario que llega por ese camino re-selecciona el animal
que ya había elegido — un toque de más en ese camino específico, no en el
camino principal (ActivitiesHub → Tratar/Vacunar) que 3.5a.2-C mide. Agregar
`initialAnimalId` a `TreatScreen`/`VaccinateScreen` (mismo patrón que
`EventsScreen`) cierra esto.

## Pendiente post-barrido 2026-08-07 (ADR-0021)

> El barrido integral cerró los P0/P1 del informe post-mortem y entregó 3.5a.5
> y la tarea 6 de 3.5a.7. Lo que queda fuera del barrido, documentado en
> `docs/adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md`, es lo siguiente:

### [3.5a] Reactivar el criterio de salida — sin esto no hay piloto

- **3.5a.2 (treatment detail)** — sub-ramas A/B/C ya escritas en
  `docs/planes/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-{A,B,C}.md`. **A y B ya
  mergeadas a integración** (A: `feature/livestock-treatment-dose-logic` →
  PR para catalogos y payload; B: parte de la misma ola). **C mergeada
  2026-08-09** vía `feature/field-app-treatment-ui` →
  `integration/fase-3-5-wave-1` (commit `fa668f4`/`90ca99b`). Solo quedan los
  dos ítems de mejora UX listados arriba en este mismo archivo (VaccinateScreen
  default route y AnimalSubjectScreen pre-selección).
- **3.5a.4 task 4 (clasificación por peso)** — sin esto, los lechones no se
  reparten en lotes de engorde por tamaño y la camada deja de seguirse dentro
  del sistema. Pieza que cierra el criterio de salida de 3.5a. Sigue abierto.
- **3.5a.6 (plausibility ranges)** — **mergeada** vía `feature/livestock-plausibility-ranges`
  (PR #73). Sin esta rama, la tarea 1 de 3.5a.7 (pesaje muestral) no se podía
  entregar sin un `if` por especie (Art. 8). CERRADO.
- **3.5a.7 tasks 1, 2, 3, 4, 5 (UI del móvil)** — tasks 1–5 mergeadas a
  integración vía `feature/field-app-lot-registration` (commit `b794c82`).
  Sigue pendiente la conversación de frecuencias con el cliente (sec.7-C,
  documentada abajo en `[docs] Orden de actividades del sujeto "lote"
  pendiente de validar con el operador`).
- **Disparador**: cuando el cliente pida abrir el piloto real contra el sistema,
  abrir [ADR-0024](./docs/adr/0024-pilot-decoupling-from-3-5a.md) y arrancar por
  3.5a.4 task 4. La cadena (3.5a.4 task 4 + sec.7-C) es lo mínimo que queda
  para cerrar el criterio de salida completo de 3.5a.

### [UI] admin-web: pantallas de catálogos que faltan

- **Pantalla de especies** (`clients/admin-web/`). El backend tiene CRUD
  completo (incluido `UpdateLactation` que se agregó en este barrido); la UI
  no tiene un componente dedicado para gestionarlas. El operador edita
  parámetros de lactancia con curl hoy.
- **Pantalla de causas de mortalidad** (anotada previamente). El endpoint
  existe; falta el componente Angular.
- **Pantalla de ítems de inventario + conversiones de unidad**. El endpoint
  existe; falta el componente Angular.
- **Catálogo de etapas de alimento (`feed_stages`, 3.5a.5 task 3)**. Solo
  existe `GET /api/v1/inventory/feed-stages` (listar) — el dominio
  (`FeedStage.Create/Activate/Deactivate`) soporta el ciclo completo pero no
  se expusieron `POST`/`activate`/`deactivate`/label update porque no hay
  panel que los consuma todavía (alcance acotado a la tarea 3 del plan).
  Cuando se construya la pantalla, agregar esos endpoints siguiendo
  `AdministrationRoutesEndpoints.cs` como plantilla exacta.
- **Disparador**: cuando se priorice trabajo de UI admin-web, agrupar las
  cuatro en una pantalla genérica "Catálogos" que reutilice un componente
  tabla parametrizable.

  *(Relacionado: "Cerradas en `feature/admin-web-inventory-detail`" en la
  sección "Ideas fuera de la fase actual" — la pantalla de detalle de
  inventario ya cerró parte de este ítem: `InventoryItem`, `InventoryBatch`,
  `UnitConversion` y `FeedStage` ya tienen UI dedicada; siguen faltando
  especies, causas de mortalidad y el CRUD completo de `feed_stages`.)*

### [UI] Pantalla dedicada de gestión de grupos (animal-groups)

- **Estado (2026-08-08)**: ✅ CERRADO en merge de PR #82 + fix post-review
  #83. Cubre: listar con `LiveHeadCount`/`SpeciesName` server-side (3–4 queries
  agregadas, no N+1), crear, detalle con resumen (5 tarjetas + miembros +
  eventos), edición inline, cambio de `TrackingMode` con guarda (409 si tiene
  actividad), desactivar/reactivar (reversible). Endurecimiento colateral:
  `POST /`, `POST /events`, `POST /members`, `DELETE /members/{id}` también
  exigen `livestock.animals.write` (gap preexistente cerrado). 18 specs vitest
  + 13 integration nuevos.
- **ADR**: `docs/adr/0025-gestion-administrativa-grupos.md`.
- **Plan**: `docs/planes/PLAN-ADMIN-WEB-ANIMAL-GROUPS.md`.
- **Nota**: el disparador original "cuando se priorice trabajo de UI admin-web"
  queda **parcialmente satisfecho** para `animalGroups` pero **no** para los
  catálogos puros (especies, causas de mortalidad, ítems de inventario); esos
  siguen pendientes en el ítem hermano de arriba.
- **Disparador**: N/A — ítem cerrado. El disparador original del ítem "[UI]
  admin-web: pantallas de catálogos que faltan" se mantiene para los catálogos
  que faltan.

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

  *(Relacionado: "3.5a.8 — Corrección de registros desde el teléfono
  (ADR-0017)" en la sección "Ideas fuera de la fase actual" — mismo tema de
  corrección, distinto ángulo: este ítem es sobre qué tipos de evento
  muestran el botón "Corregir"; ese otro es sobre el flujo completo de
  corrección en 3.5a.8.)*

## Pendiente 3.5b — diferido del barrido 2026-08-07

> 3.5b está fuera del alcance del barrido integral del 2026-08-07 (lo deja
> sentado [ADR-0021](./docs/adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md) ·
> que a su vez reemplaza a [ADR-0020](./docs/adr/0020-fase-3-5-estado-al-cierre-del-barrido-p0-p1.md)),
> pero hay **una pieza concreta** que el barrido de 3.5a.2-A dejó sembrada y
> que tiene que cerrarse con la primera rama de 3.5b. Lo siguiente es
> trazabilidad, no trabajo del barrido actual.

### [3.5b.1] Cerrar la FK de `health_plan_item_id` cuando exista `HealthPlan`

- **Estado (2026-08-08)**: ✅ CERRADO en `feature/livestock-health-plans`
  (pendiente de merge a `develop`). La tabla `health_plan_items` existe, la
  migración `20260808055359_AddHealthPlans` agrega el FK constraint sobre la
  columna pre-existente + el índice `i_x_animal_events_health_plan_item_id`,
  `AnimalEventConfiguration.cs` ahora declara el `HasOne<HealthPlanItem>()`
  correspondiente, y `RecordAnimalEventCommand.cs` valida que el id
  referenciado exista y esté activo. La rama además entrega la API CRUD de
  planes (POST /health-plans, /items, /assignments), el sync pull de las tres
  colecciones nuevas y 6 tests (4 integración + 2 sync). El ítem se mantiene
  en este BACKLOG hasta que la rama se mergee como referencia.
- **Archivos**: `animal_events.health_plan_item_id` (columna nullable ya
  creada por la migración `20260807214557_AddAnimalEventTreatmentPayload`, ver
  `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Configurations/AnimalEventConfiguration.cs:39`
  y
  `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Migrations/20260807214557_AddAnimalEventTreatmentPayload.cs:29`)
  — pendiente el `FOREIGN KEY` a `health_plan_items.id` cuando esa tabla exista.
- **Por qué se sembró así**: el plan
  [`3.5a.2-A`](./docs/planes/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md)
  sec."Forward-compat con el cronograma" fija la decisión: si el piloto corre
  un mes registrando tratamientos sin el campo, **esos datos no se pueden
  enlazar retroactivamente** al cronograma cuando aparezca. Cuesta una línea
  crear la columna nullable hoy; es irrecuperable mañana. La presencia del
  campo en la fila (no su integridad referencial) es lo que importa en el
  tramo 3.5a.
- **Trabajo a hacer**: cuando arranque 3.5b.1 (rama
  `feature/livestock-health-plans`, ADR-0016), la migración que crea
  `health_plan_items` debe **agregar el FK constraint** sobre la columna ya
  existente, no crear la columna de nuevo. Riesgo explícito en 3.5a.2-A: que
  alguien cree la columna como FK desde cero, sin notar que ya existe, y la
  migración falle por duplicado. La tarea de 3.5b.1 debe revisar primero el
  snapshot del modelo (`LivestockDbContextModelSnapshot.cs:287`) y la
  migración `20260807214557` antes de proponer el constraint.
- **Disparador**: arranque de 3.5b.1 (rama `feature/livestock-health-plans`,
  ADR-0016). Es la primera tarea de 3.5b según el macro plan sec.4.1.

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

### [mobile] `feed_stages` (y `unit_conversions`) sin viajar en el sync pull (3.5a.5 task 3)

- **Archivos**: `src/Hato.Api/Sync/SyncPullQueries.cs` (`SyncCollectionsDto`,
  `RequiredPermissionByCollection`, `GetSyncPullQueryHandler.Handle`);
  `clients/field-app/src/services/syncEngine.ts` (mapa de colecciones →
  tablas WatermelonDB); `src/Hato.Api/Endpoints/InventoryEndpoints.cs`
  (`GET /api/v1/inventory/feed-stages`, único endpoint hoy).
- **Causa raíz**: el mismo razonamiento que ya dejó afuera `unit_conversions`
  (3.5a.5 tasks 1/2, mergeadas sin entrada en el pull): hoy ningún flujo del
  móvil consume el catálogo. La tarea 4 de 3.5a.5 (registro de consumo en
  sacos) ya está implementada y no necesita `feed_stage` — resuelve la
  conversión saco↔kg, no la clasificación del ítem. `inventory_items` sí
  viaja en el pull (con `feed_stage_id`, si se agrega a `SyncInventoryItemDto`
  cuando corresponda) pero el catálogo de etapas en sí no tiene consumidor
  todavía: no hay pantalla en el móvil que filtre o muestre "preiniciador /
  iniciador / …". Agregar la colección ahora sería construir sin consumidor,
  el mismo criterio que ya se aplicó al backlog de `groupEvents`.
- **Trabajo a hacer**: cuando 3.5a.7 (`feature/field-app-lot-registration`,
  tarea 5 "consumo de alimento del lote en sacos") o cualquier pantalla de
  catálogos del móvil necesite mostrar/filtrar por etapa de alimento,
  agregar `SyncFeedStageDto` (mismo shape que `SyncAdministrationRouteDto`:
  `Id, Key, LabelEs, IsActive, CreatedAt, UpdatedAt, IsDeleted`) +
  `ReadAsync(..., "feedStages", inventoryDb.FeedStages, ...)` +  su entrada
  en `RequiredPermissionByCollection` (sugerido:
  `SystemPermissions.InventoryItemsRead`, el mismo permiso que ya protege
  `inventoryItems`) + la entrada correspondiente en `syncEngine.ts`. De paso,
  evaluar si conviene resolver `unit_conversions` en el mismo PR — comparten
  causa y consumidor futuro.
- **Disparador**: primera pantalla del móvil (o de admin-web con necesidad de
  offline) que necesite listar o filtrar ítems de inventario por etapa de
  alimento.

  *(Relacionado: "Cerradas en `feature/admin-web-inventory-detail`" en la
  sección "Ideas fuera de la fase actual" — esa pantalla ya expone
  `FeedStage`/`UnitConversion` en admin-web; lo que sigue pendiente acá es
  específicamente el viaje por el sync pull hacia el móvil.)*

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

  *(Relacionado: "3.5a.3 — Causas configurables de muerte de lechón" en la
  sección "Ideas fuera de la fase actual" — mismo catálogo `mortality_causes`,
  visto desde dos ángulos: este ítem es sobre la pantalla de administración en
  admin-web; ese otro es sobre exponer la causa al registrar la mortalidad en
  el móvil.)*

### [UI] `ConfirmDialogComponent` reutilizable

- **Causa raíz**: el PR #82 (pantalla de grupos) usa patrón de botón de dos
  pasos para la única acción destructiva (`deactivate`). Si aparece una segunda
  acción destructiva en admin-web, replicar el patrón se vuelve ruido.
- **Trabajo a hacer**: cuando se presente la segunda acción destructiva en
  admin-web, factorizar el patrón a un componente reutilizable con API mínima
  (`open(title, body, confirmLabel, cancelLabel): Observable<boolean>`), en el
  estilo de `IconComponent` / `CatalogTableComponent`.
- **Disparador**: cuando aparezca la segunda acción destructiva en admin-web
  (ej. "Eliminar rol con asignaciones").

### [UI] Icono "grupo/lote" dedicado en `IconComponent`

- **Causa raíz**: el PR #82 usa `'tag'` como placeholder del icono de grupo en
  el sidebar y en la lista, con un TODO. El `IconComponent` no tiene glyph
  dedicado para "grupo/lote".
- **Trabajo a hacer**: cuando un diseñador provea el icono, agregarlo al set
  cerrado de `IconComponent` y reemplazar el placeholder en los componentes de
  animal-groups.
- **Disparador**: cuando un diseñador provea el icono.

### [perf] Índice en `AnimalGroup.IsActive`

- **Causa raíz**: el listado default del backend (`GET /api/v1/animal-groups`)
  filtra por `is_active = true`. La columna no tiene índice. Con <100 grupos
  es invisible; con >500 fincas en producción va a doler.
- **Trabajo a hacer**: migración EF Core que agregue `IX_animal_groups_is_active`
  sobre la columna. Backfill no necesario (es columna booleana). Validar con
  `EXPLAIN ANALYZE` antes y después.
- **Disparador**: deploys con >500 grupos activos.

### [UI] Refactors de mantenibilidad post-#82/#83

- **Causa raíz**: la revisión del PR #83 (fix post-review de la pantalla de
  grupos) dejó cuatro oportunidades de mantenimiento no bloqueantes:
  1. Helper `errorFrom` duplicado en 3 componentes → extraer a
     `shared/error-format.ts`.
  2. Rutas con `permissionGuard` repetido → considerar wrapper o
     `data: { permission: ... }` en `app.routes.ts`.
  3. Sidebar "Administración" label vs sub-items → alinear la condición.
  4. `ApiExceptionHandler` filtra `exception.Message` → evaluar sanitizar
     `Detail` para no filtrar internals.
- **Trabajo a hacer**: agrupar (decisión: agrupar porque los cuatro son de
  mantenibilidad, no de funcionalidad) en un PR único que ataque los cuatro
  con sus tests. Cada uno individualmente es chico; juntos caben en un PR
  sin riesgo.
- **Disparador**: próximo barrido de admin-web, o antes si alguno se cruza con
  otro trabajo de UI.

### [docs] Orden de actividades del sujeto "lote" pendiente de validar con el operador (sec.7-C)

- **Causa raíz**: 3.5a.7 (`feature/field-app-lot-registration`) implementó las seis
  actividades del sujeto "lote" (`LotSubjectScreen.tsx`) sin la conversación con el
  cliente que `PLAN-FASE-3-5-PORCINO.md` sec.2.3/sec.7-C exige para fijar el orden por
  frecuencia real. El orden usado (alimento, pesaje muestral, vacunar, tratar,
  diagnóstico, baja) es el supuesto explícito documentado en el código de
  `LotSubjectScreen.tsx` — "alimento es lo más frecuente" según el propio plan — no una
  medición con el operador parado en el corral.
- **Trabajo a hacer**: cuando el cliente responda sec.7-C, actualizar el orden de los
  seis `BigButton` en `LotSubjectScreen.tsx` (y el de `ActivitiesHub.tsx` si el orden de
  los cuatro sujetos también cambia) en un commit dedicado, con el test de orden
  actualizado a propósito — nunca como un efecto colateral de otro cambio.
- **Disparador**: la sesión con el cliente de sec.7-C (`PLAN-FASE-3-5-PORCINO.md`).

  *(Relacionado: "3.5a.7.1–5 — UI del sujeto \"lote\"" en la sección "Ideas
  fuera de la fase actual" — mismo disparador sec.7-C; este ítem es la
  descripción operativa del reorden pendiente, ese otro es el registro de
  deuda por desacople del piloto vía ADR-0024.)*

## Reglas para este archivo

- Cada ítem lleva un prefijo `[categoría]` (cosmético / tests / deuda / docs / ops).
- Cada ítem tiene: archivos afectados, causa raíz, trabajo a hacer, disparador.
- Un ítem pasa a un PR solo cuando alguien dice "este sí".
- Lo que ya está en un ADR vigente no se duplica acá — se referencia.

## Ideas fuera de la fase actual

## De la Fase 3 (sync + app de campo)

- **Extender el borrado lógico a otras entidades sincronizables.** Hoy sólo `Animal` tiene
  un método `Delete()` real (con la invariante "no eliminar con historia"). `AnimalGroup`,
  `InventoryItem` y las entidades de Breeding tienen la columna `deleted_at` heredada de
  `AuditableEntity` y el filtro de consulta, pero ninguna operación de dominio la asigna.
  Implementar cuando aparezca un caso de uso real (p. ej., "borré un lote por error").

- **Extender LWW a otras entidades editables.** `Animal.Update()` es el único caso —
  el que ADR-0008 nombra explícitamente como ejemplo ("Datos de Animales"). Si
  `AnimalGroup` u otra entidad gana un flujo de edición real, necesita el mismo patrón:
  campo de "última edición declarada" distinto de `UpdatedAt`, más la detección de
  conflicto en el handler.

- **Resolución manual de operaciones rechazadas desde el panel.** La bandeja de
  sincronización (`/sync` en admin-web) hoy es de solo lectura. El plan original mencionaba
  "resolución manual" para el piloto — pero eso requiere un endpoint de reintento
  (`POST /api/v1/sync/operations/{id}/retry` o similar) que no existe: hoy la única forma
  de corregir una operación rechazada es que el empleado la vuelva a registrar. Justificado
  sólo si el piloto real muestra que los rechazos son frecuentes y no triviales de
  re-capturar a mano.

- ~~**`ng test` de `admin-web` está roto**, sin relación con nada de este trabajo:~~
  `app.component.spec.ts` importa un símbolo `App` que no existe (`AppComponent` es el
  nombre real).~~ **Resuelto** el 2026-08-11 por `feature/admin-web-inventory-detail` (PR
  en curso): el spec importa `AppComponent` correctamente y la suite corre verde
  (13 archivos, 68 tests passing). ~~Bajo prioridad — no bloquea CI porque el pipeline
  no corre `ng test` — pero hay que arreglarlo antes de que el panel dependa de esa suite
  para algo real.~~

## De Fase 2 (heredado, seguía pendiente)

- **`Lactation` es un tipo de dominio sin ciclo de vida implementado.** Existe la entidad
  y su tabla, pero ninguna operación llama `Lactation.Start()` — ni al registrar un parto,
  ni al iniciar el ordeño de una vaca nueva. Antes de mostrar "lactancias activas" en
  cualquier panel, hay que decidir el disparador real: ¿se abre automáticamente al parto
  (Breeding) o manualmente al primer ordeño (Production)? Es una decisión de dominio, no
  una casilla de UI.

## De la Fase 3.5 (adaptación porcina) — diferido a propósito

> Lo que salió del levantamiento del 2026-08-05 y **deliberadamente no entra** en 3.5.
> Lo que sí entra está en `PLAN-FASE-3-5-PORCINO.md`; lo que ya quedó decidido en los
> ADR-0015/0016/0017 no se duplica acá.

- **Escaneo QR y carnetización desde el nacimiento.** El cliente lo mencionó como deseable
  ("sería difícil ver código por código el animal") y él mismo lo puso después del aretado:
  primero tiene que funcionar el resto. `AnimalIdentifier` ya soporta el tipo `RFID`
  (ADR-0006) y ADR-0015 hace que pasar un lote a modo `Individual` no requiera migración,
  así que el terreno está preparado. **Disparador:** que el cliente decida aretar, que
  según él ocurre si el FCR demuestra resultados. Mientras tanto, el buscador por
  identificador de 3.5a.9 cubre la necesidad real.

- **Densidad de corral (cabezas/m²).** Necesita el área de cada corral, que es parte de
  `Paddock`/`Grazing` — **Fase 5**. **Disparador:** cuando se abra el módulo de potreros.

- **Consumo de agua por lote.** No lo pidió nadie; se anota porque en porcinos una caída de
  consumo de agua precede a la de alimento como señal de enfermedad. **Disparador:** que la
  alerta de divergencia de consumo de alimento (sec.4.4 del plan) resulte demasiado tardía en
  el piloto.

- **Ambiente del corral (temperatura, humedad).** Requiere sensores que no existen en la
  finca. YAGNI (Art. 17). **Disparador:** que aparezca hardware instalado.

- **Costo por lote y por kg producido, en dinero.** El FCR de 3.5b se calcula **en kg** a
  propósito: está completo sin contabilidad y no adelanta trabajo de otra fase. Convertirlo
  a dinero es **Fase 4**, donde ya está previsto el centro de costo "porcinos".
  **Disparador:** apertura de la Fase 4.

- **Anclas adicionales del plan sanitario** (p. ej. "a los N días del primer celo").
  ADR-0016 fija cuatro anclas (`Nacimiento`, `InicioDeLote`, `Parto`, `Destete`) que cubren
  todo lo levantado. Agregar una quinta es catálogo *más* código en el resolutor de fechas.
  **Disparador:** un ítem real del cronograma del cliente que no se pueda expresar con las
  cuatro.

### Cerradas en `feature/admin-web-inventory-detail` (PR en curso, 2026-08-11)

- **UI admin-web para `InventoryItem`, `InventoryBatch`, `UnitConversion`, `FeedStage`.**
  ADR-0020 lo declaraba como deuda pendiente ("Pantallas de catálogos que faltan"), y
  quedaba como deuda rastreable de "P2/P3 del informe post-mortem". La nueva pantalla
  `/inventory/items/:id` muestra los datos del ítem, los lotes (con creación inline), las
  conversiones de unidad (con creación inline), y — sólo para `Category=Feed` — la etapa
  de alimento. La pestaña `inventory` de `/catalogs` ahora tiene una acción "Detalle"
  que navega ahí. **Estado:** mergeada en este PR; pendiente `BACKLOG.md` se cierra acá.
  **Lo que sigue faltando (deuda explícita, no resuelto en este PR):**
  desactivación/eliminación de lotes (no hay endpoint en el backend), edición del nombre/
  descripción del ítem (sólo lectura hoy), admin CRUD de feed-stages más allá de activar/
  desactivar (no hay pantalla; el panel sólo se conecta vía curl).

### Deuda explícita por desacople del piloto (ADR-0024, 2026-08-08)

> Estas piezas viven como **deuda rastreable** porque el inicio del piloto real no las
> exige como requisito (ver sub-criterio "Para abrir el piloto real" en
> `docs/planes/PLAN-FASE-3-5-PORCINO.md`). Quedan acá con disparador explícito, no se
> "esconden" en un criterio vago. Cuando el disparador ocurra, la entrada se promueve a
> tarea concreta de la rama o sub-plan correspondiente.

- **3.5a.7.1–5 — UI del sujeto "lote" (pesaje muestral, baja con causa, vacunación de
  lote, diagnóstico grupal, consumo en sacos).** Mergeada al `develop` por PR #88
  (rama `feature/field-app-lot-registration`, commit `04ff02f`, 2026-08-09), con
  `TapBudget` validado. Lo que queda pendiente es el **reorden** de las seis actividades
  del sujeto lote cuando el cliente responda sec.7-C del plan con las frecuencias reales.
  **Disparador:** respuesta del cliente a `PLAN-FASE-3-5-PORCINO.md` sec.7-C. Cuando
  ocurra, este ítem sale del backlog y entra a la tarea explícita de reorden.

- **3.5a.8 — Corrección de registros desde el teléfono (ADR-0017).** Sin esto, la
  corrección durante el piloto se hace por re-registro manual (lo que el criterio
  completo de 3.5a explícitamente rechaza como prueba). Aceptado por ADR-0024 mientras
  la deuda esté rastreable. **Disparador:** cualquier rechazo o dedupe en el outbox del
  cliente móvil durante el piloto que no pueda corregirse volviendo a registrar — o el
  operario reporta "no encuentro cómo arreglar lo que escribí mal".

- **3.5a.3 — Causas configurables de muerte de lechón.** Sin catálogo de causas, la
  mortalidad se registra sin causa (válido para el piloto; el índice de madres de 3.5b.6
  queda incompleto hasta que esto exista). **Disparador:** el cliente pide distinguir
  causas durante el piloto, o el índice de madres (3.5b.6) necesita la causa antes de
  que se cierre el primer ciclo de engorde.

## Bloqueante transversal (descubierto durante el piloto)

- **No hay forma trazable de "rellenar" inventario de comida desde el panel.** Existe
  `POST /api/v1/inventory/items/{itemId}/batches` que crea un `InventoryBatch` con cantidad,
  costo y vencimiento opcional — pero sin `ReceivedAt` propio, sin proveedor, sin factura,
  y sin evento de dominio. La UI admin-web (`InventoryBatchesSectionComponent`) lo expone
  como botón "Crear lote"; el **móvil no tiene UI** para reponer (solo consumir; el catálogo
  `inventoryBatches`/`unitConversions` no viaja en el pull del sync). El dueño puede registrar
  que llegó alimento, pero la fila queda asociada al `created_at` del sistema, no a una fecha
  declarada de recepción, y no se puede reconstruir qué proveedor entregó qué. **El consumo
  desde lote (3.5a.7) mergeado depende de este flujo para no trabajar contra stocks vacíos
  sin historia.** Solución acordada: comando mínimo `RecordInventoryReceptionCommand` con
  `ReceivedAt` (requerido) y `SupplierLabel`/`InvoiceReference`/`Notes` (opcionales, texto
  libre); endpoint dedicado `POST /api/v1/inventory/items/{itemId}/receptions`; permiso
  nuevo `inventory.receptions.manage`; UI admin-web "Recibir alimento"; comando diseñado
  abierto a extensión para que Fase 4 (Purchasing) lo envuelva con `SupplierId`/`PurchaseOrderId`
  FK sin romper contrato. Móvil **no** se toca: la reposición es labor de oficina, no del
  operario en el potrero (Art. 9). **Vida útil:** deprecado cuando llegue
  `Purchase/PurchaseReception` de Fase 4 — los batches existentes preservan `SupplierLabel`
  como etiqueta histórica; Fase 4 añade un script de deduplicación texto→`Supplier`. Cubierto
  por ADR-0026.
  **Disparador:** el piloto real pierde trazabilidad de compras, o el contador pide
  reconstruir el proveedor de un batch viejo y no se puede.

## Seguridad — follow-up de la auditoría del PR #94 (ADR-0026)

> Hallazgos del security-audit y code-review del PR
> [#94](https://github.com/JosephBano/erp-hacienda/pull/94). Los 🟠 Altos del security-auditor
> (`AL-01` RecordedById sin JWT, `AL-02` migración con `nullable: false, defaultValue: null` en
> tabla no vacía, `AL-03` coexistencia `POST /batches`/`/receptions` sin distinguisher técnico)
> y los 🟡 Medios MD-02 (decimal sin tope) y MD-04 (ReceivedAt sin cota inferior) se arreglan
> en commits de seguimiento del propio PR antes de mergear. Los ítems siguientes son las
> restantes que se dejan como deuda rastreable.

- **`POST /feed-consumptions` sigue sin gate de permiso (BJ-04).** Pre-existente al PR #94
  pero queda como único endpoint de escritura de inventory sin policy después de que el PR
  cerró el gap de `/batches` y `/feed-stage`. Riesgo: cualquier usuario autenticado puede
  registrar consumo sin permiso específico. **Disparador:** cualquier observación del
  cliente sobre "no entiendo por qué este usuario puede hacer esto"; o, en todo caso,
  antes de Fase 4 cuando se introduzca el rol "registrador de compras".

- **Rate limiting en endpoints de escritura de Inventory (MD-01).** Ningún middleware
  `AddRateLimiter` aplicado a `POST /receptions`/`/batches`/`/feed-consumptions`. Un admin
  puede iterar 10.000 recepciones por minuto. **Disparador:** el sistema muestra signos de
  abuso accidental (operario con script en loop) o el sync pull del field-app ve deltas
  anormales en `inventory_batches` por spam.

- **Migrar JWT de `sessionStorage` a cookie httpOnly (MD-03).** Pre-existente al PR. El
  admin-web guarda el JWT en `sessionStorage` (ver `clients/admin-web/src/app/services/auth.service.ts:23`),
  vulnerable a XSS. **Disparador:** primera vulnerabilidad XSS detectada, o cuando se
  rediseñe el flujo de login para Fase 4. Cambio de magnitud: login + refresh + CORS.

- **Guideline sobre GUIDs determinísticos en seeds (BJ-01).** Los GUIDs `11111111-…`
  (admin) y `08000000-…` (permisos) son público-por-diseño. Si en algún endpoint futuro
  se filtra `role_id` en una respuesta, un atacante sabe qué GUID mapear a "admin".
  **Disparador:** la próxima vez que se diseñe un endpoint que devuelva `role_id` o
  `permission_id` en su respuesta. ADR pequeño reconociendo la decisión.

- **Cerrar coexistencia `POST /batches` con distinguisher técnico (AL-03).** Mientras
  el issue #93 esté abierto, el cierre real (opción C header `X-Allow-Legacy-Batch: true`
  o opción D eliminar el endpoint) queda pendiente. **Disparador:** el dueño contesta
  el issue #93 con la opción preferida.

## Ideas sin fase asignada

- **Fotos de eventos**: la app de campo ya guarda la referencia local (`photoUri`) y la
  marca explícitamente como `photoUploaded: false`; la subida real depende del módulo de
  adjuntos (`feature/shared-attachments`, Fase 4).
