# BACKLOG — deuda técnica e ideas fuera de fase

> Este archivo fusiona el parking lot de deuda técnica por sub-rama con las ideas que
> (regla ROADMAP.md, "Reglas anti-estancamiento" #2) no entran en la fase en curso. Un
> ítem del BACKLOG no es una tarea: es algo a discutir antes de actuar. Las ideas se
> revisan al cerrar cada fase — algunas suben a la fase siguiente, otras se descartan
> con una línea explicando por qué.

## Deuda abierta por sub-rama

## Pendiente feature-0008 — Aplicar permisos de forma consistente en API y sincronización (2026-09-08)

> Aplicación de permisos granulares en rutas REST y sincronización offline en `feature/people-permission-enforcement`.
> Los siguientes ítems recogen deuda técnica identificada y límites explícitos de alcance (spec sec. 5, tasks TC.7, regla 9):

### [auth] Migración de JWT en almacenamiento local / Bearer header a cookies HttpOnly

Actualmente la API recibe el token JWT en el encabezado `Authorization: Bearer <token>` y los clientes web/móvil lo conservan en almacenamiento local. La migración a cookies seguras `HttpOnly` con flag `SameSite` para mitigar riesgos de exfiltración vía XSS en web fue expresamente excluida del spec 0008 (spec sec. 5). Debe abordarse mediante un ADR propio cuando se estandarice el gateway web.

### [security] Limitación de tasa de intentos de autenticación (Rate Limiting en `/auth/login`)

El endpoint de login no restringe el número de intentos fallidos consecutivos por IP o por cuenta (SEGURIDAD.md sec. 6). Aunque el mensaje de error es uniforme y el hash utiliza PBKDF2 con 100.000 iteraciones, se requiere incorporar middleware de rate limiting o protección perimetral contra fuerza bruta antes de exponer la API a internet público sin VPN.

### [auth] Invalidación inmediata de refresh tokens y revocación en cascada al desactivar cuenta

`DeactivateUserCommand` marca al usuario como `IsActive = false` (lo cual es validado en cada request por `OnTokenValidated`), pero no revoca explícitamente en la base de datos todos los refresh tokens asociados emitidos previamente. Se debe diseñar un comando de revocación en lote para invalidar la cadena completa de sesiones activas.

### [multitenancy] Aislamiento multi-finca y particionamiento por tenant

El modelo actual opera bajo el supuesto de una sola hacienda física. El aislamiento estricto por finca (filtrado obligatorio por `farm_id` en todas las consultas de dominio) permanece diferido para fases avanzadas del roadmap (Fase 4+).

### [auth] Ámbito de permisos por grupo o lote (`group_id` en `user_roles` y `role_permissions`)

ADR-0007 diseñó las columnas opcionales `group_id` en las tablas de asignación de roles y permisos para permitir acotar privilegios por lote de animales en el futuro. Esta capacidad se mantiene en reserva de esquema y no está conectada en las políticas de autorización vigentes.

## Pendiente feature-0004 — Sincronización móvil fiable (2026-09-07)

> Estabilización de contratos de push y pull, motor de sincronización de `field-app`
> y persistencia diagnóstica en `feature/sync-field-app-reliability`. Los siguientes
> ítems recogen deuda técnica identificada que deliberadamente queda fuera de esta rama
> (regla 9):

### [sync] Compensación de reservas de inventario inter-módulos interrumpidas (Compuerta 1 / D4)

En la Compuerta 1 se evaluó si registrar el resultado de una reserva pendiente requería
modificar ADR-0008. Se determinó que no lo altera en el protocolo de sincronización
móvil porque el cliente ya preserva la operación en `sync_outbox`. Sin embargo, a nivel de
backend, los módulos `Livestock`/`Production` y `Inventory` operan sobre `DbContext`
independientes (arquitectura de monolito modular). Si una transacción en el servidor se
interrumpe tras haber creado la reserva en `Inventory` pero antes de confirmar el evento
de campo o si ocurre un fallo no recuperable, no existe un mecanismo formal de compensación
automática distribuida (Saga / outbox transaccional intermodular) para cancelar la reserva
sin intervención manual. Evaluar en Fase 4 al abordar contabilidad y compras.

### [testing] Ejecución de suite de campo `test-e2e.md` en SQLite nativo

Las pruebas automáticas de `clients/field-app` corren con Jest utilizando `LokiJSAdapter`
para emular la base de datos de WatermelonDB en Node.js. Aunque validan la concurrencia y
la lógica del motor de sincronización, las peculiaridades del driver SQLite nativo
(bloqueos de concurrencia en disco en Android/iOS, migraciones en SQLite real, comportamiento
de threads en background de React Native) requieren completar la verificación manual en
hardware real siguiendo `docs/spec/feature-0004-field-app-sync-reliability/test-e2e.md`.

### [mobile] Estandarización de confirmación de plausibilidad en formularios móviles

El flag `isPlausibilityConfirmed` fue incorporado a `MilkingSession` resolviendo el defecto
determinista S8. En otras actividades (pesajes, tratamientos, partos), las cotas de
plausibilidad actualmente viajan en cadenas JSON anidadas o no se persisten explícitamente
como columna confirmada. Conviene diseñar un patrón consistente de diálogo de advertencia
no bloqueante que persista la decisión del operario en todos los módulos de campo.

## Pendiente feature-0005 — Validaciones de aptitud por actividad (2026-09-08)

> Validaciones de aptitud biológica y de estado por actividad en campo y servidor en `feature/livestock-activity-validation`.
> Los siguientes ítems recogen deuda técnica identificada y límites explícitos de alcance (spec sec. 4 y 5, regla 9):

### [production] Ciclo de vida y exigencia de `Lactation` activa en ordeño

La validación de aptitud para ordeño (`RecordMilkingSessionCommand` y `milkingService`) comprueba sexo hembra, especie ordeñable y ausencia de baja efectiva anterior al hecho, pero no exige una lactancia activa (`Lactation`) porque su ciclo de vida automático y manual aún no está implementado en Livestock ni Production (spec sec. 4). Requiere modelar el inicio (tras parto) y secado de lactancias antes de poder imponerlo como bloqueo.

### [production] Distinción operativa entre leche ordeñada descartada por retiro y vendible

Actualmente la leche de animales bajo período de retiro no es vendible y el sistema bloquea su registro como vendible (ADR-0005, regla dura 6, spec sec. 4). Falta definir una especificación y flujo operativo para registrar sesiones de ordeño donde la leche de animales bajo retiro se descarte físicamente sin registrarse como volumen disponible para tanque ni mezclarse con leche comercializable.

### [livestock] Auditoría y eventos de corrección para registros históricos preexistentes

Conforme a la Regla Dura 1 (inmutabilidad de eventos históricos) y spec sec. 5, los registros históricos preexistentes en la base de datos de producción que no cumplan las nuevas invariantes (p. ej., ordeños o partos previos a estas validaciones) no se eliminan ni alteran de forma automática. Se requiere ejecutar las consultas de auditoría identificadas (TC.4) y emitir eventos de corrección cuando el dueño lo determine.

## Pendiente feature-0007 — Identificación individual por arete en campo (2026-09-08)

> Identificación individual por arete, convivencia con lotes por conteo y desambiguación móvil en `feature/livestock-individual-tagging`.
> Los siguientes ítems recogen deuda técnica identificada y límites explícitos de alcance (spec sec. 4 y 5, tasks T2.5, regla 9):

### [livestock] Unicidad concurrente de aretes / identificadores en base de datos (spec sec. 4, T2.5)

Actualmente, `AnimalIdentifierConfiguration.cs:24` garantiza unicidad por animal y tipo vigente (`AnimalId` + `Type` + `IsActive`), pero no restringe que dos animales distintos compartan el mismo valor de arete o identificador. Además, `AssignAnimalIdentifierCommand.cs:17` carga el animal y sus identificadores pero no busca la existencia previa del mismo valor en otros animales.

Garantizar la unicidad concurrente de aretes a nivel de base de datos entre animales requiere:
1. Un ADR formal que defina la política de duplicados y su normalización (alcance por finca, por tipo de identificador, reglas para reutilización temporal tras baja o muerte, tratamiento de mayúsculas y ceros iniciales).
2. Una revisión no destructiva de los datos existentes en la base de datos de producción (la Regla Dura 1 prohíbe borrados o alteraciones destructivas arbitrarias).
3. Una migración de EF Core que cree el índice único filtrado correspondiente (p. ej. sobre `FarmId` + `Type` + `Value` donde `IsActive = true`).

Hasta que este ADR y su migración se definan y aprueben, el diseño móvil de campo detecta y presenta explícitamente las ambigüedades sin resolverlas de manera automática y sin fusionar registros de animales (Commit 2, T2.1–T2.4).

### [livestock] Política de duplicados y normalización de aretes (spec sec. 4, TC.6)

El tratamiento de aretes repetidos entre distintos animales o reingresos exige fijar reglas formales de normalización: alcance por finca y por tipo de arete, posibilidad y condiciones de reutilización temporal de aretes tras baja confirmada, preservación estricta de mayúsculas y conservación de ceros a la izquierda. Hasta la definición de esta política, el sistema móvil detecta ambigüedades y exige selección consciente sin adivinar.

### [livestock] Transición física y linaje de lotes históricos ya mezclados (spec sec. 1, 5, TC.6)

La declaración del dueño «todos ellos van a tener un arete» no autoriza la reconstrucción arbitraria de genealogías ni la asignación de identificadores a filas del sistema en lotes antiguos que ya fueron mezclados físicamente (ADR-0015 y D4). La transición de estos lotes históricos requiere un protocolo aprobado de inventario físico presencial con el dueño y registro de linaje desconocido, el cual queda fuera de la fase actual y deberá abordarse cuando se complete dicho inventario.

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
  `docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-{A,B,C}.md`. **A y B ya
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
  abrir [ADR-0024](./adr/0024-pilot-decoupling-from-3-5a.md) y arrancar por
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
- **Plan**: [`docs/spec/feature-0001-admin-web-animal-groups/`](./spec/feature-0001-admin-web-animal-groups/spec.md).
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
> sentado [ADR-0021](./adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md) ·
> que a su vez reemplaza a [ADR-0020](./adr/0020-fase-3-5-estado-al-cierre-del-barrido-p0-p1.md)),
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
  [`3.5a.2-A`](./spec/plan-0002-fase-3-5/sub-planes/3.5a.2-A.md)
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

