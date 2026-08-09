# ADR-0025 — Gestión administrativa de grupos en el panel: permiso, TrackingMode mutable y soft-delete

- **Estado:** Propuesto
- **Fecha:** 2026-08-08
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina — bloque 3.5a (transversal: también aplica a Fase 1 cuando los grupos ya estaban mergeados)

## Contexto

Hechos verificables en este repositorio (al 2026-08-08, develop local en `e25adb5`):

- La entidad `AnimalGroup` existe y tiene métodos de dominio no expuestos por la API:
  `Update(name, description, speciesId)` (`src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalGroup.cs:42-50`)
  y `Deactivate()` (`src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalGroup.cs:52-55`).
  `ChangeTrackingMode` **no existe** como método de la entidad.
- `AnimalGroup` hereda `DeletedAt` (`src/Shared/Hato.SharedKernel/AuditableEntity.cs:7-16`) pero
  ninguna operación de dominio lo asigna. BACKLOG.md:9-13 lo registra como deuda.
- Los endpoints existentes (`src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs:1-98`) exponen
  POST/GET/list/byid/summary/live-head-count/events/members. **No hay PUT, ni DELETE, ni
  endpoint para cambiar `TrackingMode`**. Todos cuelgan de `RequireAuthorization()` sin policy
  específica — un gap de seguridad preexistente.
- `TrackingMode` (`src/Modules/Livestock/Hato.Modules.Livestock.Domain/TrackingMode.cs:14-18`)
  es un enum `Individual | Headcount`. ADR-0015 fija su semántica y su motivación (3.5a.1) pero
  no dice nada sobre mutabilidad post-creación. El método `AnimalGroup.Update(...)` no acepta
  `TrackingMode` y no existe `ChangeTrackingMode`.
- El DTO de lista `AnimalGroupDto` (`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/GetAnimalGroupQueries.cs:11-18`)
  no incluye `LiveHeadCount` (sólo está en `AnimalGroupSummaryDto`) ni `SpeciesName`. La
  fórmula `LiveHeadCount = miembros activos − bajas acumuladas` ya existe en
  `GetAnimalGroupSummaryHandler` (`GetAnimalGroupQueries.cs:91-149`).
- El sync ya entrega `animalGroups` bajo `livestock.animals.read` (`src/Hato.Api/Sync/SyncPullQueries.cs:301`).
  El campo `IsDeleted` se calcula de `DeletedAt` (`:360-362`), pero como nadie asigna
  `DeletedAt`, hoy nunca es `true`.
- El panel admin-web (`clients/admin-web/`) tiene 13 rutas y 50+ endpoints consumidos en
  `services/api.service.ts:1-597`. Cero hacia `/api/v1/animal-groups`. La pantalla de
  configuración `/catalogs` (`src/app/components/catalogs/`) tiene 8 pestañas y la nota
  "Una pantalla dedicada" para especies que tampoco existe todavía.
- El permiso `livestock.animals.write` ya cubre creación y edición de `Animal`
  (`src/Hato.Api/Endpoints/AnimalsEndpoints.cs:43,61` con `RequirePermission(LivestockAnimalsWrite)`).
  `AnimalsEndpoints` aplica esa policy a PUT/DELETE; lectura queda abierta a autenticados.

El problema concreto: para administrar grupos desde el panel sin tener que recurrir a
`curl` o SQL directo, falta (a) pantalla, (b) endpoints de update/deactivate/change-mode,
(c) campos derivados (`LiveHeadCount`, `SpeciesName`) en el DTO de lista. La pantalla de
detalle del animal muestra que el animal pertenece a un grupo pero no permite gestionar
el grupo. BACKLOG.md registra esto en la sección "[UI] admin-web: pantallas de catálogos
que faltan" sin asignación de responsable.

