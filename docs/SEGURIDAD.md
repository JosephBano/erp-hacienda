# SEGURIDAD.md — Modelo de seguridad del proyecto HATO

> Este documento responde **¿cómo se protege y qué está expuesto?** (`docs/DOCUMENTACION.md`
> sec. 2). Se actualiza cuando cambia auth, permisos, o se halla un hueco nuevo — es también
> el disparador que dispara la revisión de este archivo cada vez que nace un endpoint
> (`DOCUMENTACION.md` sec. 3).
>
> **Método:** este documento se escribió auditando el código — no resumiendo
> `docs/adr/0007-modelo-permisos-bd.md`, `docs/adr/0008-protocolo-sincronizacion.md` ni
> `docs/LEGAL-ECUADOR.md`. Esos tres documentos dicen qué se decidió; este dice qué quedó
> realmente implementado, verificado línea por línea contra `src/Hato.Api/` el
> **2026-08-16**. Donde el código no confirma algo, este documento dice "no verificado" en
> vez de asumirlo.
>
> Alcance: la API (`src/Hato.Api/`), no los clientes (`clients/admin-web`,
> `clients/field-app`) salvo donde su comportamiento decide algo del lado servidor (p. ej.
> dónde guardan el JWT).

---

## 1. Modelo de autenticación

### Emisión

`POST /api/v1/people/auth/login` (`src/Hato.Api/Endpoints/PeopleEndpoints.cs:53-57`,
`AllowAnonymous`) recibe `{ email, password }`. El handler
(`src/Modules/People/Hato.Modules.People.Application/Auth/LoginCommand.cs`) normaliza el
correo (`Trim().ToLowerInvariant()`), busca el usuario con sus roles y permisos precargados,
y rechaza con `UnauthorizedAccessException` si el usuario no existe, está inactivo, o la
contraseña no verifica. **El mensaje de error no distingue "usuario no existe" de
"contraseña incorrecta"** (ambos casos devuelven el mismo texto), lo que evita enumeración
de correos por fuerza bruta del mensaje de error — aunque no hay límite de intentos (ver
sec. 6).

**Contraseñas:** `PasswordHasher`
(`src/Modules/People/Hato.Modules.People.Domain/PasswordHasher.cs`) usa **PBKDF2-HMAC-SHA256**
con **100.000 iteraciones**, sal de 16 bytes generada por `RandomNumberGenerator` (CSPRNG),
clave derivada de 32 bytes. El hash se serializa como `"{iteraciones}.{saltBase64}.{keyBase64}"`
— el número de iteraciones viaja con el hash, así que subir el factor de trabajo en el futuro
no invalida los hashes ya emitidos. La comparación en `Verify()` usa
`CryptographicOperations.FixedTimeEquals` (tiempo constante, resistente a timing attack).

**Token de acceso (JWT):** `JwtTokenGenerator`
(`src/Modules/People/Hato.Modules.People.Infrastructure/JwtTokenGenerator.cs`) firma con
`HmacSha256` (`SecurityAlgorithms.HmacSha256`, clave simétrica). Claims: `sub` (user id),
`email`, `Name`, un claim `role` por cada rol asignado, y un claim `permission` por cada
permiso que ese rol otorga (los permisos van *embebidos en el token*, no solo resueltos en
cada request — ver sec. 2 sobre las consecuencias de esto). Vigencia:
`Jwt:ExpiryMinutes`, **480 minutos (8 horas)** por defecto
(`src/Hato.Api/appsettings.json`, `JwtOptions.cs:11`).

**Clave de firma:** `Jwt:SigningKey` se lee de configuración; si está vacía y el entorno
**no** es `Production`, `PeopleModule.cs:53-55` genera una clave aleatoria de 32 bytes en
memoria al arrancar. En `Production`, `.Validate()` (`PeopleModule.cs:66-70`) exige una clave
de al menos 32 caracteres o el proceso **no arranca** (`ValidateOnStart()`). Ver sec. 6 sobre
por qué esta protección no se ejercita hoy en el despliegue real.

### Refresco