### [deuda] Bloque 4.2–4.7 de la Fase 3.5 sin empezar

- **Estado (auditoría de código, 2026-08-16)**: de las 21 tareas de los bloques
  4.2 (`feature/tasks-health-plan-alerts`), 4.3 (`feature/inventory-feeding-standards`),
  4.4 (`feature/analytics-lot-fcr`), 4.5 (`feature/livestock-animal-traits`), 4.6
  (`feature/breeding-maternal-index`) y 4.7 (`feature/tasks-swine-alerts`), **ninguna**
  tiene una sola línea de código en el repo. No hay rama, migración, clase ni test
  esqueleto para ninguna de las seis. El trabajo se detuvo justo después de 4.1
  (Health Plans, que sí está completo — `HealthPlan`/`HealthPlanItem` con ancla,
  desfase, ventana y asignación a lote o individuo) y nunca arrancaron ni el
  generador de alertas `HEALTH_PLAN_ITEM_DUE` que consume los planes sanitarios, ni
  los estándares de alimentación, el FCR, los rasgos observables, el índice
  maternal o las alertas específicas de porcino.
- **No es un defecto documental**: no se encontró ningún documento (`ROADMAP.md`,
  `spec.md`, un commit "close"/"merge") que afirme que estas seis ramas ya existen.
  `docs/ROADMAP.md` describe 3.5b en tiempo futuro/descriptivo, sin marcarlo como
  completado. El corte es honesto — nadie declaró terminado lo que no se construyó
  — pero es el hueco más grande en tamaño de toda la Fase 3.5 y conviene que quede
  visible aquí en vez de asumirse implícito.
