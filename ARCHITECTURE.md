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
| IA/ML propio | existan ≥2 años de datos limpios (antes: features con LLM vía API, p. ej. registro por voz) | — |