La sub-decisión de mutabilidad de `TrackingMode` es independiente de cualquier ADR
vigente: ADR-0015 fija la semántica del modo pero calla sobre cambios post-creación.
La sub-decisión sobre permiso y sobre `IsActive` vs `DeletedAt` tampoco está tomada en
ningún ADR — el patrón de `Animal.Delete()` (tombstone) coexiste con el patrón de
`MortalityCause.Deactivate()` (flag) sin que se haya declarado cuál aplica a grupos.

## Decisión

### 1. Permiso: reusar `livestock.animals.write`. Sin migración RBAC.

El permiso para `POST`, `PUT`, `DELETE`, `PATCH` de `/api/v1/animal-groups` es
`livestock.animals.write` (constante `SystemPermissions.LivestockAnimalsWrite`,
`src/Modules/People/Hato.Modules.People.Domain/UserRole.cs:58`). La lectura queda
bajo `RequireAuthorization()` (consistente con `AnimalsEndpoints`).

**Texto imperativo:** *Los endpoints de escritura de grupos aplican la misma policy
que los endpoints de escritura de animales. No se crea `livestock.animal-groups.manage`.*

### 2. Soft-delete: `IsActive = false`. No se toca `DeletedAt`.

`DELETE /api/v1/animal-groups/{id}` ejecuta `AnimalGroup.Deactivate()`. El método
`Deactivate()` se mantiene idempotente (segundo `Deactivate` no lanza, ya verificado
en tests previos). Se agrega el par simétrico `Activate()` a la entidad y un endpoint
`POST /api/v1/animal-groups/{id}/activate` para revertir.

**Texto imperativo:** *Desactivar un grupo es reversible vía `POST /activate`. No se
asigna `DeletedAt`. El pull sigue calculando `IsDeleted = DeletedAt != null` (siempre
false hasta que un futuro ADR diga lo contrario).*

### 3. TrackingMode mutable con guarda, no inmutable.

Se agrega `AnimalGroup.ChangeTrackingMode(TrackingMode newMode)` con invariantes:

- Si `newMode == TrackingMode`, no-op idempotente.
- Si `!IsActive`, `DomainException` ("No se puede cambiar el modo de un grupo inactivo").

El handler `ChangeAnimalGroupTrackingModeHandler` agrega dos guardas que requieren
consultar otras tablas (la entidad no debe tocar EF):

- Si existen `GroupMembership` activas para el grupo → `DomainException` con mensaje
  "No se puede cambiar el modo: el grupo ya tiene miembros activos. Cree un grupo nuevo."
- Si existen `AnimalEvent` con `GroupId == grupo.Id` → `DomainException` con mensaje
  equivalente para eventos.

El comando se expone vía `PATCH /api/v1/animal-groups/{id}/tracking-mode` con body
`{ "trackingMode": "Individual" | "Headcount" }` y la policy de
`LivestockAnimalsWrite`.

**Texto imperativo:** *TrackingMode es mutable solo mientras el grupo esté vacío
(sin membresías activas ni eventos). Una vez que el grupo tiene hechos registrados,
el cambio se rechaza con 409 y el operador debe desactivar y crear un grupo nuevo.*

**Por qué esta guarda y no inmutabilidad:** en el flujo real, el operario crea el
grupo en el corral, lo deja "vacío unos minutos" mientras llena datos, y descubre
que eligió mal. Permitir el cambio en ese intervalo evita la fricción de "crear
dos veces". El ADR-0015 sec.7 ("trampa del aretado masivo") justifica el límite
estricto post-evento: cambiar retroactivamente `Individual → Headcount` sería
inventar que un evento individual ahora aplica a N cabezas; cambiar `Headcount →
Individual` sería elegir qué animal anónimo recibió el tratamiento. Ambos son
datos sintéticos y el Art. 1 los prohíbe.

**No incluye:** migración de miembros del grupo viejo al nuevo. Cuando el operador
desactiva y crea nuevo, las membresías del viejo **no se mueven automáticamente**.
Es trabajo de campo, no de UI. Si se vuelve un problema real, se aborda en un ADR
sucesor.