- **Ver**: `docs/spec/plan-0002-fase-3-5/tasks.md` sección "Bloque 3.5b — Análisis y
  automatización" (tareas T-4.2-1 a T-4.7-2, todas sin marcar con evidencia
  negativa citada línea por línea).

### [deuda] `T-3.5a.7-6` — ficha del lote incompleta (2 de 5 datos)

- **Estado (auditoría de código, 2026-08-16)**: la tarea pide cinco datos en la
  ficha del lote — cabezas vivas, peso promedio, última vacunación, enfermos,
  alimento del período. El backend
  (`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/GetAnimalGroupQueries.cs:145-208`,
  `AnimalGroupSummaryDto`) solo calcula `liveHeadCount`, `headsAffectedByDiagnosis`,
  `lastVaccinationAt`, `lastDisposalAt`, `lastTreatmentAt`. **No calcula peso
  promedio ni alimento del período** — faltan 2 de los 5 datos pedidos, y no hay
  ningún cálculo parcial de ninguno de los dos en ningún lado del backend.
- **El caso más barato de cerrar**: del lado móvil,
  `clients/field-app/src/screens/LotSubjectScreen.tsx:88-94` solo renderiza
  `liveHeadCount` y `headsAffectedByDiagnosis` — **pese a que `lastVaccinationAt` ya
  viaja en el DTO** (`clients/field-app/src/services/animalGroupsApi.ts:5`). El dato
  existe de punta a punta hasta la pantalla y simplemente no se muestra. Es la
  brecha más barata de las tres (backend + backend + un `<Text>` en el front) y la
  más absurda de dejar así.
