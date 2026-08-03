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

> **Estado real**: los tombstones están implementados de punta a punta en el protocolo y en
> el cliente, pero **ninguna operación de dominio asigna todavía `deleted_at`**, así que no
> hay borrados lógicos que propagar. El filtrado del pull por permisos y la bitácora de
> conflictos LWW siguen pendientes.