`POST /api/v1/people/auth/refresh` (`AllowAnonymous`) recibe el refresh token crudo, lo
hashea con `SHA256` (`RefreshToken.HashToken`, `src/Modules/People/Hato.Modules.People.Domain/RefreshToken.cs:50-57`)
y busca el hash en `people.refresh_tokens`. Solo el hash vive en base de datos — el token
crudo (64 bytes aleatorios de `RandomNumberGenerator`, codificados en Base64) nunca se
persiste. Si el token es válido y activo (`IsActive` = no revocado y no expirado), el handler
**rota**: crea un nuevo `RefreshToken`, revoca el viejo apuntando al hash del nuevo
(`ReplacedByTokenHash`), y emite un JWT nuevo. Vigencia del refresh token: **30 días**
(`RefreshToken.Create`, parámetro `expiryDays = 30`).

**No verificado:** el código no implementa detección de reuso de un refresh token ya
revocado como señal de robo (patrón común: si un token revocado se presenta de nuevo,
revocar toda la cadena del usuario). `RevokeRefreshTokenCommandHandler` revoca solo si el
token sigue activo; presentar un token ya revocado no dispara ninguna alarma ni revoca otros
tokens del usuario.

### Cierre de sesión

`POST /api/v1/people/auth/revoke` (`AllowAnonymous`) hashea el token recibido y, si existe y
está activo, lo marca revocado. **No hay endpoint para revocar todos los refresh tokens de
un usuario a la vez** (p. ej. al desactivar un empleado) — `DeactivateUserCommand` desactiva
el usuario (`IsActive = false`, lo que bloquea el próximo login y el próximo refresh porque
ambos handlers chequean `user.IsActive`), pero **no invalida el JWT ya emitido**: ese token
sigue siendo válido criptográficamente hasta su expiración natural (hasta 8 horas) aunque el
usuario ya esté desactivado, porque la validación del JWT (`OnTokenValidated` en
`PeopleModule.cs`) no vuelve a consultar `IsActive` en cada request — solo lo hace el
handler de permisos cuando cae al fallback de base de datos (sec. 2).

### Tabla `people.refresh_tokens`

Migración `20260802164651_AddRefreshTokens.cs`. Columnas relevantes:
`user_id`, `token_hash` (único), `expires_at`, `revoked_at`, `replaced_by_token_hash`,
`created_by_ip`. `created_by_ip` se declara en el dominio (`RefreshToken.cs:17`) pero
**no verificado**: no se confirmó en este audit si algún endpoint lo puebla realmente (el
`LoginCommandHandler` llama `RefreshToken.Create(user.Id)` sin pasar `createdByIp`, así que
queda `null` en el flujo de login).

---

## 2. Modelo de autorización

### Permisos en base de datos (ADR-0007)

`roles`, `permissions`, `role_permissions`, `user_roles` — RBAC granular, sin jerarquía
implícita entre roles. `SystemRoles`
(`src/Modules/People/Hato.Modules.People.Domain/UserRole.cs:39-44`) declara tres roles semilla:
`admin`, `registrar`, `veterinarian`. `SystemPermissions` (mismo archivo, líneas 49-90)
declara **21 códigos de permiso** en 7 módulos (People, Livestock, Production, Inventory,
Breeding, Tasks, Settings).

**El rol `admin` tiene bypass total**: `PermissionAuthorizationHandler`
(`src/Modules/People/Hato.Modules.People.Infrastructure/Authorization/PermissionAuthorization.cs:27-32`)
concede cualquier permiso a quien tenga el rol `admin` (por claim de rol en el JWT o por rol
en BD), sin mirar `role_permissions`. La comparación es case-insensitive.

**Resolución de permisos, dos vías:**
1. Claim `permission` embebido en el JWT (emitido en login/refresh) — si el permiso pedido
   está ahí, pasa sin tocar la base de datos.
2. Si no está en el JWT, consulta `UserRoles` en BD con `AsNoTracking()`, filtrando
   `User.IsActive` (esta consulta sí respeta desactivación en el momento en que se ejecuta).

**Consecuencia directa de la vía 1 sobre revocación (ligado a sec. 1):** como los permisos
viajan en el JWT y la vía 1 nunca toca la base de datos, revocar un rol o desactivar un
usuario **no tiene efecto sobre un JWT ya emitido** mientras ese JWT siga vigente (hasta 8
horas) — el permiso seguía en el claim y la vía 1 nunca cae a la vía 2 para ese código de
permiso. La vía 2 (que sí respeta `IsActive`) solo se ejecuta para permisos que **no**
estaban en el JWT al emitirlo — p. ej. un rol asignado *después* del login.