- **Nota sobre la documentación existente**: el comentario en
  `clients/field-app/src/services/animalGroupsApi.ts:11` ("already delivered
  server-side") es técnicamente cierto para el endpoint pero engañoso sobre el
  alcance — no menciona que el endpoint no calcula 2 de los 5 datos originales.
- **Ver**: `docs/spec/plan-0002-fase-3-5/tasks.md`, tarea `T-3.5a.7-6` (sin marcar, con
  evidencia negativa citada línea por línea).

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

### ~~[test] El riesgo de N+1 de `GetAnimalGroupsHandler` quedó sin red~~ — RESUELTO 2026-08-17

- **Causa raíz**: el plan de la serie de animal-groups declaró como mitigación una prueba de
  integración que contara consultas con un `DbCommandInterceptor` (50 grupos, ≤4 consultas), y
  esa prueba **nunca se escribió**. La agregación server-side de `GetAnimalGroupsHandler`
  funcionaba, pero si alguien rompía el batching al tocarla, ninguna prueba lo detectaba — el
  resultado seguía siendo correcto, sólo que lento.
- **Detectado**: 2026-08-17, al convertir `PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` a
  `docs/spec/feature-0001-admin-web-animal-groups/`.
- **Resuelto**: PR #105, con la prueba que el plan describía —
  `GetAnimalGroupsQueryCountApiTests.cs`, más `QueryCountingInterceptor.cs` y
  `QueryCountingApiFactory.cs`. Conteo real observado: exactamente 4 consultas para 50 grupos,
  los cuatro round-trips documentados del handler; no había N+1. T1.10 del `tasks.md` de esa
  carpeta queda en `[x]`.

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
  cliente que `docs/spec/plan-0002-fase-3-5/spec.md` sec.2.3/sec.7-C exige para fijar el orden por
  frecuencia real. El orden usado (alimento, pesaje muestral, vacunar, tratar,
  diagnóstico, baja) es el supuesto explícito documentado en el código de
  `LotSubjectScreen.tsx` — "alimento es lo más frecuente" según el propio plan — no una
  medición con el operador parado en el corral.
- **Trabajo a hacer**: cuando el cliente responda sec.7-C, actualizar el orden de los
  seis `BigButton` en `LotSubjectScreen.tsx` (y el de `ActivitiesHub.tsx` si el orden de
  los cuatro sujetos también cambia) en un commit dedicado, con el test de orden
  actualizado a propósito — nunca como un efecto colateral de otro cambio.
- **Disparador**: la sesión con el cliente de sec.7-C (`docs/spec/plan-0002-fase-3-5/spec.md`).

### [sync] Reconciliación automática de existencia entre servidor y dispositivo

- **Archivos**: `src/Hato.Api/Sync/SyncPullQueries.cs`, `clients/field-app/src/services/syncEngine.ts`.
- **Causa raíz**: el pull actual entrega deltas basados en `(UpdatedAt ?? CreatedAt) > cursor`
  y propaga tombstones lógicos (`deleted_at`), pero no reconcilia eliminaciones físicas en BD
  (reset de ambiente, restauración de backup, depuraciones directas). La rama `feature/field-app-parto-redesign`
  mitigó esto con la acción manual "Rehacer descarga" en `SyncStatusScreen.tsx` (D7), pero no
  existe detección ni purga automática de filas huérfanas en el cliente.
- **Trabajo a hacer**: implementar un mecanismo de reconciliación automática periódica o bajo demanda
  (mediante checksum por colección o lista compacta/bloom filter de IDs vigentes) que permita
  al cliente detectar y purgar localmente registros inexistentes en el servidor sin requerir
  intervención manual del usuario.
- **Disparador**: cuando el uso en múltiples fincas o entornos muestre recurrencia de datos locales
  desalineados tras restauraciones de servidor, o cuando se diseñe una versión 2 del protocolo de sync.

### [mobile] Rediseño de espacio y scroll en pantallas de registro (Milking, Events, Treat, LotEvents)

- **Archivos**: `clients/field-app/src/screens/MilkingScreen.tsx`, `clients/field-app/src/screens/EventsScreen.tsx`,
  `clients/field-app/src/screens/TreatScreen.tsx`, `clients/field-app/src/screens/LotEventsScreen.tsx`.
- **Causa raíz**: las pantallas de captura mencionadas comparten el mismo patrón que motivó el rediseño
  de `BirthScreen` y `ActivitiesHub` (spec `feature-0002-field-app-parto-redesign` sec. 1 y sec. 9):
  contenedores con `scrollable = false` o vistas con listas internas anidadas compitiendo por espacio flex,
  lo que dificulta la navegación y el espaciado en teléfonos de pantallas reducidas manteniendo los objetivos
  táctiles de 64pt del tema.
- **Trabajo a hacer**: rediseñar o ajustar el layout de cada pantalla para asegurar que el contenido
  fluya con scroll holgado o se organice en pasos guiados (asistentes) sin comprimir elementos interactivos.
- **Disparador**: cuando los operarios reporten incomodidad de uso en esas pantallas específicas o se
  abra una sub-rama de UX dedicada al flujo de captura diaria en campo.

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
> Lo que sí entra está en `docs/spec/plan-0002-fase-3-5/spec.md`; lo que ya quedó decidido en los
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
> `docs/spec/plan-0002-fase-3-5/spec.md`). Quedan acá con disparador explícito, no se
> "esconden" en un criterio vago. Cuando el disparador ocurra, la entrada se promueve a
> tarea concreta de la rama o sub-plan correspondiente.

