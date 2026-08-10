# BACKLOG — ideas, deuda técnica, ítems no bloqueantes

> Este archivo es el parking lot del proyecto. Lo que no entra en un PR porque no es
> bloqueante, o lo que descubrimos mientras hacemos otra cosa, se anota acá.
> Un ítem del BACKLOG no es una tarea: es algo a discutir antes de actuar.

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

## Reglas para este archivo

- Cada ítem lleva un prefijo `[categoría]` (cosmético / tests / deuda / docs / ops).
- Cada ítem tiene: archivos afectados, causa raíz, trabajo a hacer, disparador.
- Un ítem pasa a un PR solo cuando alguien dice "este sí".
- Lo que ya está en un ADR vigente no se duplica acá — se referencia.