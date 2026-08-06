# ARCHITECTURE.md — Arquitectura del Proyecto HATO

> Fuente de verdad técnica. Cambios estructurales pasan por ADR (`docs/adr/`).
> Estilo: **monolito modular** + Clean Architecture + CQRS ligero. Un deploy, muchos módulos.

## Stack

| Capa | Tecnología | Notas |
|---|---|---|
| Backend | .NET 8+ (C#), ASP.NET Core | Minimal APIs o Controllers; REST versionado |
| ORM / BD | EF Core + **PostgreSQL** | JSONB para payloads de eventos; migraciones versionadas |
| Mediación interna | MediatR | Commands/Queries + Domain Events entre módulos |
| Validación | FluentValidation | En Application; invariantes en Domain |
| Pruebas | xUnit + Testcontainers | Integración contra Postgres real |
| Web admin | **Angular** | Panel administrativo/gerencial |
| Móvil | **React Native** | Offline-first: SQLite/WatermelonDB + sync |
| CI/CD | GitHub Actions | Build + tests en cada PR; deploy por tags desde `main` |
| Auth | ASP.NET Identity + JWT | Roles/permisos configurables en BD |

## Mapa de módulos (bounded contexts)

```
┌────────────────────────────────────────────────────────────────────┐
│                         HATO (monolito modular)                    │
│                                                                    │
│  Livestock        Breeding         Grazing          Health         │
│  (animales,       (servicios, IA,  (potreros,       (tratamientos, │
│  eventos,         gestaciones,     rotación,        vacunas,       │
│  identificación,  partos,          aforos)          retiros,       │
│  grupos)          genealogía)                       calendario)    │
│                                                                    │
│  Production       Inventory        Transformation   Sales          │
│  (ordeño,         (ítems, lotes,   (BOM, órdenes,   (ventas de     │
│  lactancias,      consumos por     costeo)          productos y    │
│  calidad)         grupo)                            animales)      │
│                                                                    │
│  Purchasing       Accounting       People           Tasks/Alerts   │
│  (compras, CxP,   (asientos,       (empleados,      (motor de      │
│  proveedores)     centros de       roles,           reglas y       │
│                   costo, CxC)      permisos)        recordatorios) │
│                                                                    │
│  ─── Shared Kernel: UUID, unidades, dinero, DomainEvent, auditoría │
│  ─── Transversales: Identity/Auth, Adjuntos, Sync móvil, Auditoría │
└────────────────────────────────────────────────────────────────────┘
        Futuro (módulos nuevos, mismo patrón): Turismo · Maquinaria/Activos
```

**Regla de dependencias**: los módulos se comunican por (a) contratos públicos
(interfaces en `<Módulo>.Contracts`) y (b) eventos de dominio vía MediatR. `Accounting`
solo **recibe** asientos; no conoce a nadie. Prohibido el acceso directo a tablas ajenas.

## Decisiones de modelado que definen todo

### 1. Animal genérico + eventos inmutables
No existen tablas `Vacas` ni `Cerdos`. Existe `Animal` → `Species`/`Breed`/`Category`
(datos configurables). El historial es `animal_events`: tabla append-only con
`type`, `occurred_at`, `recorded_by`, `cost`, y `payload JSONB` tipado por evento.
Correcciones = evento nuevo que referencia al erróneo. Esto da: expediente completo,
auditoría gratis, y la serie temporal que la IA necesitará en fase 5.

### 2. Identidad soberana + identificaciones adjuntas
`animals.id` = UUID interno (generable en el móvil, offline). `animal_identifiers` =
{tipo: FarmTag | OfficialTag/SIFAE | RFID | Nombre, valor, desde, hasta}. Soporta animales
sin registrar, aretes que llegan tarde, aretes reemplazados, y ventas pre-registro,
**sin perder historial jamás**.

### 3. Genealogía con padre dual
`animals.mother_id → animals.id`; el padre es una referencia polimórfica:
`father_animal_id` **o** `father_straw_id → semen_straws`. Árboles = CTE recursiva en
Postgres. Los KPI de "buena madre" se derivan de eventos (partos, destetes, servicios).

### 4. Grupos (lotes) como unidad de manejo y costeo
`animal_groups` + `group_memberships` (animal, grupo, desde, hasta). Consumos de alimento
y costos operativos se registran **al grupo**; el costeo prorratea por animal-día.
Movimientos a potreros también son del grupo.

### 5. Transformación = BOM (el seguro de extensibilidad)
`bill_of_materials` (receta: insumos → productos + subproductos + merma esperada) y
`transformation_orders` (ejecución con cantidades y costos reales). Leche→queso,
cerdo→cortes, leche→yogurt: **misma maquinaria conceptual, cero código nuevo en el core**.

### 6. Períodos de retiro como regla bloqueante
Un `TreatmentEvent` con medicamento que tenga retiro genera un `WithdrawalPeriod` activo
sobre el animal (leche y/o carne). `Production` y `Sales` consultan ese contrato: la leche
de una vaca en retiro se registra pero se marca no-vendible; la venta se bloquea.

### 7. Offline-first móvil (resumen; detalle en ADR-0005)
- Escritura local en SQLite (WatermelonDB) con UUIDs de cliente.
- Sync bidireccional por *pull/push* con `updated_at` + tombstones lógicos.
- Conflictos: los eventos son append-only ⇒ casi no hay conflictos reales; para entidades
  editables, last-write-wins por campo + bitácora de conflictos revisable.
- El backend expone endpoints `/sync` idempotentes (reintentos seguros).

### 8. Dinero, unidades y tiempo
`decimal` para dinero; cantidades siempre con unidad explícita (tabla de unidades y
conversiones); persistencia en UTC, presentación en `America/Guayaquil`.

## Esqueleto de solución

```
hato/
├─ src/
│  ├─ Hato.Api/                     # Composición: DI, auth, endpoints de todos los módulos
│  ├─ Shared/Hato.SharedKernel/     # DomainEvent, Money, Quantity, AuditableEntity...
│  └─ Modules/
│     ├─ Livestock/ {Domain, Application, Infrastructure, Contracts}
│     ├─ Breeding/  {...}
│     ├─ Health/    {...}
│     ├─ Production/{...}
│     ├─ Inventory/ {...}
│     ├─ Sales/     {...}
│     ├─ Accounting/{...}
│     └─ People/    {...}
├─ tests/
│  ├─ Livestock.UnitTests / Livestock.IntegrationTests
│  └─ ...
├─ clients/
│  ├─ admin-web/    # Angular
│  └─ field-app/    # React Native
├─ docs/ (estos documentos + adr/)
└─ .github/workflows/ci.yml
```

## Seguridad y auditoría (mínimos desde fase 1)

- Roles/permisos en BD (no hardcodeados); JWT con refresh; contraseñas con hash moderno.
- Auditoría transversal: quién creó/modificó qué y cuándo, en todas las tablas.
- Adjuntos (fotos de guías, facturas, certificados) como almacenamiento de archivos
  referenciado por eventos/documentos.
- Backups: diario automatizado + offsite + **restauración probada mensual** (Art. 2).

## Anti-decisiones vigentes (lo que NO hacemos y por qué)

| No hacemos | Hasta que… | ADR |
|---|---|---|
| Microservicios | un módulo demuestre necesidad real de escalar aparte | 0001 |
| GraphQL | existan clientes con necesidades de datos divergentes | 0002 |
| Facturación SRI directa | el volumen justifique dejar al proveedor autorizado | — |
| Blockchain | un comprador/certificador exija trazabilidad verificable (→ anclaje de hashes) | — |
| IA/ML propio | existan ≥2 años de datos limpios (antes: features with LLM vía API, p. ej. registro por voz) | — |

## Arquitectura de Cliente Móvil y Protocolo de Sincronización (Fase 3)

- **Cliente Móvil (`clients/field-app/`)**: React Native (Expo SDK 51), TypeScript y
  WatermelonDB sobre SQLite. Toda escritura de UI entra al outbox local; ninguna pantalla
  llama a la red.
- **Seguridad Offline**: sesión en `expo-secure-store` (llavero del dispositivo) y PIN de
  desbloqueo guardado como digest SHA-256 con sal.
- **Identidad soberana en el cliente**: los UUID se generan con `expo-crypto` y el servidor
  los acepta (`createAnimal` admite `id`). Sin eso, un animal registrado en el potrero no
  puede ser referenciado por sus propios eventos hasta después de sincronizar (Art. 3).

### Cursor del pull

`GET /api/v1/sync/pull?since=<cursor>&collections=&batchSize=`

El cursor es un par **`(timestamp, id)`**, no un timestamp. El id no es decorativo: dos
filas escritas en el mismo milisegundo son indistinguibles por tiempo, así que un cursor
sólo-tiempo tiene que elegir entre reenviarlas para siempre (con `>=`) o perder las que no
alcanzó (con `>`). Cada colección se ordena por ese mismo par **antes** del `LIMIT` — un
`LIMIT` sin orden determinista descarta filas que el cliente no vuelve a pedir jamás.

Cuando una colección se trunca por el tamaño de lote, el cursor devuelto **no avanza más
allá de la última fila entregada de esa colección**: se toma la frontera más temprana entre
las truncadas. Repetir filas cuesta ancho de banda; saltárselas pierde datos.

Requisito de infraestructura: toda entidad sincronizable recibe `created_at`/`updated_at`
del `AuditTimestampInterceptor` compartido, registrado en **todos** los DbContext de módulo.
Una fila sin sellar queda en `0001-01-01` y sale del flujo de cambios.

### Filtrado por permisos

El pull sólo entrega las colecciones que el rol del usuario puede leer (PLAN-FASE-3-4
sec.3.A, tarea 4). `Hato.Api` resuelve el conjunto de permisos efectivos vía
`IUserPermissionsReader` (contrato público de People, implementado en
`People.Infrastructure/CrossModule`, sin que Livestock ni Inventory dependan de People —
Art. 6) y lo cruza contra un mapa colección→permiso: `animals`, `animalIdentifiers`,
`animalGroups`, `groupMemberships`, `withdrawalPeriods` y las tablas de referencia
(`species`, `breeds`, `animalCategories`) requieren `livestock.animals.read`;
`inventoryItems` requiere `inventory.items.read`. Un rol de admin ya trae todos los
códigos por la semilla RBAC, así que no hace falta un bypass especial. El parámetro
`collections=` explícito nunca puede ampliar lo que el permiso permite: se cruzan ambos
filtros antes de tocar la base.

### Borrado lógico

`Animal.Delete()` es el primer —y por ahora único— caso real de tombstone: deshace un
animal mal registrado (doble toque en el campo, especie equivocada). Es un tombstone en
el sentido estricto de Art. 1: la fila nunca se toca físicamente, sólo se marca
`deleted_at`. Un `HasQueryFilter(a => a.DeletedAt == null)` la oculta de los endpoints
normales (`GET /api/v1/animals`, `GET /api/v1/animals/{id}`); el pull la sigue viendo
porque lee con `IgnoreQueryFilters()` a propósito. Invariante de la capa de Aplicación
(el agregado no puede verla): un animal con eventos registrados no se puede eliminar —
en ese punto ya no es "un error", y borrarlo dejaría eventos apuntando a un animal
oculto.

> **Estado real**: el borrado lógico y el filtrado por permisos del pull ya están
> implementados y probados de punta a punta (`Hato.Sync.IntegrationTests`). Sigue
> pendiente: extender el borrado lógico a otras entidades sincronizables (grupos,
> ítems de inventario) si aparece un caso de uso real que lo pida.

### Conflictos LWW en entidades editables

Casi todo el modelo es append-only (eventos, ordeño, partos) y ahí no hay conflicto de
edición posible: dos dispositivos nunca compiten por el mismo campo porque cada registro
es una fila nueva. La única entidad genuinamente editable es `Animal` (raza, categoría,
fecha de nacimiento) — el ejemplo que ADR-0008 nombra explícitamente para la estrategia
Last-Write-Wins.

`Animal.LastEditedAt` guarda el momento declarado de la última edición aceptada — **no**
`UpdatedAt` (tiempo de procesamiento del servidor). Esa distinción es la que hace que el
resultado dependa de cuándo ocurrió la edición en el campo y no de qué tan rápido llegó
la señal: un empleado que edita a las 6 AM y recupera señal al mediodía no debe perder
frente a una edición hecha a las 7 AM que llegó primero sólo por el orden de los push.

El push declara `knownUpdatedAt` — el `LastEditedAt` que ese dispositivo vio la última
vez (`null` significa "vi que nunca se había editado", un valor legítimo, no "no sé").
Cuando ese valor ya no coincide con el estado real de la fila, dos escrituras compitieron
por el mismo campo: gana quien declare el `occurredAt` más tardío, sin importar el orden
de llegada. Cada campo que realmente difiere entre lo que había y lo que el push
perdedor quería queda escrito en `sync_conflicts` (`GET /api/v1/sync/conflicts`, permiso
de administración) con el valor que quedó, el que se intentó y la resolución.

Una edición directa desde el panel (`PUT /api/v1/animals/{id}`) no declara
`knownUpdatedAt`: es síncrona y en línea, no existe ventana offline que pueda haber
quedado obsoleta, así que se aplica siempre sin pasar por la detección de conflictos.

> **Estado real**: implementado y probado de punta a punta para `Animal`
> (`Hato.Sync.IntegrationTests`). La resolución es a nivel de fila completa (la edición
> que gana se aplica a los tres campos juntos), no por campo individual con marcas de
> tiempo independientes — coincide con lo que ADR-0008 describe ("basado en `updated_at`
> reportado"), aunque el registro del conflicto sí detalla cada campo por separado.
> El mecanismo es alcanzable desde un cliente real por ambos lados: la pantalla
> "Editar animal" en `clients/field-app/` encola la edición offline con el
> `knownUpdatedAt` que ese dispositivo vio la última vez (sin aplicarla localmente —
> ver el README del cliente), y la bandeja `/sync` en `admin-web` expone lo que se
> resolvió. Falta: extender el mismo patrón a otras entidades editables si aparece un
> caso de uso real.

### Push

`POST /api/v1/sync/push` — lote de hasta 500 operaciones tipadas (`recordMilking`,
`recordAnimalEvent`, `createAnimal`, `recordBirth`, `moveAnimal`).

- **Idempotencia por reserva previa**: la operación se inserta con estado `Processing`
  *antes* de ejecutarse, de modo que el índice único sobre `client_operation_id` —y no un
  `SELECT` previo— arbitre las carreras.
- **Aislamiento por operación**: un lote nunca falla entero por una operación mala; cada una
  responde `Accepted` / `Duplicate` / `Rejected` con su motivo.
- **No es una puerta trasera**: cada operación se despacha por el mismo comando MediatR que
  usa la API web, así que validaciones, invariantes y el bloqueo por retiro (Art. 19)
  aplican igual.
- **Límite de atomicidad conocido**: una operación que toca dos módulos (un parto escribe en
  Breeding y en Livestock) no es atómica entre ambos, igual que en la API web. La garantía
  que sí se sostiene es que nunca se duplica y nunca se pierde en silencio.

### Invariante de cero pérdida

Una operación rechazada se conserva con su motivo y aparece en la bandeja de problemas del
teléfono. No se borra nunca.

> **Estado real**: los tombstones están implementados de punta a punta en el protocolo, el
> borrado lógico y el cliente (ver "Borrado lógico" más arriba).