### Hallazgo central: 7 de 21 permisos declarados nunca se usan en ningún endpoint

Se buscó `SystemPermissions.<Código>` en todo `src/` para cada uno de los 21 permisos
declarados. **Siete no aparecen en ningún archivo de `Endpoints/`:**

| Permiso declarado | Módulo que debería exigirlo | Estado |
|---|---|---|
| `BreedingEventsRecord` | Breeding (`BreedingEndpoints.cs`) | Nunca referenciado |
| `BreedingEventsRead` | Breeding (`BreedingEndpoints.cs`) | Nunca referenciado |
| `ProductionMilkingRecord` | Production (`MilkingEndpoints.cs`) | Nunca referenciado |
| `ProductionMilkingRead` | Production (`MilkingEndpoints.cs`) | Nunca referenciado |
| `TasksManage` | Tasks (`TasksEndpoints.cs`) | Nunca referenciado |
| `TasksRead` | Tasks (`TasksEndpoints.cs`) | Nunca referenciado |
| `PeopleUsersRead` | People (lectura de usuarios) | Nunca referenciado (`PeopleUsersManage` cubre lectura y escritura) |

El efecto práctico: **todo el módulo Breeding, todo Milking y todo Tasks/Alerts están
abiertos a cualquier usuario autenticado**, sin distinción de rol — `RequireAuthorization()`
de grupo es la única puerta. ADR-0007 diseñó el control fino ("un operador de ordeño debe
poder registrar leche sin acceso a reportes financieros... un veterinario requiere permisos
sobre salud pero no sobre facturación") pero para estos tres módulos ese control **no se
conectó**. Ver sec. 3 de la tabla endpoint→permiso para el detalle fila por fila, y sec. 7
para el hueco documentado en `BACKLOG.md`.

### Tabla endpoint → permiso exigido

Los 19 archivos de `src/Hato.Api/Endpoints/` están representados. "Solo auth" significa
`RequireAuthorization()` de grupo sin política de permiso adicional — cualquier usuario
autenticado y activo, sin importar rol, puede llamarlo. `AllowAnonymous` significa sin
autenticación.

#### `AdministrationRoutesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/administration-routes` | Solo auth |
| `POST /api/v1/administration-routes` | `livestock.treatments.configure` |
| `DELETE /api/v1/administration-routes/{id}` | `livestock.treatments.configure` |
| `POST /api/v1/administration-routes/{id}/activate` | `livestock.treatments.configure` |
| `PATCH /api/v1/administration-routes/{id}/label` | `livestock.treatments.configure` |

#### `AnimalCategoriesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/animal-categories` | Solo auth |
| `POST /api/v1/animal-categories` | `livestock.categories.manage` |

#### `AnimalEventsEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/animals/{animalId}/events` | **Solo auth** — sin `livestock.animals.write` |
| `GET /api/v1/animals/{animalId}/events` | Solo auth |
| `GET /api/v1/animals/{animalId}/withdrawal-periods` | Solo auth |

Registrar un evento sanitario/productivo sobre un animal individual (tratamiento, vacuna,
muerte, pesaje) no exige ningún permiso más allá de estar autenticado. Comparar con
`AnimalGroupsEndpoints.cs` abajo: el mismo tipo de evento a nivel de grupo **sí** exige
`livestock.animals.write`. La asimetría es del código, no de una decisión documentada.

#### `AnimalGroupsEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/animal-groups/{id}/events` | `livestock.animals.write` |
| `GET /api/v1/animal-groups/{id}/events` | Solo auth |
| `GET /api/v1/animal-groups/{id}/live-head-count` | Solo auth |
| `POST /api/v1/animal-groups` | `livestock.animals.write` |
| `PUT /api/v1/animal-groups/{id}` | `livestock.animals.write` |
| `DELETE /api/v1/animal-groups/{id}` | `livestock.animals.write` |
| `POST /api/v1/animal-groups/{id}/activate` | `livestock.animals.write` |
| `PATCH /api/v1/animal-groups/{id}/tracking-mode` | `livestock.animals.write` |
| `GET /api/v1/animal-groups` | Solo auth |
| `GET /api/v1/animal-groups/{id}` | Solo auth |
| `GET /api/v1/animal-groups/{id}/summary` | Solo auth |
| `POST /api/v1/animal-groups/{id}/members` | `livestock.animals.write` |
| `DELETE /api/v1/animal-groups/{id}/members/{animalId}` | `livestock.animals.write` |

#### `AnimalsEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/animals` (registrar) | **Solo auth** — sin `livestock.animals.write` |
| `GET /api/v1/animals` | Solo auth |
| `GET /api/v1/animals/{id}` | Solo auth |
| `POST /api/v1/animals/{id}/identifiers` | **Solo auth** — sin `livestock.animals.write` |
| `DELETE /api/v1/animals/{id}` | `livestock.animals.write` |
| `GET /api/v1/animals/{id}/individual-state` | Solo auth |
| `PUT /api/v1/animals/{id}` | `livestock.animals.write` |

Registrar un animal nuevo y asignarle un identificador no exigen `livestock.animals.write`,
pero editarlo (`PUT`) o eliminarlo (`DELETE`) sí. Mismo patrón de asimetría que en
`AnimalEventsEndpoints.cs`.

#### `BreedingEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/breeding/semen-straws` | Solo auth |
| `GET /api/v1/breeding/semen-straws` | Solo auth |
| `POST /api/v1/breeding/services` | Solo auth |
| `POST /api/v1/breeding/pregnancy-checks` | Solo auth |
| `GET /api/v1/breeding/pregnancies/active` | Solo auth |
| `POST /api/v1/breeding/birthings` | Solo auth |
| `GET /api/v1/breeding/birthings` | Solo auth |
| `POST /api/v1/breeding/weanings` | Solo auth |
| `POST /api/v1/breeding/cohorts/{cohortId}/wean` | Solo auth |
| `POST /api/v1/breeding/cohorts/{cohortId}/classify-by-weight` | Solo auth |
| `GET /api/v1/breeding/pedigree/{animalId}` | Solo auth |
| `GET /api/v1/breeding/kpis/dams/{damId}` | Solo auth |

Ningún endpoint de este archivo usa `RequirePermission`, pese a que `breeding.events.record`
y `breeding.events.read` existen en el catálogo (sec. 2, hallazgo central).

#### `BreedsEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/breeds` | Solo auth |
| `POST /api/v1/breeds` | `livestock.breeds.manage` |

#### `FarmModulesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/farm-modules` | `settings.farm-modules.read` |
| `PATCH /api/v1/farm-modules/{key}` | `settings.farm-modules.manage` |

#### `HealthPlansEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/health-plans` | Solo auth |
| `POST /api/v1/health-plans` | Solo auth |
| `POST /api/v1/health-plans/{planId}/items` | Solo auth |
| `POST /api/v1/health-plans/{planId}/assignments` | Solo auth |

Crear un plan sanitario completo (con sus ítems y asignaciones a animales o lotes) no exige
ningún permiso específico — solo estar autenticado.

#### `InventoryEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/inventory/items` (crear ítem) | **Solo auth** |
| `GET /api/v1/inventory/items` | Solo auth |
| `GET /api/v1/inventory/items/{itemId}` | Solo auth |
| `GET /api/v1/inventory/items/{itemId}/batches` | Solo auth |
| `GET /api/v1/inventory/items/{itemId}/unit-conversions` | Solo auth |
| `POST /api/v1/inventory/items/{itemId}/feed-stage` | `inventory.items.manage` |
| `POST /api/v1/inventory/items/{itemId}/batches` (legacy) | `inventory.items.manage` |
| `POST /api/v1/inventory/items/{itemId}/receptions` | `inventory.receptions.manage` |
| `POST /api/v1/inventory/items/{itemId}/unit-conversions` | **Solo auth** |
| `POST /api/v1/inventory/feed-consumptions` | **Solo auth** (deuda conocida BJ-04, ver `BACKLOG.md`) |
| `GET /api/v1/inventory/items/{itemId}/consumptions` | Solo auth |
| `POST /api/v1/inventory/feed-stages` | `inventory.feed-stages.manage` |
| `POST /api/v1/inventory/feed-stages/{id}/deactivate` | `inventory.feed-stages.manage` |
| `POST /api/v1/inventory/feed-stages/{id}/activate` | `inventory.feed-stages.manage` |
| `GET /api/v1/inventory/feed-stages` | Solo auth |

#### `MilkingEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/milking-sessions` (registrar ordeño) | **Solo auth** |
| `GET /api/v1/milking-sessions` | **Solo auth** |

`production.milking.record` y `production.milking.read` existen en el catálogo y no se usan
aquí (sec. 2, hallazgo central).

#### `MortalityCausesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/mortality-causes` | Solo auth |
| `GET /api/v1/mortality-causes` | Solo auth |
| `DELETE /api/v1/mortality-causes/{id}` | Solo auth |

Ningún endpoint de este catálogo tiene permiso propio — ni siquiera crear o desactivar una
causa de mortalidad.

#### `PeopleEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/people/users` (bootstrap/registro) | `AllowAnonymous`, con guarda interna: si ya existe al menos un usuario, exige rol `admin` o `people.users.manage`; si la tabla está vacía, cualquiera puede crear el primer usuario |
| `GET /api/v1/people/users` | `people.users.manage` |
| `POST /api/v1/people/users/{id}/deactivate` | `people.users.manage` |
| `POST /api/v1/people/auth/login` | `AllowAnonymous` |
| `POST /api/v1/people/auth/refresh` | `AllowAnonymous` |
| `POST /api/v1/people/auth/revoke` | `AllowAnonymous` |
| `GET /api/v1/people/permissions` | `people.roles.manage` |
| `GET /api/v1/people/roles` | `people.roles.manage` |
| `POST /api/v1/people/roles` | `people.roles.manage` |
| `PUT /api/v1/people/roles/{id}` | `people.roles.manage` |
| `POST /api/v1/people/users/{userId}/roles/{roleId}` | `people.roles.manage` |
| `GET /api/v1/people/audit` | `people.users.manage` |
| `GET /api/v1/audit` (duplicado fuera del grupo `/people`) | `people.users.manage` |

El endpoint de bootstrap (`POST /users` sin usuarios previos) es una ventana deliberada:
solo se abre mientras `people.users` está vacío, protegida por `BootstrapLock` (semáforo)
contra doble-creación concurrente del primer admin.

#### `PlausibilityRangesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/plausibility-ranges` | Solo auth |
| `POST /api/v1/plausibility-ranges` | **Solo auth** |
| `PATCH /api/v1/plausibility-ranges/{id}/bounds` | **Solo auth** |
| `DELETE /api/v1/plausibility-ranges/{id}` | **Solo auth** |
| `POST /api/v1/plausibility-ranges/{id}/activate` | **Solo auth** |
| `POST /api/v1/plausibility-ranges/evaluate` | Solo auth |

**El comentario XML del archivo (líneas 6-13) afirma**: "Mutating endpoints require the
standard `LivestockAnimalsWrite` permission so the panel can manage them". **El código no lo
hace** — ningún `POST`/`PATCH`/`DELETE` de este archivo llama `.RequireAuthorization(policy
=> policy.RequirePermission(...))`. Es un desface entre lo documentado en el propio código y
lo implementado, no solo un permiso faltante.

#### `SpeciesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/species` | Solo auth |
| `POST /api/v1/species` | `livestock.species.manage` |
| `PATCH /api/v1/species/{speciesId}/lactation` | `livestock.species.manage` |
| `GET /api/v1/species/{speciesId}/lactation` | Solo auth |

#### `SyncEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/sync/pull` | Solo auth de grupo; el filtrado por colección ocurre **dentro** del handler (sec. 3) |
| `POST /api/v1/sync/push` | Solo auth |
| `GET /api/v1/sync/operations` | **Solo auth** — y sin filtrar por usuario (ver hueco 5, sec. 7) |
| `GET /api/v1/sync/conflicts` | `people.users.manage` |

#### `TasksEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/alerts` | **Solo auth** |
| `POST /api/v1/alerts/generate` | **Solo auth** |
| `POST /api/v1/alerts/{id}/dismiss` | **Solo auth** |

`tasks.manage` y `tasks.read` existen en el catálogo y no se usan aquí (sec. 2, hallazgo
central). Cualquier usuario autenticado puede disparar la generación de alertas o
descartarlas.

#### `TreatmentCoursesEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `POST /api/v1/treatment-courses` | Solo auth |
| `GET /api/v1/treatment-courses/{id}` | Solo auth |
| `POST /api/v1/treatment-courses/{id}/applications` | Solo auth |
| `GET /api/v1/animals/{animalId}/treatment-courses` | Solo auth |
| `GET /api/v1/animal-groups/{groupId}/treatment-courses` | Solo auth |
| `GET /api/v1/dose-kinds` | Solo auth |

Ningún endpoint de este archivo tiene permiso propio, incluyendo crear un curso de
tratamiento completo con dosis y período de retiro.

#### `TreatmentReasonsEndpoints.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /api/v1/treatment-reasons` | Solo auth |
| `POST /api/v1/treatment-reasons` | `livestock.treatments.configure` |
| `DELETE /api/v1/treatment-reasons/{id}` | `livestock.treatments.configure` |
| `POST /api/v1/treatment-reasons/{id}/activate` | `livestock.treatments.configure` |
| `PATCH /api/v1/treatment-reasons/{id}/label` | `livestock.treatments.configure` |

#### Fuera de `Endpoints/`: `Program.cs`

| Método y ruta | Permiso exigido |
|---|---|
| `GET /health` | `AllowAnonymous` — solo devuelve `{ status: "ok" }`, sin datos de negocio |

---

## 3. Autorización del pull de sincronización

`GetSyncPullQueryHandler` (`src/Hato.Api/Sync/SyncPullQueries.cs`) no delega el filtrado de
colecciones al framework de autorización de ASP.NET: lo hace a mano con el diccionario
`RequiredPermissionByCollection` (líneas 358-380), que mapea cada uno de los 19 nombres de
colección sincronizable (`animals`, `animalGroups`, `inventoryItems`, `healthPlans`, etc.) al
permiso que un rol necesita para leerla. El flujo (`Handle`, líneas 382-404):

1. Resuelve los códigos de permiso del usuario autenticado (`IUserPermissionsReader`). Si
   `currentUser.UserId` es nulo — no debería pasar detrás de `RequireAuthorization()`, pero el
   comentario en el código lo trata como caso real — la lista de permisos queda vacía:
   **denegar por defecto**, no fallar abierto.
2. Construye `visible` = las colecciones cuyo permiso requerido está en la lista del usuario.
3. Si el cliente pidió colecciones específicas por nombre (`?collections=`), la intersección
   final es `requested ∩ visible` — pedir por nombre una colección que el rol no puede leer
   **no la filtra a nivel de permiso**, la deja fuera igual.

**Este es el punto que un error entrega datos a quien no debe**, tal como señala el
comentario del propio código (línea 389): si una colección nueva se agrega a
`SyncCollectionsDto` y a `ReadAsync(...)` pero **se olvida agregar su entrada en
`RequiredPermissionByCollection`**, el compilador no avisa — el diccionario es la única
fuente de verdad de qué permiso protege esa colección, y una entrada faltante no es un error
de tipos, es una colección que **nunca aparece en `effective`** (porque `visible` se
construye iterando el diccionario, no las DTOs) y por lo tanto queda simplemente vacía en la
respuesta para todos los roles — el modo de fallo de un olvido aquí es "nadie ve la
colección nueva", no "todos la ven". El riesgo inverso — mapear una colección a un permiso
demasiado amplio, o reusar por accidente el permiso de otra colección — sí filtra datos de
más, y no hay una prueba automatizada que compare `SyncCollectionsDto` contra las claves del
diccionario para detectar un mapeo faltante o incorrecto en CI. **No verificado**: no se
confirmó si existe un test de esa forma en `tests/Hato.Sync.IntegrationTests/`.

---

## 4. Manejo de secretos

| Secreto | Dónde vive | Cómo llega a producción |
|---|---|---|
| `POSTGRES_PASSWORD` | `.env` (gitignored), referenciado por `docker-compose.yml` con `${POSTGRES_PASSWORD:?...}` (falla el `up` si falta) | El operador la define en `.env` local a partir de `.env.example`, que documenta el campo pero no trae valor |
| `Jwt:SigningKey` | Variable de entorno `Jwt__SigningKey` o `dotnet user-secrets` (mensaje de validación en `PeopleModule.cs:67-69`) | **No está en `docker-compose.yml`** — ver hueco 4, sec. 7 |
| Cadena de conexión (`ConnectionStrings__HatoDb`) | Interpolada en `docker-compose.yml` a partir de `POSTGRES_PASSWORD` | Vive solo en el proceso del contenedor `api`/`migrate`, no en un archivo commiteado |

`.env.example` documenta explícitamente la política ("Ninguna credencial real vive en el
repositorio, ni siquiera las de desarrollo") y `.env` está en `.gitignore`. Se verificó que
el `.env` local actual solo contiene `POSTGRES_PASSWORD` (valor no transcrito aquí, por
regla de este documento).

**Precedente real:** `docs/ROADMAP.md:11` documenta que en Fase 0, GitGuardian detectó una
contraseña de desarrollo commiteada por descuido antes de llegar a `develop`; se resolvió
aplastando la rama antes del merge. La retrospectiva de Fase 0 lo registra como la disciplina
de PR + CI "pagándose sola en la primera pasada" — el control (GitGuardian en el pipeline)
funcionó como diseñado, pero el hecho de que haya ocurrido una vez es la razón por la que
este documento existe.

---

## 5. Superficie expuesta

### CORS

`src/Hato.Api/Program.cs:12-42`. Una sola política, `AdminWebCors`, aplicada globalmente
(`app.UseCors(AdminWebCorsPolicy)`, línea 42) — no hay políticas distintas por grupo de
endpoints. Orígenes permitidos: `Cors:AllowedOrigins` de configuración, con fallback a
`["http://localhost:4200"]` si la sección no está configurada (línea 29). `AllowAnyHeader()`
y `AllowAnyMethod()` (líneas 32-33) — sin restricción de método ni de cabecera, solo de
origen. En `docker-compose.yml`, `Cors__AllowedOrigins__0` se fija a
`http://localhost:${WEB_PORT:-4200}` (línea 37) — es decir, en el despliegue de referencia
por compose, el origen permitido sigue siendo `localhost`, no una URL de red de la finca.
**No verificado**: no se confirmó qué valor de `Cors:AllowedOrigins` usa un despliegue real
fuera de `docker-compose.yml` (p. ej. si la finca expone el panel en una IP de LAN o un
dominio propio, hace falta configurar esa sección explícitamente o CORS lo rechaza).

### Puertos publicados (`docker-compose.yml`)

| Servicio | Puerto host | Expone |
|---|---|---|
| `postgres` | `${POSTGRES_PORT:-5432}` → 5432 interno | El motor de base de datos, directo, sin proxy — cualquier equipo en la misma red que alcance ese puerto puede intentar conectarse (protegido solo por la contraseña de `POSTGRES_PASSWORD`, sin filtro de IP a nivel de compose) |
| `api` | `8080` → 8080 interno | La API REST completa |
| `web` | `${WEB_PORT:-4200}` → 80 interno | El panel Angular servido |
| `migrate` | (ninguno) | Contenedor de corrida única, no expone puerto |

**En la red de la finca**, sin un firewall o reglas adicionales fuera de este
`docker-compose.yml`, los tres puertos publicados (`postgres`, `api`, `web`) son alcanzables
por cualquier dispositivo en la misma red local — el compose no aísla `postgres` detrás de
la red interna de Docker con `expose` en vez de `ports`, lo publica directamente al host.
**No verificado**: no se confirmó si el despliegue real de la finca aplica un firewall de
host o de router que restrinja esto — está fuera del alcance de lo que el repositorio
controla.

---

## 6. Huecos conocidos

Auditoría de código realizada el **2026-08-16**. Cada hueco tiene su entrada correspondiente
en `docs/BACKLOG.md` sec. "Seguridad — hallazgos de la auditoría de `SEGURIDAD.md`
(2026-08-16)".

1. **Siete permisos declarados en `SystemPermissions` nunca se exigen en ningún endpoint**
   (`breeding.events.record`, `breeding.events.read`, `production.milking.record`,
   `production.milking.read`, `tasks.manage`, `tasks.read`, `people.users.read`). Los
   módulos Breeding, Milking y Tasks/Alerts están abiertos a cualquier usuario autenticado
   sin distinción de rol. Detalle en sec. 2.

2. **Asimetría entre escritura individual y grupal.** `POST
   /api/v1/animals/{id}/events`, `POST /api/v1/animals` (registrar) y `POST
   /api/v1/animals/{id}/identifiers` no exigen `livestock.animals.write`, mientras que sus
   equivalentes de edición/borrado (`PUT`, `DELETE`) y el análogo grupal (`POST
   /api/v1/animal-groups/{id}/events`) sí lo exigen. Ningún ADR documenta esta asimetría
   como decisión — es un patrón de código, no una decisión de dominio.

3. **`PlausibilityRangesEndpoints.cs` documenta en comentario un permiso que el código no
   implementa.** El comentario XML (líneas 6-13) afirma que las mutaciones exigen
   `livestock.animals.write`; ningún endpoint del archivo llama `RequirePermission`. Riesgo
   adicional: quien lea solo el comentario (no el código) concluye que el catálogo de
   rangos de plausibilidad está protegido cuando no lo está.

4. **`docker-compose.yml` fuerza `ASPNETCORE_ENVIRONMENT: Development` para el contenedor
   `api`** (línea 39), sobreescribiendo el `ENV ASPNETCORE_ENVIRONMENT=Production` del
   `Dockerfile` (`src/Hato.Api/Dockerfile:27`). Efecto concreto: como `Jwt:SigningKey` no
   está definida en `docker-compose.yml` y el proceso nunca corre en `IsProduction()` bajo
   este compose, `PeopleModule.cs:53-55` genera una clave de firma aleatoria en memoria en
   cada arranque del contenedor — lo que invalida todo JWT de acceso emitido antes del
   reinicio (hasta 8 horas de sesiones activas) aunque los refresh tokens en base de datos
   sigan siendo válidos. Además, la validación que exige una `SigningKey` real de 32+
   caracteres en producción (`PeopleModule.cs:66-70`) nunca se ejercita en este camino de
   despliegue.

5. **`GET /api/v1/sync/operations` no filtra por usuario ni exige permiso propio.**
   `GetSyncOperationsQueryHandler` (`src/Hato.Api/Sync/SyncPullQueries.cs:661-689`) consulta
   `context.SyncOperations` sin `Where(o => o.UserId == ...)` — cualquier usuario
   autenticado ve las últimas 100 operaciones de sincronización de **todos** los empleados y
   dispositivos, incluidos `DeviceId` y `ErrorDetails`. Compárese con `GET
   /api/v1/sync/conflicts`, en el mismo archivo, que sí exige `people.users.manage`.

6. **`POST /api/v1/inventory/feed-consumptions` sigue sin permiso propio.** Confirmado
   vigente en esta auditoría — coincide con el hallazgo `BJ-04` ya registrado en
   `docs/BACKLOG.md` desde la auditoría del PR #94/ADR-0026. Se referencia aquí, no se
   duplica.

7. **Sin límite de intentos de login.** `LoginCommandHandler` no aplica rate limiting ni
   bloqueo temporal tras intentos fallidos repetidos — un atacante con acceso de red al
   endpoint puede probar contraseñas sin fricción más allá de la latencia de PBKDF2
   (100.000 iteraciones, que sí impone un costo por intento, pero no un límite duro).

8. **Sin detección de reuso de refresh token revocado.** Ver sec. 1 — presentar un
   `refresh_token` ya revocado no dispara revocación en cascada de la cadena del usuario.

No verificado explícitamente en esta auditoría (fuera del alcance de lo que el código
permite confirmar por lectura estática): comportamiento real de un despliegue fuera de
`docker-compose.yml` (firewall de host, VPN de finca), y si `created_by_ip` en
`RefreshToken` se puebla en algún flujo no revisado.

---

## Ver también

- `docs/adr/0007-modelo-permisos-bd.md` — la decisión de RBAC granular en BD (qué se decidió).
- `docs/adr/0008-protocolo-sincronizacion.md` — el protocolo pull/push/conflictos completo.
- `docs/LEGAL-ECUADOR.md` — trazabilidad y retención exigidas por Agrocalidad/SRI, que
  determinan qué debe *conservarse* aunque este documento se enfoque en qué debe
  *protegerse*.
- `docs/BACKLOG.md` sec. "Seguridad" — el rastreo accionable de cada hueco de la sec. 6.