- **3.5a.7.1–5 — UI del sujeto "lote" (pesaje muestral, baja con causa, vacunación de
  lote, diagnóstico grupal, consumo en sacos).** Mergeada al `develop` por PR #88
  (rama `feature/field-app-lot-registration`, commit `04ff02f`, 2026-08-09), con
  `TapBudget` validado. Lo que queda pendiente es el **reorden** de las seis actividades
  del sujeto lote cuando el cliente responda sec.7-C del plan con las frecuencias reales.
  **Disparador:** respuesta del cliente a `docs/spec/plan-0002-fase-3-5/spec.md` sec.7-C. Cuando
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

## Seguridad — hallazgos de la auditoría de `SEGURIDAD.md` (2026-08-16)

> `docs/SEGURIDAD.md` se escribió auditando `src/Hato.Api/Endpoints/*.cs` línea por línea
> (los 19 archivos) en vez de resumir ADR-0007/ADR-0008. Estos ítems son la diferencia entre
> lo que ADR-0007 diseñó y lo que quedó realmente conectado en el código, más dos hallazgos
> de higiene de sesión sin relación con permisos. Ninguno se arregló en esta rama (regla 9,
> AGENTS.md: un PR, un propósito) — este PR es solo documentación.

- **[seguridad] Siete permisos declarados en `SystemPermissions` nunca se exigen en ningún
  endpoint.** `breeding.events.record`, `breeding.events.read`, `production.milking.record`,
  `production.milking.read`, `tasks.manage`, `tasks.read`, `people.users.read`
  (`src/Modules/People/Hato.Modules.People.Domain/UserRole.cs:49-90`) no aparecen en ningún
  archivo de `src/Hato.Api/Endpoints/`. Efecto: `BreedingEndpoints.cs`, `MilkingEndpoints.cs`
  y `TasksEndpoints.cs` están abiertos por completo a cualquier usuario autenticado, sin el
  control fino por rol que ADR-0007 diseñó explícitamente (el ejemplo del propio ADR —
  "un operador de ordeño debe poder registrar leche sin acceso a reportes financieros" —
  no tiene gate que lo haga cumplir hoy). **Disparador:** antes de que un rol no-admin
  reciba acceso al sistema en el piloto real, o la primera vez que el dueño pida "que este
  empleado no pueda X".

- **[seguridad] Asimetría entre escritura individual y grupal de eventos/animales.**
  `POST /api/v1/animals/{id}/events` (`AnimalEventsEndpoints.cs`), `POST /api/v1/animals`
  (registrar) y `POST /api/v1/animals/{id}/identifiers` (`AnimalsEndpoints.cs`) no exigen
  `livestock.animals.write`, mientras que `PUT`/`DELETE` sobre el mismo recurso y el
  equivalente grupal (`POST /api/v1/animal-groups/{id}/events`) sí lo exigen. Ningún ADR
  documenta la asimetría como decisión. **Disparador:** el mismo que el ítem anterior —
  ambos se resuelven mejor en un solo PR de "cerrar los gates de permiso que ADR-0007 dejó
  a medias" que revise los 19 archivos de una vez.

- **[seguridad] `PlausibilityRangesEndpoints.cs` documenta en comentario un permiso que el
  código no implementa.** El comentario XML del archivo (líneas 6-13) afirma que las
  mutaciones exigen `livestock.animals.write`; ningún `POST`/`PATCH`/`DELETE` del archivo
  llama `RequirePermission`. Quien lea solo el comentario concluye que el catálogo está
  protegido. **Disparador:** mismo PR que cierre los gates de permiso de arriba — corregir
  el comentario para que describa el código real, o el código para que cumpla el
  comentario, lo que decida el dueño.

- **[seguridad] `docker-compose.yml` fuerza `Development` en el contenedor `api`, anulando
  la clave de firma JWT persistente.** `docker-compose.yml:39` fija
  `ASPNETCORE_ENVIRONMENT: Development`, sobreescribiendo el `ENV
  ASPNETCORE_ENVIRONMENT=Production` de `src/Hato.Api/Dockerfile:27`. Como
  `Jwt:SigningKey` no está en `docker-compose.yml` y el proceso nunca corre en
  `IsProduction()` bajo este compose, `PeopleModule.cs:53-55` genera una clave aleatoria
  en memoria en cada arranque — invalidando todo JWT de acceso emitido antes del reinicio
  (hasta 8h de sesiones activas), aunque los refresh tokens en BD sobreviven. La validación
  que exige clave real de 32+ caracteres en producción (`PeopleModule.cs:66-70`) nunca se
  ejercita en este camino de despliegue. **Disparador:** antes de cualquier despliegue del
  compose fuera de una máquina de desarrollo — fijar `ASPNETCORE_ENVIRONMENT: Production` y
  agregar `Jwt__SigningKey` a las variables requeridas del `.env`.

