# spec.md — Fase 3: app móvil offline-first

> **Documento archivado.** La Fase 3 está cerrada salvo un punto (ver sec. 4): esta
> carpeta es el registro histórico de lo que se construyó, no un plan activo. Se marca
> aquí, en el encabezado, según la regla de archivado de `docs/DOCUMENTACION.md` sec. 4 —
> no se borra ni se vacía.
>
> **Qué es este documento.** La especificación de la Fase 3 tal como quedó ejecutada,
> trasladada desde `docs/planes/PLAN-FASE-3-4.md` (que cubría las Fases 3 **y** 4 juntas)
> más el estado real de la fase desde `docs/ROADMAP.md`. El *cómo se ejecutó* vive en
> [`plan.md`](./plan.md), el desglose con casillas en [`tasks.md`](./tasks.md) y la
> verificación de los escenarios de sincronización en [`test-e2e.md`](./test-e2e.md).
>
> **Por qué esta subcarpeta.** `PLAN-FASE-3-4.md` mezclaba la Fase 3 (ya ejecutada) con la
> Fase 4 (aún no empezada), y **21 citas del código** (`.cs`, `.ts`, `.tsx`) apuntan a
> `PLAN-FASE-3-4 sec.2.2` y `sec.3.A`/`3.B`/`3.C`. Separar la Fase 3 en su propia carpeta,
> con esos mismos números de sección preservados intactos, permite que `PLAN-FASE-3-4.md`
> se retire sin romper ninguna de esas 21 citas (D6 de
> `docs/planes/reestructura-documentacion/spec.md`).
>
> **Procedencia exacta del contenido.** Sec. 2 y sec. 3 de este documento son un traslado
> literal de `PLAN-FASE-3-4.md` secs. 2.2 y 3 (bloques 3.A–3.C) — no una paráfrasis. Sec. 4
> traslada el estado y la retrospectiva de `docs/ROADMAP.md:33-56` y `:109-142`. Donde el texto
> original decía "sec.2.2" o "sec.3.x" en prosa, se conserva tal cual.

- **Rama Git:** `docs/reestructura-documentacion` (desde `develop`).
- **Fecha:** 2026-08-16.
- **Fase del ROADMAP:** Fase 3 — App móvil offline-first.
- **ADRs vigentes que respalda o respeta:** ADR-0007 (permisos en BD), ADR-0008 (protocolo
  de sincronización), ADR-0009 (stack de `field-app`), ADR-0021 (compuerta sec.2.3),
  ADR-0024 (desacople del inicio del piloto).
- **Reglas duras que gobiernan este trabajo:** `AGENTS.md` Art. 1 (no borrado físico,
  aplica al archivado de este documento), Art. 11 (una fase no cierra sin uso real),
  Art. 20 (glosario).

---

## Índice