### 4. `LiveHeadCount` y `SpeciesName` se calculan en el backend (no N+1).

`AnimalGroupDto` extiende su shape para incluir:

```csharp
record AnimalGroupDto(
    Guid Id, string Name, string? Description,
    Guid? SpeciesId, string? SpeciesName,    // NUEVO: nombre resuelto
    bool IsActive, TrackingMode TrackingMode,
    int LiveHeadCount,                       // NUEVO: cabezas vivas
    List<GroupMembershipDto> Memberships);
```

El handler `GetAnimalGroupsHandler` se reescribe con la fórmula existente
(`GetAnimalGroupQueries.cs:91-149`, sección de resumen) usando **tres queries
agregadas** (no N+1):

1. `SELECT * FROM animal_groups [LEFT JOIN group_memberships]` — existente.
2. `SELECT group_id, COUNT(*) FROM group_memberships WHERE left_at IS NULL GROUP BY group_id`.
3. `SELECT group_id, SUM(affected_count) FROM animal_events WHERE event_type = 'Disposal' GROUP BY group_id`.
4. (Sólo si C9 lo aprueba en el PR) `SELECT id, name FROM species WHERE id IN (...)`.

La fórmula de cabezas vivas se extrae a
`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/LiveHeadCountCalculator.cs`
para que el list query y el summary la compartan y un cambio futuro se haga en un
solo lugar.

### 5. Sin `HasQueryFilter` en `AnimalGroupConfiguration`.

La columna `DeletedAt` existe pero no se agrega `HasQueryFilter(g => g.DeletedAt == null)`
en `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Configurations/AnimalGroupConfiguration.cs`.
Razón: nadie asigna `DeletedAt`, así que el filtro no hace nada. El pull ya usa
`IgnoreQueryFilters()` (`SyncPullQueries.cs:506`); si en el futuro alguien decide
usar tombstone real, ese punto está cubierto.

## Alternativas consideradas

### Permiso: crear `livestock.animal-groups.manage`

- Descartada. Reusar `LivestockAnimalsWrite` es consistente con `AnimalsEndpoints.cs:43,61`
  y evita asimetría read (`LivestockAnimalsRead`) ≠ write (nuevo permiso) sin ganancia.
  Los tres roles actuales (`admin`, `registrar`, `veterinarian`) cubren la UI sin cambios.
- Condición de reversa: si aparece un rol "registrador de campo sin permisos
  administrativos" que deba editar animales pero no gestionar lotes, se reabre este ADR
  y se separa el permiso.

### Soft-delete: tombstone vía `DeletedAt` (estilo `Animal.Delete()`)

- Descartada. El tombstone de `Animal` tiene semántica legal (Art. 4 sobre retiro de
  leche/carne). Desactivar un grupo es "no usar más este lote" — reversible, sin efecto
  sobre eventos históricos ni sobre la leche/carne de nadie. La asimetría con `Animal`
  es deliberada, no por descuido.
- Consistencia con catálogos: `MortalityCause.Deactivate()` y `AdministrationRoute.Deactivate()`
  usan el mismo patrón de flag.

### TrackingMode inmutable post-creación

- Descartada. Pierde el caso real "operario crea el grupo, lo deja vacío unos minutos,
  se da cuenta de que eligió mal". Permitir el cambio solo mientras está vacío es la
  diferencia entre "tuve que migrar" y "funciona". El rechazo post-evento protege la
  semántica del ADR-0015 (no sintetizar datos individuales sobre eventos grupales ni
  viceversa).

### `LiveHeadCount` y `SpeciesName` resueltos en el frontend

- Descartado. Implica más estado en el cliente (cache de species), más invalidación, y
  la misma información llega por la red. El `LEFT JOIN Species` es trivial sobre una
  tabla pequeña (decenas de filas). Consistente con `Animal.speciesName`,
  `Animal.breedName`, `Animal.categoryName` que ya vienen resueltos del backend.