- **[seguridad] `GET /api/v1/sync/operations` no filtra por usuario ni exige permiso
  propio.** `GetSyncOperationsQueryHandler`
  (`src/Hato.Api/Sync/SyncPullQueries.cs:661-689`) consulta `context.SyncOperations` sin
  `Where(o => o.UserId == ...)` — cualquier usuario autenticado ve las últimas 100
  operaciones de sync de todos los empleados y dispositivos (`DeviceId`, `ErrorDetails`
  incluidos). Compárese con `GET /api/v1/sync/conflicts`, en el mismo archivo, que sí exige
  `people.users.manage`. **Disparador:** mismo PR de cierre de gates — filtrar por usuario
  salvo que quien llame tenga un permiso de alcance más amplio (a definir cuál).

- **[seguridad] Sin límite de intentos de login.** `LoginCommandHandler`
  (`src/Modules/People/Hato.Modules.People.Application/Auth/LoginCommand.cs`) no aplica
  rate limiting ni bloqueo temporal tras intentos fallidos repetidos. PBKDF2 (100.000
  iteraciones) impone un costo por intento pero no un límite duro. **Disparador:** el
  piloto real expone el endpoint fuera de la red de la finca (p. ej. acceso remoto al
  panel), o se detecta un intento de fuerza bruta en logs.

- **[seguridad] Sin detección de reuso de refresh token revocado.**
  `RevokeRefreshTokenCommandHandler` y `RefreshAuthTokenCommandHandler`
  (`src/Modules/People/Hato.Modules.People.Application/Auth/RefreshTokenCommands.cs`) no
  implementan el patrón de "revocar toda la cadena del usuario si se presenta un token ya
  revocado" (señal común de robo de refresh token). **Disparador:** evidencia de que un
  refresh token se filtró o se usó desde un dispositivo no reconocido.

- **Nota:** `POST /feed-consumptions` sin gate de permiso (BJ-04, arriba) es el mismo
  patrón de gate faltante que los ítems de esta sección — se referencia, no se duplica.

## Docs — hallazgos de la auditoría de diagramas en `docs/reestructura-documentacion` (2026-08-16)

> Surgieron auditando `docs/DATA-MODEL.md` contra el código real mientras se ejecutaban los
> 11 commits de la reestructura documental. No caben en ningún commit del plan y no se
> corrigen aquí (regla 9, AGENTS.md: un PR, un propósito) — este PR es solo documentación.

- **[docs] `docs/DATA-MODEL.md` tiene dos afirmaciones desactualizadas sobre qué está
  implementado.**
  - El "Núcleo 6" (`docs/DATA-MODEL.md:233-269`) presenta el CHECK XOR `animal_id`/`group_id`
    de `animal_events` bajo el banner "nada de esto está implementado"
    (`docs/DATA-MODEL.md:235-236`). Ya no es cierto: la constraint existe y se aplica —
    `CK_AnimalEvent_AnimalXorGroup` en
    `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Configurations/AnimalEventConfiguration.cs:16-18`
    (`(animal_id IS NOT NULL) <> (group_id IS NOT NULL)`).
  - El "Núcleo 5 — Producción de leche" (`docs/DATA-MODEL.md:203-227`) describe captura por
    grupo/tanque, resolución automática de `lactation_id` y el enlace con
    `withdrawal_periods` como si fuera el diseño vigente. Las migraciones reales de
    `src/Modules/Production/Hato.Modules.Production.Infrastructure/Persistence/Migrations/`
    (`20260801183926_InitialCreateProduction.cs`, `20260802163710_AddAuditRecordedBy.cs`) no
    construyen ese diseño: nunca se llegó a implementar.

  Corregir `docs/DATA-MODEL.md` para reflejar el estado real es **otro propósito** (regla 9,
  AGENTS.md) y por eso no se hace en esta rama. **Disparador:** el PR que retome Producción
  de leche o el CHECK XOR como trabajo activo debería actualizar el documento antes de tocar
  código, no después.