1. [Por qué existe este documento](#1-por-qué-existe-este-documento)
2. [Nivel de exigencia en pruebas (heredado de PLAN-FASE-3-4 sec. 2)](#2-nivel-de-exigencia-en-pruebas-heredado-de-plan-fase-3-4-sec-2)
3. [Fase 3 — App móvil offline-first (heredado de PLAN-FASE-3-4 sec. 3)](#3-fase-3--app-móvil-offline-first-heredado-de-plan-fase-3-4-sec-3)
4. [Estado y retrospectiva](#4-estado-y-retrospectiva)
5. [Alcance](#5-alcance)
6. [Riesgos y deuda](#6-riesgos-y-deuda)
7. [Criterios de aceptación](#7-criterios-de-aceptación)

---

## 1. Por qué existe este documento

`PLAN-FASE-3-4.md` documentaba dos fases a la vez y, según su propio encabezado, "no se
sube nunca a una rama remota ni entra a un commit" — es decir, iba a desaparecer con el
tiempo. Pero la Fase 3 ya está construida y en uso parcial, y 21 comentarios del código
(pruebas de sincronización, componentes de `field-app` y `admin-web`, contratos de
`People`) citan sus números de sección como referencia permanente de diseño. Este
documento existe para que esa referencia siga siendo válida cuando `PLAN-FASE-3-4.md`
se retire, sin que nadie tenga que releer una fase ya cerrada para saber por qué una
prueba se llama como se llama.

## 2. Nivel de exigencia en pruebas (heredado de PLAN-FASE-3-4 sec. 2)

> Trasladado literalmente de `PLAN-FASE-3-4.md` sec. 2.2. El número de sección **2.2** se
> conserva sin cambios porque 13 de las 21 citas del código lo usan directamente.

### 2.2 Exigencias especiales de la Fase 3 (sincronización)

La sincronización es donde el proyecto se puede morir en silencio: los bugs no se ven, se
descubren cuando faltan datos. Por eso, además de lo anterior:

- **Toda entidad que se vuelva sincronizable estrena una prueba de push duplicado.**
  Enviar la misma operación dos veces debe producir exactamente un registro. Sin excepción.
- **Suite dedicada `Hato.Modules.Sync.IntegrationTests` con, como mínimo, estos escenarios:**
  1. Push idempotente: misma operación ×3 → un registro, misma respuesta.
  2. Push parcialmente fallido: 10 operaciones, la #4 inválida → las 9 válidas persisten,
     la #4 vuelve con error tipado, el reintento del lote completo no duplica nada.
  3. Push desordenado: el evento hijo llega antes que el padre → se resuelve o se
     encola, nunca se pierde.
  4. Pull incremental: cursor `since` no pierde ni repite registros en el borde exacto
     del timestamp (el caso de dos escrituras en el mismo milisegundo).
  5. Pull con tombstones: un borrado lógico llega al cliente y desaparece de su base.
  6. Reloj del dispositivo desfasado ±2 días → el servidor manda en el orden, el
     `occurred_at` del cliente se conserva como dato declarado.
  7. Lote grande: 500 operaciones en un push → sin timeout, transaccional por lote.
  8. Conflicto en entidad editable: dos dispositivos editan el mismo campo → LWW aplicado
     y **entrada en la bitácora de conflictos**.
  9. Token expirado a mitad del push → reintento tras refresh sin duplicar.
  10. Corte de red simulado a mitad de push → el cliente reintenta y converge.
- **Prueba de convergencia end-to-end** (`field-app` contra API real en Docker): dos
  dispositivos simulados registran offline, sincronizan y terminan con estado idéntico.

## 3. Fase 3 — App móvil offline-first (heredado de PLAN-FASE-3-4 sec. 3)

> Trasladado literalmente de `PLAN-FASE-3-4.md` sec. 3. Los números de bloque **3.A**,
> **3.B** y **3.C** se conservan sin cambios porque 8 de las 21 citas del código los usan
> directamente.

**Objetivo (roadmap):** que los empleados registren desde el potrero, sin señal.
**Criterio de salida:** una semana completa de registros de campo hechos solo desde el
móvil, incluyendo días sin señal, sin pérdida ni duplicación de datos.

**La idea clave del plan:** la Fase 3 **no empieza en React Native**. Empieza en el
backend. Antes de esta fase no existía protocolo de sync, los roles eran un `enum`
compilado (viola Art. 8) y `AnimalEvent.RecordedBy` era un `string` libre, no un usuario
real — con eso no se podía sostener una bitácora de "quién registró qué". Primero se
construyó el suelo; después se caminó sobre él.

### Bloque 3.A — Suelo en el backend (4 ramas)

#### `feature/people-permissions` — permisos finos en base de datos · **estructural**

*Por qué:* el roadmap pide "roles y permisos finos por empleado" y el Art. 8 prohíbe que
los roles vivan como `enum` en el código. Antes, `UserRole` era `{Admin, Registrar,
Veterinarian}` compilado.

Tareas:
1. ADR-0007: modelo de permisos (roles en BD, permisos granulares tipo
   `livestock.animals.write`, asignación N:N, decisión sobre permisos por grupo/lote).
2. Dominio: `Role`, `Permission`, `RolePermission`, `UserRole` (entidad, no enum).
3. Migración con **datos semilla** que reproducen los 3 roles actuales y backfill de los
   usuarios existentes — nadie debe perder acceso al desplegar.
4. Autorización basada en políticas en ASP.NET Core: un `PermissionAuthorizationHandler`
   que resuelve contra BD con caché por request; sustituir los `RequireRole` actuales.
5. Endpoints `GET/POST/PUT /api/v1/roles`, asignación de roles a usuarios.
6. Angular: pantalla de administración de roles y permisos.
7. CI: job `dotnet format --verify-no-changes`.

Pruebas: resolución de permisos (usuario con 2 roles, permiso heredado, permiso revocado),
403 por permiso faltante en cada endpoint tocado, invariante "no se puede quedar sin ningún
Admin" ya existente sigue verde, semilla idempotente.

#### `feature/people-audit-trail` — bitácora de quién registró qué

*Por qué:* requisito del roadmap y de la LOPDP (`LEGAL-ECUADOR.md` sec.5): la app registra
actividad de empleados. Antes, `RecordedBy` era texto libre.

Tareas:
1. `RecordedBy` pasa de `string` a `Guid` (FK a `users`) **conservando** el texto original
   en una columna `recorded_by_label` para no perder historia (Art. 1). Migración con
   backfill por coincidencia de nombre; lo que no casa, queda etiquetado.
2. `ICurrentUser` en SharedKernel + interceptor de EF Core que rellena
   `CreatedBy`/`UpdatedBy` automáticamente en toda `AuditableEntity`.
3. Endpoint `GET /api/v1/audit?userId=&from=&to=` con paginación.
4. Angular: vista de bitácora filtrable.
5. `docs/LEGAL-ECUADOR.md`: nota de la información que se dará por escrito al empleado.

Pruebas: backfill sobre datos legacy, interceptor rellena en create y update, la bitácora
no permite edición ni borrado, filtro por usuario y rango de fechas.

#### `feature/sync-protocol-pull` — el lado de lectura · **estructural**

*Por qué:* el cliente necesita bajarse el estado de la finca y mantenerlo fresco con
tráfico mínimo.

Tareas:
1. ADR-0008: protocolo de sincronización (formato de cursor, granularidad por colección,
   tombstones, ventana máxima, comportamiento ante *reset* del cliente). Este ADR es el
   documento más importante de la fase: escríbelo con calma.
2. Índices `(updated_at)` y `deleted_at` en todas las tablas sincronizables; convención
   verificada por una prueba que recorre el modelo de EF.
3. `GET /api/v1/sync/pull?since=<cursor>&collections=` → `{ collections: {...}, cursor,
   hasMore }`, con paginación por tamaño de página y tombstones incluidos.
4. Filtrado por permisos: el empleado solo baja lo que le corresponde.
5. Colecciones de la v1: `animals`, `animal_identifiers`, `animal_groups`,
   `group_memberships`, `species/breeds/categories`, `medications/items`, `alerts`.

Pruebas: los 4 escenarios de pull de sec.2.2 + filtrado por permisos + cursor estable ante
escrituras concurrentes.

#### `feature/sync-protocol-push` — el lado de escritura · **estructural**

*Por qué:* aquí es donde se pierden o duplican datos. Es la rama más delicada del proyecto.

Tareas:
1. Tabla `sync_operations` (`client_operation_id` UUID **unique**, `user_id`, `device_id`,
   `received_at`, `status`, `result_ref`) — la garantía de idempotencia.
2. `POST /api/v1/sync/push` con lote de operaciones tipadas
   (`recordMilking`, `recordAnimalEvent`, `createAnimal`, `recordBirth`, `moveAnimal`…),
   cada una con su `clientOperationId` y su `occurredAt` declarado por el cliente.
3. Respuesta **por operación**: aceptada / duplicada (devuelve el resultado original) /
   rechazada con Problem Details. Un lote nunca falla entero por una operación mala.
4. Transaccionalidad por operación, orden estable, límite de tamaño de lote configurable.
5. Reglas de negocio idénticas a las de la API normal: el push **no** es una puerta trasera
   que salta validaciones ni el bloqueo por retiro.

Pruebas: **los 10 escenarios de sec.2.2 son obligatorios en esta rama.** Es el único punto
del plan donde se exigió que las pruebas se escribieran antes de mirar siquiera la firma
del endpoint.

### Hito 3.A ✅ — antes de seguir

Con Docker levantado: crear un usuario con rol limitado, hacer `pull`, hacer `push` de un
lote con duplicados desde `curl`, volver a hacer `pull` y verificar que los datos están una
sola vez. Si esto no se siente sólido, no se avanza al móvil.

### Bloque 3.B — La app (5 ramas)

#### `feature/field-app-scaffolding` — esqueleto React Native · **estructural**

1. ADR-0009: React Native (Expo con dev-client vs bare), WatermelonDB, y la lista de
   dependencias npm iniciales — **todas** las dependencias del cliente entran por este ADR
   de una vez, para no pedir aprobación cada semana.
2. `clients/field-app/` con estructura, esquema local de WatermelonDB, navegación.
3. Login con credenciales del backend + **sesión offline**: token cacheado con expiración
   larga y desbloqueo por PIN local; la app arranca y permite registrar sin red.
4. Job de CI para el cliente (typecheck, lint, jest).
5. README del cliente: cómo levantarlo contra el backend local.

Pruebas: arranque sin red, login offline con token cacheado, esquema local se crea y migra.

#### `feature/field-app-sync-engine` — el motor de sincronización en el cliente · **estructural**

1. Outbox local: toda escritura de UI crea una operación con `clientOperationId` (UUID) y
   estado `pendiente`.
2. Sincronizador: pull incremental + push del outbox, con backoff exponencial, detección de
   conectividad, y sincronización oportunista (al recuperar red, al abrir la app, manual).
3. Estados visibles: pendiente / sincronizado / rechazado, con contador siempre a la vista.
4. Manejo de rechazos: la operación no se borra en silencio jamás — va a una bandeja de
   "registros con problema" que el empleado o el admin puede revisar.
5. Migraciones del esquema local versionadas.

Pruebas: cola persiste entre reinicios de la app, reintento tras fallo no duplica,
rechazo del servidor deja el registro visible, convergencia de dos clientes simulados.

#### `feature/field-app-milking` — ordeño en ≤3 toques

*Por qué primero:* es el registro más frecuente (5 AM, todos los días). Si esto no gana al
cuaderno, nada lo hace.

1. Flujo por grupo y por vaca, con la lista precargada del pull.
2. ≤3 toques por registro; botones grandes, alto contraste (guantes y sol).
3. Marca visible de vaca en período de retiro con leche no vendible (dato bajado del pull).
4. Resumen del día y edición del registro **del día en curso** antes de sincronizar
   (después, corrección = evento nuevo, Art. 1).

Pruebas: registro completo en modo avión, conteo de toques del flujo principal, marca de
retiro presente, registro del día persiste tras cerrar la app.

#### `feature/field-app-events` — tratamientos, pesajes y movimientos

1. Tratamiento con medicamento del inventario, dosis, costo y **cálculo local del retiro**
   a partir de los datos sincronizados (el servidor recalcula y manda la verdad).
2. Pesaje y movimiento entre grupos.
3. Selector de animal por arete, nombre o escaneo/búsqueda rápida — asumiendo animales
   **sin** arete también (Art. 3).
4. Adjuntar foto al evento, encolada para subir cuando haya red (usa el mismo outbox).

Pruebas: los tres flujos en modo avión, retiro calculado local coincide con el del servidor
tras sincronizar, foto grande no bloquea el push de los datos.

#### `feature/field-app-births` — partos y crías offline

*Por qué su propia rama:* es el caso donde el cliente **crea una entidad nueva** (la cría)
con un UUID local que después tiene que existir en el servidor con su genealogía intacta.
Es el escenario que más podía doler si salía mal.

1. Registro de parto con creación de la cría (UUID de cliente, Art. 3).
2. Madre obligatoria; padre opcional: animal o pajuela (padre dual).
3. Camadas (múltiples crías) sin `if` por especie: el número viene de configuración.
4. Push que crea animal + evento de parto en una sola operación atómica.

Pruebas: parto offline → sync → la cría existe una sola vez con su genealogía; parto
duplicado por doble toque no crea dos crías; camada de N crías converge completa.

### Hito 3.B ✅

Instalar la app en un teléfono real de gama media, poner el teléfono en modo avión un día
entero, registrar el ordeño real y un par de eventos, y sincronizar al volver a la casa.

### Bloque 3.C — Piloto y cierre (2 ramas)

#### `feature/field-app-pilot-hardening` — lo que el piloto pida

1. Pantalla de estado de sincronización comprensible para un empleado (no para el
   desarrollador).
2. Bandeja de conflictos y rechazos, con resolución manual desde el panel Angular
   (implementa aquí la bitácora de conflictos LWW del ADR-0005 y del ADR-0008).
3. Registro de errores local exportable (sin telemetría en la nube: LOPDP, no recolectar de
   más).
4. Ajustes de UX salidos del piloto con 1–2 empleados reales. Esta rama se puede iterar
   varias veces: es legítimo que sean 2 o 3 PRs pequeños seguidos con el mismo prefijo
   (`feature/field-app-pilot-hardening-2`).

#### `docs/fase-3-cierre` — retrospectiva

Actualizar `ROADMAP.md` (fecha real + retrospectiva de 5 líneas), `ARCHITECTURE.md` (el
protocolo de sync ya construido), `GLOSSARY.md`, `BACKLOG.md`. Etiquetar release desde
`main` vía `release/*` si ya hay uso productivo.

**Cierre de Fase 3 = una semana completa de registros de campo hechos solo desde el móvil,
con días sin señal, sin pérdida ni duplicación.** No antes.

## 4. Estado y retrospectiva

> Trasladado de `docs/ROADMAP.md:33-56` (cierre revertido y los tres defectos que lo
> motivaron) y `docs/ROADMAP.md:109-142` (objetivo, criterio de salida y el estado
> pendiente para cerrar).

**Objetivo:** que los empleados registren desde el potrero, sin señal. La fase más difícil.
**Criterio de salida:** una semana completa de registros de campo hechos solo desde el
móvil, incluyendo días sin señal, sin pérdida ni duplicación de datos.

**Cierre revertido (2026-08-03).** La fase se había marcado como cerrada, pero su criterio
de salida ("una semana completa de registros de campo hechos solo desde el móvil") era
imposible de haber cumplido: `clients/field-app/` no contenía ni una sola pantalla, no
había `App.tsx` ni assets, y `app.json` apuntaba a imágenes inexistentes, de modo que la
app no arrancaba. Una auditoría posterior encontró además tres defectos que habrían
perdido datos en producción sin dar ningún error:

1. **Ninguna entidad de Livestock recibía `created_at`/`updated_at`**: el interceptor de
   auditoría estaba registrado sólo en `PeopleDbContext`. Todas las filas quedaban en
   `0001-01-01`, así que el pull incremental no volvía a entregar nada a un cliente cuyo
   cursor ya hubiera avanzado.
2. **El parto perdía la genealogía en silencio**: la app encolaba `createAnimal` con
   `motherId` en el payload, pero ese comando no tiene ese campo; el JSON se descartaba y
   el servidor respondía `Accepted` con una cría huérfana.
3. **El outbox del cliente vivía en `localStorage`**, una API que no existe en React
   Native: en un teléfono real todo lo pendiente moría al cerrar la app.

Lección: el criterio del Art. 11 —"algo se usa de verdad en la finca"— no admite cierre por
avance parcial, y una suite verde no prueba nada si no ejercita el camino que el usuario
recorre. Las pruebas de sync que existían cubrían 3 de los 10 escenarios obligatorios del
plan, y ninguna tocaba el borde del cursor.

**Trabajo de corrección (rama `fix/fase-3-sync-correctness`):** sellado universal de marcas
de tiempo, cursor `(timestamp, id)` con orden determinista y frontera por colección,
`recordBirth`/`moveAnimal` en el push, idempotencia por reserva previa con índice único,
UUID de cliente aceptado por el servidor (Art. 3), outbox real en WatermelonDB, motor de
sync con backoff y detección de conectividad, y las pantallas de campo. Suite dedicada
`Hato.Sync.IntegrationTests`.

**Pendiente para cerrar (2026-08-03):** de todo lo que quedaba abierto al reabrir la fase,
sólo falta una cosa y es deliberadamente ajena al código: **el piloto real** — una semana
de registros hechos por un empleado desde un teléfono de verdad, sin señal, tal como exige
el criterio de salida. Nada de trabajo de ingeniería puede sustituir esa semana.

Todo lo demás ya está resuelto: borrado lógico real (`Animal.Delete()`, con invariante de
"no eliminar con historia" y filtro de query), filtrado del pull por permisos, la bitácora
de conflictos LWW (`Animal.LastEditedAt` + `GET /api/v1/sync/conflicts`) con una pantalla
en `field-app` que la hace alcanzable en uso real (antes, ningún cliente podía disparar un
conflicto LWW fuera de una prueba) y otra en `admin-web` que la expone, los endpoints de
lectura de especies/razas/categorías que faltaban para poder registrar un animal desde
cualquier cliente con su pantalla correspondiente, los 10 escenarios obligatorios de
sincronización de sec.2.2 de este documento (los últimos dos — token expirado a mitad de
push y corte de red a mitad de un lote — encontraron y corrigieron un bug real en el
cliente), la prueba de convergencia end-to-end con dos dispositivos simulados, y las
pantallas de roles/permisos (con edición), auditoría y sincronización en el panel.
`docs/BACKLOG.md` recoge lo que se dejó fuera a propósito (extender borrado lógico y LWW a
otras entidades, resolución manual de operaciones rechazadas, `ng test` roto en
`admin-web`) y por qué. Sigue pendiente, heredado y sin relación con esta fase: la subida
de fotos (Fase 4) y el ciclo de vida de `Lactation` (Fase 2, nunca implementado).

## 5. Alcance

### Entra

- La especificación de sincronización (sec. 2.2) y los ~11 ramas de ejecución (sec. 3) de
  la Fase 3, trasladadas de `PLAN-FASE-3-4.md`.
- El estado real de cierre de la fase, trasladado de `ROADMAP.md:33-56` y `:109-142`.

### No entra

- La Fase 4 (dinero: ventas, compras, costos), que también vivía en `PLAN-FASE-3-4.md`
  sec. 4. Queda fuera de esta carpeta porque aún no se ejecuta; se traslada en un commit
  posterior de esta misma rama.
- El piloto real en sí — es trabajo de campo, no de documentación, y queda como la única
  tarea abierta de `tasks.md` de esta carpeta.

## 6. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| El piloto real se pospone indefinidamente y la fase queda "casi cerrada" para siempre | `tasks.md` de esta carpeta deja esa única tarea en `[ ]`, visible, sin diluirla entre las demás que ya están hechas |
| Alguien reintroduce lógica de sync sin releer los 10 escenarios obligatorios | Suite dedicada `Hato.Sync.IntegrationTests` los ejercita en CI; `test-e2e.md` de esta carpeta los deja como guion manual también |

## 7. Criterios de aceptación

1. `docs/planes/fase-3/spec.md` existe y su encabezado declara la carpeta como archivada.
2. La sección `2.2` existe con ese número exacto y contiene los 10 escenarios de
   sincronización, verificable con
   `grep -nE '^#+ .*(2\.2|3\.[ABC])' docs/planes/fase-3/spec.md`.
3. Las secciones `3.A`, `3.B` y `3.C` existen con esos números exactos, mismo comando.
4. `docs/planes/PLAN-FASE-3-4.md` no se modifica ni se borra en este commit.
