# PLAN-ADMIN-WEB-ANIMAL-GROUPS.md — Pantalla `/animal-groups` con CRUD + resumen + TrackingMode mutable

> **Qué es este documento.** El plan operativo para cerrar el hueco "no hay UI para
> configurar lotes en el panel" detectado en BACKLOG.md sec.[UI]. Ejecuta el
> [ADR-0025](../adr/0025-gestion-administrativa-grupos.md) en tres PRs secuenciales.
> No sustituye al ADR (las decisiones viven allá); este archivo dice *cómo* se hace
> el trabajo día a día, qué archivos toca cada PR, qué pruebas exige y cómo se
> mergea el conjunto.

- **Rama Git del plan:** `docs/plan-admin-web-animal-groups` (este archivo).
- **ADR que respalda:** [ADR-0025](../adr/0025-gestion-administrativa-grupos.md).
- **Estado al 2026-08-08:** ADR propuesto en PR #78. Plan listo para arrancar en cuanto
  el ADR se acepte.
- **Compuerta:** ninguna externa (no depende del cliente, no hay conversación previa
  pendiente). El desacople del piloto (ADR-0024) habilita la entrada de UI
  administrativa sin esperar 3.5a.7.

---

## Índice

1. [Por qué existe este plan](#1-por-qué-existe-este-plan)
2. [Decisiones que el ADR fija y este plan ejecuta](#2-decisiones-que-el-adr-fija-y-este-plan-ejecuta)
3. [PR1 — Dominio + Application: comandos, entidad, DTO extendido](#pr1--dominio--application-comandos-entidad-dto-extendido)
4. [PR2 — Endpoints REST + policy de autorización](#pr2--endpoints-rest--policy-de-autorización)
5. [PR3 — Frontend admin-web: pantalla, sidebar, ApiService](#pr3--frontend-admin-web-pantalla-sidebar-apiservice)
6. [Orden y dependencias entre PRs](#orden-y-dependencias-entre-prs)
7. [Cómo se prueba cada PR](#cómo-se-prueba-cada-pr)
8. [Riesgos y deuda que se registra](#riesgos-y-deuda-que-se-registra)

---

## 1. Por qué existe este plan

Tres hechos verificables al 2026-08-08:

- El backend tiene `AnimalGroup` con `Create`/`Get*`/`AddMember`/`RemoveMember`/`RecordGroupEvent`,
  pero **no tiene** `Update`/`Deactivate`/`Activate`/`ChangeTrackingMode` ni los endpoints
  REST correspondientes (`src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs:1-98`).
- El panel admin-web tiene 13 pantallas en `clients/admin-web/`, ninguna para grupos
  (`src/app/app.routes.ts:17-46`). `services/api.service.ts:1-597` no consume `/api/v1/animal-groups`.
- El DTO de lista `AnimalGroupDto` (`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/GetAnimalGroupQueries.cs:11-18`)
  no incluye `LiveHeadCount` ni `SpeciesName`. La fórmula para cabezas vivas ya existe
  en el summary (`:91-149`) pero está duplicada en intención.

BACKLOG.md sec.[UI] lo lista como pendiente. Este plan lo cierra.

## 2. Decisiones que el ADR fija y este plan ejecuta

Referencia: [ADR-0025](../adr/0025-gestion-administrativa-grupos.md). Resumen:

| # | Decisión | Consecuencia en este plan |
|---|---|---|
| 1 | Permiso = `livestock.animals.write` | PR2 aplica `RequirePermission(LivestockAnimalsWrite)` en PUT/DELETE/PATCH. Sin migración RBAC. |
| 2 | Soft-delete = `IsActive = false` | PR1 agrega `Activate()` a la entidad. PR2 expone `DELETE` y `POST /activate`. |
| 3 | TrackingMode mutable con guarda | PR1 agrega `ChangeTrackingMode(newMode)` con invariantes de `IsActive`. PR1 agrega command con guardas de membresías/eventos. PR2 expone `PATCH /tracking-mode`. |
| 4 | `LiveHeadCount` + `SpeciesName` en DTO | PR1 extiende `AnimalGroupDto` y reescribe `GetAnimalGroupsHandler` con 3-4 queries agregadas. Extrae `LiveHeadCountCalculator`. |
| 5 | Sin `HasQueryFilter` en `AnimalGroupConfiguration` | Ningún cambio en `AnimalGroupConfiguration.cs` ni en `LivestockDbContextModelSnapshot.cs`. |

---

## PR1 — Dominio + Application: comandos, entidad, DTO extendido

- **Rama:** `feature/livestock-animal-groups-domain`
- **Origen:** `develop` (después de que el PR #78 del ADR-0025 se mergee; si el reviewer
  no ha mergeado todavía, se puede abrir contra el SHA del develop actual y resolver trivial
  — el ADR no toca código).
- **Propósito:** cerrar el CRUD a nivel de dominio y aplicación. **Sin endpoints nuevos**.
  Sin UI. Todo verde antes de pasar a PR2.

### 3.1 Cambios concretos

**`src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalGroup.cs`**
- Agregar `public void Activate()`: pone `IsActive = true`. Idempotente.
- Agregar `public void ChangeTrackingMode(TrackingMode newMode)`:
  - Si `newMode == TrackingMode`, no-op.
  - Si `!IsActive`, `throw new DomainException("No se puede cambiar el modo de un grupo inactivo.")`.
  - Asigna `TrackingMode = newMode`.
- `Deactivate()` ya existe y se mantiene idempotente (verificar con test nuevo).

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/UpdateAnimalGroupCommand.cs`** (nuevo)
```csharp
public record UpdateAnimalGroupCommand(Guid Id, string Name, string? Description, Guid? SpeciesId) : IRequest<AnimalGroupDto>;

public class UpdateAnimalGroupValidator : AbstractValidator<UpdateAnimalGroupCommand>
{
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
}

public class UpdateAnimalGroupHandler(ILivestockDbContext db, ISpeciesLookup species) : IRequestHandler<UpdateAnimalGroupCommand, AnimalGroupDto>
{
    // 1. Cargar grupo por Id (sin tracking si solo se va a mutar in-place).
    // 2. group.Update(name, description, speciesId).
    // 3. SaveChangesAsync.
    // 4. Devolver AnimalGroupDto proyectado (mismo shape que GetAnimalGroupByIdHandler).
}
```

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/DeactivateAnimalGroupCommand.cs`** (nuevo)
```csharp
public record DeactivateAnimalGroupCommand(Guid Id) : IRequest<Unit>;
public class DeactivateAnimalGroupHandler(ILivestockDbContext db) : IRequestHandler<DeactivateAnimalGroupCommand, Unit>
{
    // Cargar, group.Deactivate(), SaveChanges.
}
```

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/ActivateAnimalGroupCommand.cs`** (nuevo)
- Espejo del anterior pero llama `group.Activate()`.

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/ChangeAnimalGroupTrackingModeCommand.cs`** (nuevo)
```csharp
public record ChangeAnimalGroupTrackingModeCommand(Guid Id, TrackingMode TrackingMode) : IRequest<AnimalGroupDto>;

public class ChangeAnimalGroupTrackingModeValidator : AbstractValidator<ChangeAnimalGroupTrackingModeCommand>
{
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.TrackingMode).IsInEnum();
}

public class ChangeAnimalGroupTrackingModeHandler(ILivestockDbContext db) : IRequestHandler<...>
{
    // 1. Cargar grupo (sin tracking? no, con tracking para mutar).
    // 2. group.ChangeTrackingMode(request.TrackingMode).  // tira si IsActive == false.
    // 3. Verificar guardas externas:
    //    hasActiveMembers = await db.GroupMemberships.AnyAsync(m => m.GroupId == id && m.LeftAt == null, ct);
    //    hasEvents       = await db.AnimalEvents.AnyAsync(e => e.GroupId == id, ct);
    //    si alguna, throw new DomainException("...").
    // 4. SaveChanges.
    // 5. Devolver AnimalGroupDto.
}
```

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/LiveHeadCountCalculator.cs`** (nuevo)
- `public static int Compute(int activeMemberships, int disposed) => Math.Max(0, activeMemberships - disposed);`
- Usado por `GetAnimalGroupSummaryHandler` (refactor) y por el nuevo `GetAnimalGroupsHandler`.

**`src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/GetAnimalGroupQueries.cs`**
- Extender `record AnimalGroupDto(...)` con `int LiveHeadCount` y `string? SpeciesName`.
- Extender `GroupMembershipDto` (sin cambios; ya tiene lo necesario).
- Reescribir `GetAnimalGroupsHandler` con la agregación server-side:
  - Query 1: `AnimalGroups.Include(Memberships)`.
  - Query 2: `GroupMemberships.Where(LeftAt == null).GroupBy(GroupId).Select(Count)`.
  - Query 3: `AnimalEvents.Where(GroupId != null && EventType == Disposal).GroupBy(GroupId).Select(Sum(AffectedCount))`.
  - Query 4 (opcional, ver §3.2): `Species.Where(Id IN [...]).Select(Id, Name)`.
  - Combinar en memoria. Devolver lista con `LiveHeadCount` y `SpeciesName`.
- Refactorizar `GetAnimalGroupSummaryHandler` para usar `LiveHeadCountCalculator.Compute(...)` con los
  mismos valores que ya computa (mantener el comportamiento; sólo extraer la fórmula).

### 3.2 Decisión que se cierra acá, no en el ADR

- **¿`SpeciesName` se calcula con un `LEFT JOIN` server-side o con un `IN (...)` aparte?**
  La query existente en `GetAnimalGroupByIdHandler` ya carga `Species` por separado. Mantener
  la misma forma para la lista: un `IN (...)` aparte. Más limpio de leer y el `DbContext` ya
  lo hace así en otros casos.

### 3.3 Tests

**Unit (xUnit) — `tests/Hato.Modules.Livestock.UnitTests/Domain/AnimalGroupTests.cs`** (extender):
- `Activate_OnInactiveGroup_SetsIsActiveTrue`
- `Activate_OnActiveGroup_IsNoOp`
- `ChangeTrackingMode_SameMode_IsNoOp`
- `ChangeTrackingMode_OnInactive_Throws`
- `ChangeTrackingMode_OnActive_UpdatesMode`  *(la guarda de membresías/eventos vive en el handler, no acá)*
- `Update_WithNullDescription_AllowsNull`
- `Update_WithEmptyName_Throws`
- `Deactivate_Twice_IsIdempotent`

**Unit (xUnit) — nuevos archivos en `tests/Hato.Modules.Livestock.UnitTests/Application/AnimalGroups/`:**
- `UpdateAnimalGroupCommandTests.cs`: validator (nombre vacío, descripción >500), handler (404 si no existe, OK con datos válidos).
- `ChangeAnimalGroupTrackingModeCommandTests.cs`: validator, handler (409 si tiene membresías activas, 409 si tiene eventos, 200 si vacío, 409 si grupo inactivo vía `IsActive=false`).

**Integration (xUnit + Testcontainers) — `tests/Hato.Modules.Livestock.IntegrationTests/Api/AnimalGroupApiTests.cs`** (extender):
- `UpdateGroup_UpdatesAllFields_Returns200`
- `UpdateGroup_WithEmptyName_Returns400_ProblemDetails`
- `UpdateGroup_OnNonexistent_Returns404`
- `DeactivateGroup_IsIdempotent`
- `ActivateGroup_AfterDeactivate_IsActiveTrue`
- `ChangeTrackingMode_OnEmptyGroup_Returns200`
- `ChangeTrackingMode_OnGroupWithActiveMembership_Returns409`
- `ChangeTrackingMode_OnGroupWithEvent_Returns409`
- `GetList_ReturnsLiveHeadCountPerRow`

### 3.4 Migración EF Core

**Ninguna.** No cambia esquema. `DeletedAt`/`UpdatedAt` los hereda `AuditableEntity`.

### 3.5 Verificación manual (backend aislado, sin UI)

Con el servidor corriendo y `psql`:
```bash
# crear grupo
curl -s -X POST localhost:5000/api/v1/animal-groups -H 'Content-Type: application/json' \
  -d '{"name":"PR1 Smoke Test","trackingMode":"Headcount"}' -b cookies.txt
# capturar id

# actualizar
curl -s -X PUT localhost:5000/api/v1/animal-groups/<id> -H 'Content-Type: application/json' \
  -d '{"name":"PR1 Smoke Test Renombrado","description":"smoke"}' -b cookies.txt

# ver lista con LiveHeadCount
curl -s 'localhost:5000/api/v1/animal-groups' -b cookies.txt | jq '.[] | {id, name, liveHeadCount, speciesName}'
```
(Estos endpoints **no están expuestos** hasta el PR2 — el smoke de PR1 es unit/integration.)

### 3.6 Qué NO incluye este PR

- Endpoints HTTP. Llegan en PR2.
- Cambios en la entidad que no sean `Activate`/`ChangeTrackingMode`.
- UI.
- Sync pull / push. `SyncAnimalGroupDto` queda igual (los campos derivados no se sincronizan).

---

## PR2 — Endpoints REST + policy de autorización

- **Rama:** `feature/livestock-animal-groups-endpoints`
- **Origen:** `develop` (post-merge de PR1).
- **Propósito:** exponer los 4 verbos nuevos y aplicar la policy de autorización.

### 4.1 Cambios concretos

**`src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs`** — agregar al grupo `MapGroup("/api/v1/animal-groups").WithTags("AnimalGroups").RequireAuthorization()`:

```csharp
group.MapPut("/{id:guid}", async (Guid id, UpdateAnimalGroupCommand command, ISender sender) =>
{
    await sender.Send(command with { Id = id });
    return Results.NoContent();
}).RequirePermission(SystemPermissions.LivestockAnimalsWrite);

group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
{
    await sender.Send(new DeactivateAnimalGroupCommand(id));
    return Results.NoContent();
}).RequirePermission(SystemPermissions.LivestockAnimalsWrite);

group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
{
    await sender.Send(new ActivateAnimalGroupCommand(id));
    return Results.NoContent();
}).RequirePermission(SystemPermissions.LivestockAnimalsWrite);

group.MapPatch("/{id:guid}/tracking-mode",
    async (Guid id, ChangeAnimalGroupTrackingModeRequest body, ISender sender) =>
{
    await sender.Send(new ChangeAnimalGroupTrackingModeCommand(id, body.TrackingMode));
    return Results.NoContent();
}).RequirePermission(SystemPermissions.LivestockAnimalsWrite);
```

Donde `ChangeAnimalGroupTrackingModeRequest` es un record local:
```csharp
public record ChangeAnimalGroupTrackingModeRequest(TrackingMode TrackingMode);
```

**Endurecer el `POST /` existente** (gap de seguridad preexistente):
```csharp
group.MapPost("/", /* existente */ ).RequirePermission(SystemPermissions.LivestockAnimalsWrite);
```

### 4.2 Tests

**Integration (xUnit) — extender `tests/Hato.Modules.Livestock.IntegrationTests/Api/AnimalGroupApiTests.cs`:**
- `PutGroup_OnNonexistent_Returns404` (ya estaba, se mantiene).
- `DeleteGroup_DeactivatesGroup_Returns204`
- `DeleteGroup_OnNonexistent_Returns404`
- `DeleteGroup_Twice_IsIdempotent`
- `PostActivate_AfterDeactivate_Returns204`
- `PostActivate_OnActiveGroup_IsIdempotent`
- `PatchTrackingMode_OnEmptyGroup_Returns204`
- `PatchTrackingMode_OnGroupWithMembership_Returns409_ProblemDetails`
- `PatchTrackingMode_OnGroupWithEvent_Returns409_ProblemDetails`
- `PatchTrackingMode_OnInactiveGroup_Returns400_ProblemDetails`
- `PutDeletePatchPostActivate_WithoutLivestockAnimalsWrite_Returns403` (crear usuario de prueba sin el permiso)
- `PostCreate_WithoutLivestockAnimalsWrite_Returns403`  *(cubre el endurecimiento del POST)*

### 4.3 Migración EF Core

**Ninguna.**

### 4.4 Verificación manual end-to-end backend

```bash
# Setup: login admin
curl -c cookies.txt -X POST localhost:5000/api/v1/people/auth/login \
  -H 'Content-Type: application/json' -d '{"username":"admin","password":"..."}'

# Crear grupo (POST ahora exige LivestockAnimalsWrite; admin lo tiene).
curl -b cookies.txt -X POST localhost:5000/api/v1/animal-groups \
  -H 'Content-Type: application/json' -d '{"name":"PR2 Manual","trackingMode":"Individual"}'

# Update
curl -b cookies.txt -X PUT localhost:5000/api/v1/animal-groups/<id> \
  -H 'Content-Type: application/json' -d '{"name":"PR2 Manual v2"}'

# Desactivar
curl -b cookies.txt -X DELETE localhost:5000/api/v1/animal-groups/<id>

# Reactivar
curl -b cookies.txt -X POST localhost:5000/api/v1/animal-groups/<id>/activate

# TrackingMode (grupo recién creado, sin membresías ni eventos)
curl -b cookies.txt -X PATCH localhost:5000/api/v1/animal-groups/<id>/tracking-mode \
  -H 'Content-Type: application/json' -d '{"trackingMode":"Headcount"}'

# Login como usuario SIN LivestockAnimalsWrite y probar el 403
curl -c cookies-noedit.txt -X POST ... (otro usuario)
curl -b cookies-noedit.txt -X PUT localhost:5000/api/v1/animal-groups/<id>  # → 403
```

### 4.5 Qué NO incluye este PR

- UI.
- Sync. (Los campos `LiveHeadCount`/`SpeciesName` no se sincronizan; el pull sigue entregando `SyncAnimalGroupDto` igual que antes.)
- Cambios en la lógica de dominio. La entidad y los handlers quedaron cerrados en PR1.

---

## PR3 — Frontend admin-web: pantalla, sidebar, ApiService

- **Rama:** `feature/admin-web-animal-groups-screen`
- **Origen:** `develop` (post-merge de PR2; el backend debe estar estable antes de cablear la UI).
- **Propósito:** entregar la pantalla `/animal-groups` consumiendo los endpoints nuevos.

### 5.1 Cambios concretos

**`clients/admin-web/src/app/services/api.service.ts`** — agregar DTOs y métodos (ver §5.3 contratos).

**`clients/admin-web/src/app/app.routes.ts`** — agregar 3 rutas bajo el mismo permissionGuard:
```typescript
{
  path: 'animal-groups',
  loadComponent: () => import('./components/animal-groups-list/animal-groups-list.component').then(m => m.AnimalGroupsListComponent),
  canActivate: [authGuard, permissionGuard('livestock.animals.write')]
},
{
  path: 'animal-groups/new',
  loadComponent: () => import('./components/animal-group-create/animal-group-create.component').then(m => m.AnimalGroupCreateComponent),
  canActivate: [authGuard, permissionGuard('livestock.animals.write')]
},
{
  path: 'animal-groups/:id',
  loadComponent: () => import('./components/animal-group-detail/animal-group-detail.component').then(m => m.AnimalGroupDetailComponent),
  canActivate: [authGuard, permissionGuard('livestock.animals.write')]
},
```
(Usar `loadComponent` con lazy loading si la app lo soporta — verificar convención en `app.routes.ts`. Si las 13 rutas existentes son eager, seguir igual.)

**`clients/admin-web/src/app/app.component.html`** — agregar entrada en el sidebar bajo "Administración":
```html
@if (auth.hasPermission('livestock.animals.write')) {
  <a routerLink="/animal-groups" class="nav-link" aria-label="Administración de lotes y grupos">
    <app-icon name="tag" />
    <span>Lotes y Grupos</span>
  </a>
}
```

**Tres componentes nuevos** (carpeta por componente, siguiendo convención `roles-management/`):
- `animal-groups-list/` — tabla con `CatalogTableComponent`, columnas: Nombre, Especie (vía `speciesName` server-side), Modo (texto derivado), Cabezas vivas (vía `liveHeadCount`), Activo (boolean). Acciones: Editar → navegar a detalle, Desactivar/Reactivar con confirmación de dos pasos.
- `animal-group-create/` — form con `[(ngModel)]`: nombre, descripción, especie (dropdown cargado de `getSpecies()`), radio de TrackingMode con texto de ayuda (ver §5.4). Submit → POST → navegar al detalle del nuevo grupo.
- `animal-group-detail/` — vista con tres secciones: encabezado (nombre, descripción, especie, modo, badges de activo/inactivo), Resumen (5 tarjetas: cabezas vivas, última vacunación, última baja, último tratamiento, cabezas afectadas por diagnóstico), Miembros (lista), Eventos (lista). Edición inline para nombre/descripción/especie; cambio de TrackingMode con flujo separado (advertencia si tiene membresías/eventos); desactivar/reactivar con confirmación.

### 5.2 Patrón a imitar

- **Lista**: `clients/admin-web/src/app/components/catalogs/catalogs.component.ts` (signals + `CatalogTableComponent`).
- **Detalle**: `clients/admin-web/src/app/components/animal-detail/animal-detail.component.ts` (route param, carga paralela con `forkJoin`).
- **CRUD inline**: `clients/admin-web/src/app/components/roles-management/roles-management.component.ts` (edición inline con `successMessage`/`errorMessage`).

Forms con `[(ngModel)]` y `FormsModule`. **No** usar Reactive Forms (no es convención del proyecto).
Errores con `err?.error?.detail || 'No se pudo X.'` (patrón `RolesManagementComponent`).

### 5.3 Contratos TypeScript (resumen)

```typescript
export interface AnimalGroupDto {
  id: string;
  name: string;
  description?: string | null;
  speciesId?: string | null;
  speciesName?: string | null;   // PR1 server-side
  isActive: boolean;
  trackingMode: 'Individual' | 'Headcount';
  liveHeadCount: number;          // PR1 server-side
  memberships: GroupMembershipDto[];
}

export interface AnimalGroupSummaryDto { /* idem backend */ }

export interface CreateAnimalGroupRequest {
  name: string;
  description?: string | null;
  speciesId?: string | null;
  trackingMode?: 'Individual' | 'Headcount';
}

export interface UpdateAnimalGroupRequest {
  name: string;
  description?: string | null;
  speciesId?: string | null;
}

export interface ChangeTrackingModeRequest {
  trackingMode: 'Individual' | 'Headcount';
}

export interface GroupMembershipDto {
  id: string; animalId: string;
  joinedAt: string; leftAt?: string | null; isActive: boolean;
}
```

Métodos nuevos en `ApiService`:
```typescript
getAnimalGroups(includeInactive?: boolean): Observable<AnimalGroupDto[]>
getAnimalGroupById(id: string): Observable<AnimalGroupDto>
getAnimalGroupSummary(id: string): Observable<AnimalGroupSummaryDto>
createAnimalGroup(req: CreateAnimalGroupRequest): Observable<{ id: string }>
updateAnimalGroup(id: string, req: UpdateAnimalGroupRequest): Observable<void>
deactivateAnimalGroup(id: string): Observable<void>
activateAnimalGroup(id: string): Observable<void>
changeAnimalGroupTrackingMode(id: string, mode: 'Individual' | 'Headcount'): Observable<void>
```

### 5.4 Texto de ayuda para TrackingMode (en español)

> **Modo de seguimiento**
>
> Elegí cómo el sistema va a tratar a los miembros de este lote. Esto define qué clase de eventos podés registrar y cómo se atribuyen.
>
> ◯ **Individual**
> El sistema conoce a cada miembro por separado (arete, SIFAE, RFID). Use este modo para ganado identificado: los eventos pueden ser por animal o por lote, y el linaje se mantiene.
>
> ◯ **Por conteo (sin identificar)**
> El sistema solo sabe cuántas cabezas hay, no cuáles. Use este modo para lotes sin identificación (lechones al destete, pollos de engorde, terneros sin arete). Los eventos se registran sobre el lote completo.
>
> **Atención:** una vez que el grupo tenga miembros activos o eventos registrados, no se puede cambiar el modo. Si te equivocaste, desactivá este grupo y creá uno nuevo con el modo correcto.

(El "Atención:" no es un emoji; si el proyecto rechaza algún glyph, ajustar a `<app-icon name="alert" />` o texto plano.)

### 5.5 Tests (vitest)

Specs por componente:
- `animal-groups-list.component.spec.ts` — render vacío, render con datos, acción Editar navega, acción Desactivar llama API y recarga, `showWhen` correcto por fila, no muestra acciones que el permiso no permite.
- `animal-group-create.component.spec.ts` — submit deshabilitado sin nombre, submit válido llama API y navega, manejo de error 400 muestra `errorMessage`.
- `animal-group-detail.component.spec.ts` — `ngOnInit` carga group + summary, 404 marca `notFound`, `saveEdit` llama API y recarga, `confirmDeactivate` llama API, `requestTrackingModeChange` con mismo modo no-op, con membresías activas muestra warning.

Cada spec usa `TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: apiStub }] })` siguiendo `catalogs.component.spec.ts:13-22`.

### 5.6 Verificación manual end-to-end (con backend corriendo)

1. Login como `admin` → aparece "Lotes y Grupos" en el sidebar bajo "Administración".
2. Click → `/animal-groups` → tabla con grupos existentes.
3. Click "Nuevo grupo" → completar form (nombre, especie del dropdown, modo **Por conteo**) → Crear.
4. Redirige a `/animal-groups/:id` → muestra resumen con `liveHeadCount = 0`.
5. Editar nombre → guardar → la lista refleja el cambio.
6. "Cambiar modo" → Individual → guardar → DTO actualizado.
7. Agregar miembro vía `POST /members` (curl). Intentar cambiar modo otra vez → warning + al confirmar 409.
8. "Desactivar" → segundo click → grupo inactivo, ya no aparece en la lista por defecto.
9. Aparece "Reactivar" → vuelve a la lista activa.
10. Logout, login como `veterinarian` → intentar `/animal-groups` → redirige al dashboard; el link no aparece en el sidebar.

### 5.7 Qué NO incluye este PR

- Modal reutilizable de confirmación (botón de dos pasos basta para esta única acción).
- Icono dedicado "grupo/lote" (usa `'tag'` como placeholder con TODO).
- Index en `AnimalGroup.IsActive` (deuda, ver §8).
- Sub-página `/animal-groups/:id/edit` separada (la edición ocurre inline dentro del detalle).
- Edición del `TrackingMode` en el mismo formulario que el nombre/descripción (flujo separado, con su propia advertencia).

---

## Orden y dependencias entre PRs

```
PR0 (ADR-0025)  ──►  PR1 (domain)  ──►  PR2 (endpoints)  ──►  PR3 (frontend)
   docs/                feature/            feature/              feature/
```

- **PR0 → PR1**: PR1 no compila si PR0 no está mergeado (las invariantes y la forma del DTO
  referencian el ADR; sin él un reviewer podría pedir cambios incompatibles con PR1).
  **Bloqueante estricto.** Esperar merge.
- **PR1 → PR2**: PR2 usa los handlers y DTOs creados en PR1. **Bloqueante estricto.** PR2 debe
  partir de develop con PR1 mergeado.
- **PR2 → PR3**: PR3 consume los endpoints que PR2 expone. **Bloqueante estricto.** Pero: la UI
  puede desarrollarse con un mock del backend mientras PR2 está en revisión, si el equipo
  decide paralelizar. **Decisión por defecto: secuencial.** Si se paraleliza, el merge de PR3
  se hace tras el merge de PR2 y un rebase.
- **Cualquier PR**: ninguno de los tres toca `develop` directamente. Todos vía PR + revisión.

---

## Cómo se prueba cada PR

Resumen; el detalle está en cada sección.

| PR | Unit (xUnit) | Integration (xUnit + Testcontainers) | Vitest | Manual |
|---|---|---|---|---|
| PR1 | 8 nuevos en `AnimalGroupTests`; 2 archivos nuevos para Update/ChangeMode | 9 escenarios en `AnimalGroupApiTests` | — | (sin UI, smoke con `psql`/`dotnet test`) |
| PR2 | — | 11 escenarios nuevos (incluyendo 403 sin permiso) | — | `curl` con cookies de sesión |
| PR3 | — | — | 3 specs | E2E con admin/veterinarian (10 pasos) |

**Disciplina TDD (PR1 y PR2):** todo handler/command nuevo tiene su test escrito **primero**, rojo,
luego la implementación. La skill `test-driven-development` aplica con su iron law: nada de
"escribir tests al final para verificar".

**Disciplina verification-before-completion:** ningún PR se marca completo sin:
- `dotnet test` exit 0 con los conteos esperados.
- `dotnet build` exit 0.
- `ng test --watch=false` exit 0 (PR3).
- `ng build` exit 0 (PR3).
- Sin warnings de compilación nuevos.

**PostgreSQL de tests:** `Hato.TestSupport` resuelve con Testcontainers por defecto; si Docker no
levanta contenedores, usar `HATO_TEST_POSTGRES` con el servidor local. AGENTS.md:5 lo deja sentado.

---

## Riesgos y deuda que se registra

### Riesgos durante implementación

| Riesgo | Mitigación |
|---|---|
| El cambio en `GetAnimalGroupsHandler` (3 queries agregadas) introduce N+1 si se rompe el batching | Test de integration que cuenta queries con `DbCommandInterceptor`. PR1 §3.3 escenario "lista 50 grupos, ≤4 queries". |
| El endurecimiento del `POST /animal-groups` rompe consumidores existentes | PR2 §4.2 test "POST sin permiso devuelve 403". Si admin es el único caller conocido, el riesgo es bajo. |
| El form de TrackingMode en el frontend excede los 3-4 toques de la promesa del plan porcino | El form es admin-web (no field-app); la promesa de toques es del árbol móvil, no del panel. Sin mitigación necesaria. |
| El `IconComponent` no tiene icono "grupo" y el set está cerrado | Usar `'tag'` con TODO. Deuda en BACKLOG. |

### Deuda del backlog que este plan **agrega**

Tres ítems al cerrar el PR3 (no bloquean este plan):

1. **`[UI]` ConfirmDialogComponent reutilizable.** El patrón de botón de dos pasos cubre la única acción destructiva de este PR. Disparador: cuando aparezca la segunda acción destructiva en admin-web (ej. "Eliminar rol con asignaciones").

2. **`[UI]` Icono "grupo/lote" dedicado en `IconComponent`.** PR3 usa `'tag'` como placeholder con TODO. Disparador: cuando un diseñador provea el icono.

3. **`[perf]` Índice en `AnimalGroup.IsActive`.** El listado default filtra por `is_active = true`. Con <100 grupos es invisible; con >500 fincas en producción va a doler. Disparador: deploys con >500 grupos activos.

### Deuda del backlog que este plan **paga**

- El ítem "[UI] admin-web: pantallas de catálogos que faltan" (`BACKLOG.md` sec.[UI]) queda parcialmente satisfecho: animalGroups tiene pantalla dedicada. Los catálogos puros (especies, mortalidad, inventario) **siguen pendientes** y mantienen su disparador original.

### Lo que NO se hace

- LWW para `AnimalGroup`. Sigue como en BACKLOG.md:15-19 (deuda rastreable). La pantalla nueva hace PUT directo (síncrono, en línea) — no declara `knownUpdatedAt`. Consistente con el patrón documentado en `ARCHITECTURE.md` sec."Conflictos LWW en entidades editables" para `Animal`.
- Borrado lógico real (`DeletedAt`). El ADR-0025 explícitamente lo descarta. El `DeletedAt` de la tabla sigue sin asignarse.
- Permiso `livestock.animal-groups.manage`. El ADR-0025 lo descarta. Si la condición de reversa dispara, se reabre el ADR.

---

*Listo para arrancar cuando el ADR-0025 se acepte. PR1 espera el SHA de develop con el ADR mergeado.*