- **[docs] `Lactation` es código muerto — confirmación en código de una deuda ya declarada.**
  La tabla `lactations` existe y la entidad tiene métodos de dominio funcionales
  (`Lactation.Start`, `Lactation.Close`,
  `src/Modules/Production/Hato.Modules.Production.Domain/Lactation.cs:27-38`), pero ningún
  comando de `Hato.Modules.Production.Application` los invoca — un `grep` sobre
  `src/Modules/Production/` no encuentra un solo `Lactation.Start(` ni `Lactation.Close(`
  fuera de la propia clase; `IProductionDbContext` solo expone el `DbSet<Lactation>`. Esto no
  es un hallazgo nuevo: es la confirmación en código de la deuda ya declarada en
  "De Fase 2 (heredado, seguía pendiente)" (arriba, en este mismo archivo) y en
  `docs/ROADMAP.md:141-142` ("el ciclo de vida de `Lactation` (Fase 2, nunca implementado)").
  **Disparador:** el mismo que el ítem de Fase 2 — decidir si el ciclo de vida lo dispara
  Breeding (al parto) o Production (al primer ordeño) antes de mostrar "lactancias activas"
  en cualquier panel.

- **[código] `ResolveConsumptionBatchId` calcula el `batch_id` de un consumo FIFO sobre
  stock ya descontado, no sobre "quién perdió stock".**
  `src/Modules/Inventory/Hato.Modules.Inventory.Application/Consumptions/RecordGroupFeedConsumptionCommand.cs`:
  `DeductFromBatchesFifo` (línea 100, cuerpo en 136-175) muta `InventoryBatch.Quantity` en
  memoria; `ResolveConsumptionBatchId` (línea 102, cuerpo en 184-193) corre **después**,
  filtrando `item.Batches.Where(b => b.Quantity > 0)` sobre ese mismo estado ya mutado —no
  sobre qué lote participó en la deducción. Dos casos de falla concretos:
  - Si la deducción deja un lote en **exactamente 0** (el caso más común: el operario agota
    el saco más viejo), ese lote queda excluido del filtro por `Quantity > 0` y `batch_id`
    apunta al **siguiente** lote con stock — que puede no haber perdido ni una unidad.
  - Si la deducción agota **todos** los lotes, `FirstOrDefault()` no encuentra nada y
    `batch_id` queda **`null`** — exactamente el comportamiento que
    `feature/inventory-consumption-history` (PR #102) vino a corregir.

  El comentario XML de la línea 180 ("the row carries the **first** batch that lost stock")
  describe el diseño pretendido, no el código: es correcto solo cuando ningún lote llega a
  cero. Hallazgo de la ronda de corrección 1 sobre `d70205b`
  (`docs/diagramas/flujo-consumo-alimento.mermaid`, nodo `ResolveConsumptionBatch`), que ya
  documenta el flujo real con este bug visible. No se corrige aquí (regla 9, AGENTS.md: un
  PR, un propósito — esta rama es documental). **Disparador:** antes de confiar el reporte
  "¿de qué lote salió este consumo?" del panel para trazabilidad de vencimientos o de costo
  por lote; la corrección probable es capturar los ids de los lotes tocados dentro del mismo
  bucle de `DeductFromBatchesFifo`, no re-derivarlos después.

- **[docs] `docs/adr/0022-rangos-plausibilidad.md:11` enlaza a
  `../spec/PLAN-FASE-3-5-PORCINO.md`, archivo que esta rama borró (commit `8a33d41`).**
  Es el único ADR de la fase con un enlace markdown real (`](...)`) al archivo eliminado;
  `0019-visibilidad-de-modulos.md`, `0021-cierre-retroactivo-compuerta-3-5a-9-B.md`,
  `0023-eventos-clasificacion-por-peso.md` y `0024-pilot-decoupling-from-3-5a.md` también
  citan `PLAN-FASE-3-5-PORCINO.md` pero solo como texto/código (`` `PLAN-FASE-3-5-PORCINO.md`
  sec.X.Y ``), sin sintaxis de enlace, así que no rompen la verificación de enlaces. No se
  arregla en esta rama: un ADR no se edita retroactivamente, se reemplaza por uno nuevo (regla
  del propio proyecto, tarea T11.4b del plan de reestructuración). Queda declarado para que no
  se pierda por descuido. **Disparador:** el ADR que reemplace o cierre 0022 debería repuntar
  esa cita a `docs/spec/plan-0002-fase-3-5/spec-3.5a.md` sec.3.5a.6 (destino real de esa sección tras
  la fusión).

- **[docs] `docs/adr/0020-fase-3-5-estado-al-cierre-del-barrido-p0-p1.md:36` y
  `docs/adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md:155` citan `docs/spec/sub_planes/`,
  carpeta que ya no existe:** los ocho sub-planes se movieron a
  `docs/spec/plan-0002-fase-3-5/sub-planes/` y perdieron el prefijo `PLAN-FASE-3-5-PORCINO-` de su
  nombre. `0021` lo cita como ruta en texto y `0020` como mención en prosa; ninguno usa
  sintaxis de enlace, así que no rompen la verificación de enlaces. No se arreglan por la
  misma regla que la entrada de arriba: un ADR no se edita retroactivamente. **Disparador:**
  el ADR que cierre la compuerta de 3.5a.9-B debería citar la ruta nueva.

- **[docs] `docs/PROTOCOLO-DE-TRABAJO.md` cita `AGENTS.md sec.5` (línea 78) y
  `AGENTS.md sec.2` (línea 103), pero `AGENTS.md` ya no numera sus encabezados** — un
  `grep -n "^#" AGENTS.md` no devuelve ninguna sección con esos números. Es deuda preexistente
  a esta rama (tarea TC.0b del plan de reestructuración) y no se corrige acá: es otro
  propósito (regla 9). **Disparador:** cualquier edición de `AGENTS.md` o
  `PROTOCOLO-DE-TRABAJO.md` que toque esas secciones debería resolver la cita — por número si
  se vuelve a numerar, o por nombre de encabezado si no.

## App de campo — visto de paso en `feature/field-app-interaction-reliability` (2026-09-07)

Deuda observada durante el barrido T2.5 de
[feature-0006](spec/feature-0006-field-app-interaction-reliability/spec.md). Ninguna hacía
falta para alcanzar un control, así que ninguna se tocó en esa rama (regla 9, D5).

- **[app de campo] `LoginScreen` no ofrece "ver contraseña".** Pedido explícito de los
  empleados junto con el reporte de scroll del 2026-09-07: el campo es `secure` y no hay
  forma de revisar lo escrito, lo que en un teclado de tablet con guantes es una fuente
  segura de intentos fallidos. No se resolvió en `feature-0006` porque es un control nuevo,
  no una corrección de alcance: ese spec fija que "los cambios visuales que no hagan falta
  para alcanzar un control" pertenecen a otra rama (D5), y AGENTS.md regla 9 pide un PR, un
  propósito. **Disparador:** es un cambio chico y aislado (`TextField` ya recibe `secure`;
  haría falta un `secureTextEntry` conmutable y un botón de alternar dentro del campo);
  puede entregarse como rama propia apenas 0006 esté mergeada, sin esperar al rediseño.

- **[app de campo] Campos de texto que no pasan por `TextField`/`NumberField` y quedan por
  debajo del objetivo táctil de 64.** `src/screens/TodayScreen.tsx` (el campo "¿Qué se
  corrigió?") y `src/screens/birth/Step3Offspring.tsx` (arete y peso de cada cría) declaran
  su propio `TextInput` con estilos locales cuya altura sale del `padding`, mientras
  `src/ui/components.tsx` ya expone campos con `minHeight: theme.touchTarget`.
  `Step2ConfirmDetails.tsx` hace lo mismo pero sí fija `minHeight`. **Disparador:** el
  rediseño integral (`feature-0010-field-app-redesign`, todavía sin carpeta), o cualquier
  rama que ya esté tocando esos formularios por otro motivo.

- **[app de campo] El asistente de parto no tiene `KeyboardAvoidingView`.** `BirthScreen`
  mantiene su `Screen` fijo a propósito (cada paso trae su propio scroll, D1), así que no
  hereda el `KeyboardAvoidingView` del modo `scrollable`. En Android la ventana se
  redimensiona sola y el último control queda visible — que es el caso reportado —, pero en
  iOS el teclado se superpone y el pie del paso 3 ("Continuar a Resumen") puede quedar
  debajo. **Disparador:** la primera vez que la app se pruebe en iOS, o el commit 3 de
  0006 si decide cubrir el asistente.

- **[app de campo] Los selectores de animal renderizan el hato entero sin virtualizar.**
  `EventsScreen` (`animal-list`), `AnimalEditScreen` (`edit-animal-list`) y
  `birth/Step1PickDam` (`dam-list`) mapean la lista completa a `BigButton` dentro de un
  `ScrollView`: con las 200 cabezas del fixture de estrés son 200 `Pressable` montados de
  una vez. No es un defecto de alcance —se llega al final desplazando— pero es el candidato
  más concreto para la queja de fluidez que 0006 D6 deja explícitamente sin reproducir.
  **Disparador:** cuando se identifique teléfono, build y gesto de esa queja; medir esto
  antes de proponer cualquier corrección de animación.

## Ideas sin fase asignada

- **Fotos de eventos**: la app de campo ya guarda la referencia local (`photoUri`) y la
  marca explícitamente como `photoUploaded: false`; la subida real depende del módulo de
  adjuntos (`feature/shared-attachments`, Fase 4).