### `HasQueryFilter(g => g.DeletedAt == null)` en EF Core

- Descartado por ahora. `DeletedAt` no se asigna, el filtro no haría nada. Una entrada
  menos en `LivestockDbContextModelSnapshot.cs` por entidad hace más fácil revisar
  migraciones. Se revierte cuando alguien asigne `DeletedAt` con un propósito real.

## Consecuencias

### Positivas

- El panel admin-web cierra un hueco operativo: gestionar grupos deja de exigir `curl`
  o SQL directo. El operador edita nombre, descripción, especie, modo, estado activo
  desde la pantalla `/animal-groups` con permisos del mismo nivel que tiene hoy para
  animales individuales.
- La guarda de TrackingMode protege el principio central del ADR-0015 (no sintetizar
  datos) sin castigar el flujo real de captura temprana.
- El DTO `AnimalGroupDto` enriquecido se consume también en el detalle (no sólo en la
  lista) y queda disponible para cualquier cliente futuro que lo pida. El sync no se
  ve afectado: `SyncAnimalGroupDto` no incluye estos campos derivados.
- La fórmula `LiveHeadCount` queda en un solo lugar (`LiveHeadCountCalculator`),
  reduciendo el riesgo de divergencia entre lista y resumen.

### Negativas / costos

- El permiso compartido con animales implica que el día que aparezca un rol
  "registrador de campo sin administrativos" habrá que reabrir este ADR y separar.
  No es un costo de hoy, pero el precedente queda establecido.
- "Desactivar y crear nuevo" para corregir TrackingMode tras el primer evento deja al
  operador con trabajo manual de re-agregar miembros en el grupo nuevo. Se documenta
  en la UI pero es fricción real. Si la fricción resulta inaceptable en el piloto, se
  aborda con un ADR sucesor (posiblemente una operación de "clonar grupo sin
  eventos").
- 3 queries agregadas por cada carga de lista de grupos. Con <100 grupos es invisible;
  con >500 fincas en producción va a doler. Se documenta en BACKLOG.md la deuda de
  un índice en `AnimalGroup.IsActive` que acelere el filtro default.
- El handler de cambio de TrackingMode hace dos queries (membresías activas, eventos)
  además de la del grupo. Es O(1) en latencia, pero deja una dependencia implícita
  hacia tablas de otros agregados (AnimalEvent). Se mantiene porque la guarda no
  puede vivir en la entidad sin acoplar el dominio a EF.

### Condición de reversa

Este ADR se reabre si:

1. El cliente pide un rol personalizado que distinga "puedo editar animales pero no
   gestionar lotes" — la separación de permisos queda corta y se introduce
   `livestock.animal-groups.manage`.
2. La fricción de "desactivar + crear nuevo + re-agregar miembros" para corregir
   TrackingMode se vuelve inaceptable en el piloto y se necesita una operación de
   clonado de grupo.
3. El listado de grupos supera los 500 grupos activos por finca y la latencia de las
   3 queries agregadas duele. Se mueve `LiveHeadCount` a una columna materializada o
   a un endpoint separado (`/summary?ids=...`) con caché.
4. Alguien propone usar `DeletedAt` con un propósito distinto al soft-delete (ej.
   "ocultar grupo fusionado") y el patrón de flag `IsActive` se queda corto.

### Lo que NO entra en este ADR (deuda rastreable en BACKLOG al cerrar)

- Modal reusable de confirmación (`ConfirmDialogComponent`). El PR frontend usa un
  patrón de botón de dos pasos para la única acción destructiva (desactivar). Si
  aparece una segunda acción destructiva, se aborda con su propio componente.
- Icono "grupo/lote" dedicado en `IconComponent`. El set está cerrado a propósito;
  el PR usa `'tag'` como placeholder con TODO. Se agrega cuando haya diseño.
- Índice en `AnimalGroup.IsActive` para acelerar el filtro default del listado.
  Disparador: deploys con >500 grupos activos.
