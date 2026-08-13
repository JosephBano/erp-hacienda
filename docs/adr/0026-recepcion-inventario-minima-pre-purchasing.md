# ADR-0026 — Recepción de inventario mínima pre-Purchasing (objetivo: desbloquear el piloto)

- **Estado:** Aceptado
- **Fecha:** 2026-08-13
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina — bloque 3.5a (bloqueante transversal del piloto; prepara Fase 4 — Purchasing)

## Contexto

Hechos verificables en este repositorio (al 2026-08-13, develop local):

- `InventoryBatch` (`src/Modules/Inventory/Hato.Modules.Inventory.Domain/InventoryItem.cs:102-138`)
  tiene `BatchNumber`, `Quantity`, `CostPerUnit`, `ExpirationDate?` y auditorías
  (`CreatedAt`, `UpdatedAt`, `DeletedAt`, `CreatedBy`, `UpdatedBy`). **No** tiene
  `ReceivedAt`, `SupplierLabel`, `InvoiceReference`, `Notes`, `RecordedById`
  ni `RecordedByLabel`. La fecha declarada por el operario se confundiría con el
  `CreatedAt` del sistema si se intentara leerla hoy.
- El único flujo de "entrada" es `CreateInventoryBatchCommand`
  (`src/Modules/Inventory/Hato.Modules.Inventory.Application/Items/CreateInventoryItemCommand.cs:53-86`)
  expuesto vía `POST /api/v1/inventory/items/{itemId}/batches`
  (`src/Hato.Api/Endpoints/InventoryEndpoints.cs:44-50`). El handler no emite
  ningún evento de dominio, llama `item.AddBatch(...)` y persiste. No pide
  permiso (gap de seguridad preexistente, mismo patrón que `AnimalGroupsEndpoints`
  antes de ADR-0025).
- `GroupFeedConsumption` (`src/Modules/Inventory/Hato.Modules.Inventory.Domain/GroupFeedConsumption.cs:9-126`)
  define `QuantityRecorded`, `UnitRecorded`, `QuantityInBaseUnit`, `AppliedFactor`,
  `RecordedById?`, `RecordedByLabel`, `ConsumedAt`, `Notes?` y se persiste con
  `numeric(18,3)`/`numeric(18,6)`. Es el espejo de "lo que sí podemos hacer" para
  la salida de stock; la entrada no tiene nada equivalente.
- `InventoryConfigurations` (`src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure/Persistence/Configurations/InventoryConfigurations.cs:48-58`)
  define `InventoryBatch` con `OnDelete(DeleteBehavior.Cascade)` respecto a
  `InventoryItem` y la columna `BatchNumber` con `HasMaxLength(50)`. El contexto
  (`InventoryDbContext.cs:5-46`) aplica snake_case automático.
- BACKLOG.md (entrada bajo "Bloqueante transversal (descubierto durante el
  piloto)") describe al pie de la letra el problema: el dueño puede registrar
  que llegó alimento, pero la fila queda con `created_at` del sistema, no con
  una fecha de recepción declarada, y no se puede reconstruir qué proveedor
  entregó qué. El consumo de alimento desde lote (3.5a.7, mergeado) **ya
  descuenta** de `InventoryBatch`; sin este flujo, el lote se queda "vacío
  sin historia" y la integración piloto-Field tapa el hueco desde el móvil
  (clientes/field-app/src/screens/LotEventsScreen.tsx:446-449 muestra el
  mensaje "No hay alimentos en el inventario. Agréguelos desde el panel y
  sincronice para poder registrar el consumo.").
- `docs/planes/PLAN-FASE-3-4.md:516-521` ya menciona como Fase 4 los
  `suppliers`, órdenes de compra y recepciones que alimentan lotes de inventario
  por purchase order. Adelantar la Fase 4 a esta fase para tapar el hueco es
  una sobredimensión injustificable: Fase 4 introduce CxP, evaluación de
  proveedores, NC, y un modelo de costeo por orden — cosas que el piloto no
  usa.
- `docs/GLOSSARY.md` (sección "Producción, inventario y transformación") ya
  incluye las entradas `Recepción de inventario` → `InventoryReception` y
  `Evento de recepción` → `InventoryReceptionRecorded`, con la nota explícita
  "Flujo deprecado y reemplazado por `Purchase/PurchaseReception` cuando
  llegue Purchasing (Fase 4)".

El problema concreto: el piloto no puede abrir —y el sub-criterio de
apertura del piloto definido por ADR-0024 ("alimentación del lote con
trazabilidad de proveedor y factura") es lo que la finca real necesita para
dejar el cuaderno— sin un comando que cree `InventoryBatch` capturando fecha
declarada, proveedor, factura opcional y autor, y emitiendo un evento de
dominio. Hoy el endpoint existe pero es un "volcado en `inventory_batches`"
sin historia.

La sub-decisión de cómo modelar el **devenir** de estos datos (qué pasa
cuando exista `Supplier` FK en Fase 4) tampoco está tomada. ADR-0007 fija
que los permisos viven en BD; ADR-0017 fija que correcciones se registran
como nuevos eventos; ningún ADR vigente dice cómo un comando pre-Purchasing
debe diseñarse para ser sustituible sin perder lo escrito.

El riesgo concreto de **coexistencia** entre la ruta nueva (`POST /receptions`)
y la legacy (`POST /batches`) se rastrea en
[GitHub Issue #93](https://github.com/JosephBano/erp-hacienda/issues/93);
las mitigaciones de UI (badge "Sin declaración completa") y operacionales
(log de warning en el endpoint deprecado) viven en el PR que implementa
este ADR, no en el ADR mismo.

## Decisión

### 1. Enriquecer `InventoryBatch` con seis columnas opcionales + una obligatoria con backfill honesto.

`InventoryBatch` (`src/Modules/Inventory/Hato.Modules.Inventory.Domain/InventoryItem.cs:102-138`)
recibe:

- `ReceivedAt` (`DateTimeOffset` UTC, **no-null**): fecha declarada de
  recepción. Igual semántica que `Animal.LastEditedAt`: la declara el operario,
  no el sistema.
- `SupplierLabel` (`string?`, max 200): proveedor en texto libre.
- `InvoiceReference` (`string?`, max 100): número de factura o guía.
- `Notes` (`string?`, max 500): notas libres.
- `RecordedById` (`Guid?`): FK blanda a `people.users` (sin `REFERENCES` a
  nivel Postgres — la tabla vive en otro schema y prefiero replicar el
  patrón de `GroupFeedConsumption.RecordedById` que es huérfano y se
  reconcilia por `people.users.full_name` cuando hace falta, vía
  `AuditSaveChangesInterceptor` después).
- `RecordedByLabel` (`string?`, max 200): quién registró, en texto. Mismo
  patrón que `GroupFeedConsumption`.

**Texto imperativo:** *`InventoryBatch` gana `ReceivedAt` (no-null, UTC),
`SupplierLabel`, `InvoiceReference`, `Notes`, `RecordedById`, `RecordedByLabel`.
Los últimos cinco son nullable. La única restricción dura es que `ReceivedAt`
debe ser declarado por el operario.*

### 2. Backfill: filas existentes reciben `received_at = created_at` (no `null`).

`docs/GLOSSARY.md` enseña la distinción entre la fecha del sistema
(`CreatedAt`) y la fecha declarada (`ReceivedAt`). Pre-poblar con
`created_at` no es mentir: es la mejor fecha de recepción que el sistema
tiene para esa fila. La UI admin-web mostrará un badge "Sin fecha de
recepción declarada" cuando `CreatedAt != ReceivedAt` (es decir, para
todas las filas del backfill) — el dueño distingue de un vistazo qué
batches fueron capturados "con mano" y cuáles vienen de la historia.

**Texto imperativo:** *La migración `AddInventoryReceptionFields` hace
`received_at = created_at` para filas existentes. Las filas nuevas
siempre llevan `ReceivedAt` declarado por el operario. La UI muestra el
origen.*

### 3. Comando dedicado `RecordInventoryReceptionCommand`; `AddBatch` se deprecaba, no se elimina.

`InventoryItem.AddBatch(...)` se marca `[Obsolete("Use RecordReception(...).
See ADR-0026.")]` (warning de compilador, no error). `AddBatch` queda para
ajustes manuales raros (p. ej. conciliación contra un stock físico que no
proviene de una recepción). El endpoint `POST /batches` recibe el mismo
atributo a nivel de OpenAPI y muestra un banner en la UI admin-web.

`Hato.Modules.Inventory.Application/Receptions/RecordInventoryReceptionCommand.cs`
define:

```csharp
public record RecordInventoryReceptionCommand(
    Guid ItemId,
    string BatchNumber,
    decimal Quantity,
    string Unit,
    decimal CostPerUnit,
    DateOnly? ExpirationDate,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? Notes,
    Guid? RecordedById,
    string? RecordedByLabel) : IRequest<Guid>;
```

El handler `RecordInventoryReceptionHandler` (mismo patrón que
`RecordGroupFeedConsumptionHandler`):

1. Carga `InventoryItem` con `Batches.Include`. Si es `null` o
   `item.IsDeleted`, `DomainException`.
2. Resuelve la conversión de unidad **igual que `RecordGroupFeedConsumptionHandler`**:
   si `Unit == item.Unit` ⇒ `AppliedFactor=1`, `QuantityInBaseUnit=Quantity`;
   si no ⇒ busca `UnitConversion` por `(InventoryItemId, FromUnit=Unit, ToUnit=item.Unit)`;
   si no existe ⇒ `DomainException("No hay conversión definida…")`. `Math.Round(
   Quantity * Factor, 3, MidpointRounding.ToEven)`.
3. Llama `item.RecordReception(BatchNumber, QuantityInBaseUnit, Unit, AppliedFactor,
   CostPerUnit, ExpirationDate, ReceivedAt, SupplierLabel, InvoiceReference, Notes,
   RecordedById, RecordedByLabel)`. El método de dominio:
   - Valida `QuantityInBaseUnit > 0`, `CostPerUnit >= 0`, `ReceivedAt <= DateTimeOffset.UtcNow`,
     `ReceivedAt >= item.CreatedAt` (no se recibió antes de existir el ítem).
   - Crea un `InventoryBatch` y emite `InventoryReceptionRecorded`.
4. Persiste. Retorna `BatchId`.

`RecordInventoryReceptionValidator` (`FluentValidation`):
- `ItemId` NotEmpty.
- `BatchNumber` NotEmpty, max 50.
- `Quantity` GreaterThan(0).
- `Unit` NotEmpty, max 20.
- `CostPerUnit` GreaterThanOrEqualTo(0).
- `ExpirationDate` opcional.
- `ReceivedAt` NotEqual default, debe ser ≤ hoy (la igualdad exacta con hoy
  se permite; sólo bloqueamos futuros).
- `SupplierLabel` max 200 When not null.
- `InvoiceReference` max 100 When not null.
- `Notes` max 500 When not null.
- `RecordedByLabel` max 200 When not null.

**Texto imperativo:** *Toda "entrada normal" de alimento al inventario
pasa por `RecordInventoryReceptionCommand`. `AddBatch` queda para ajustes
manuales técnicos y queda marcado `[Obsolete]`.*

### 4. Evento de dominio `InventoryReceptionRecorded` en proceso, sin outbox.

`Hato.Modules.Inventory.Domain/Events/InventoryReceptionRecorded.cs` define:

```csharp
public sealed record InventoryReceptionRecorded(
    Guid BatchId,
    Guid ItemId,
    decimal QuantityInBaseUnit,
    string UnitRecorded,
    decimal? AppliedFactor,
    DateTimeOffset ReceivedAt,
    string? SupplierLabel,
    string? InvoiceReference,
    string? RecordedBy) : IDomainEvent;
```

Lo emite `InventoryItem.RecordReception(...)` vía `RaiseDomainEvent(...)`
(heredado de `Hato.SharedKernel.Entity`). El consumidor es **cualquiera que
se suscriba vía MediatR** (`INotificationHandler<InventoryReceptionRecorded>`).
**No** se persiste en outbox (Inventario no tiene outbox; `IDomainEvent : INotification`
semánticamente es in-process, ver `src/Shared/Hato.SharedKernel/IDomainEvent.cs:1-12`).

Coherencia con la regla de los padres: Art. 4 dice "todo cambio relevante
es un evento". Una recepción es un cambio de stock (crea un lote nuevo):
es relevante.

**Texto imperativo:** *`InventoryReceptionRecorded` se publica in-process
vía MediatR. No hay outbox. Se reabre si Fase 4 (Purchasing) introduce un
consumidor downstream que exija durabilidad.*

### 5. Endpoint nuevo `POST /api/v1/inventory/items/{itemId}/receptions` con permiso propio.

`src/Hato.Api/Endpoints/InventoryEndpoints.cs` agrega:

```csharp
group.MapPost("/items/{itemId:guid}/receptions", async (
    Guid itemId,
    RecordInventoryReceptionRequest request,
    ISender sender) =>
{
    var command = new RecordInventoryReceptionCommand(
        itemId, request.BatchNumber, request.Quantity, request.Unit,
        request.CostPerUnit, request.ExpirationDate, request.ReceivedAt,
        request.SupplierLabel, request.InvoiceReference, request.Notes,
        request.RecordedById, request.RecordedByLabel);
    var id = await sender.Send(command);
    return Results.Created($"/api/v1/inventory/items/{itemId}/receptions/{id}", new { id });
})
.RequireAuthorization(policy => policy.RequirePermission(SystemPermissions.InventoryReceptionsManage));
```

Aprovecho para cerrar el gap de seguridad preexistente:
`POST /items/{itemId:guid}/batches` y `POST /items/{itemId:guid}/feed-stage`
reciben `RequirePermission(InventoryItemsManage)` simultáneamente. No es
retrabajo, es el costo de oportunidad de tener el archivo abierto.

Permiso: `SystemPermissions.InventoryReceptionsManage = "inventory.receptions.manage"`.
Sembrado por la migración People `SeedInventoryReceptionsManage` (mismo
patrón que `20260811214054_SeedInventoryFeedStagesManage.cs`):
- `permissions`: nueva fila con `code="inventory.receptions.manage"`, `name="Registrar recepciones de inventario"`, `module="Inventory"`.
- `role_permissions`: asignado al rol `admin` (GUID `11111111-…`).
- `Down`: `DELETE` por UUID.

`PeopleModule.cs` agrega la policy `"InventoryReceptionsManage"` al
`AddAuthorization(...)` (línea 117-128).

**Texto imperativo:** *El nuevo endpoint exige `inventory.receptions.manage`.
Los endpoints `POST /batches` y `POST /feed-stage` exigen `inventory.items.manage`
(cierre de gap de seguridad).*

### 6. UI admin-web reemplaza (no duplica) el "Crear lote" por "Recibir alimento".

`clients/admin-web/src/app/components/inventory-item-detail/`
-queda intacto; ya hospeda `<app-inventory-batches-section>`-.
`clients/admin-web/src/app/components/inventory-batches-section/inventory-batches-section.component.ts`
se reescribe para:

- "Crear lote" deja de ser el botón visible; pasa a llamarse "Recibir alimento"
  y la acción abre un formulario con `Proveedor`, `Factura/Guía`, `Fecha de
  recepción`, `Cantidad`, `Unidad`, `Costo por unidad`, `Vencimiento`,
  `Notas`, `Quién registra` (texto libre, mismo patrón que `LotEventsScreen`).
- La unidad por defecto es `item.Unit`; el campo es editable y dispara la
  conversión server-side (si no existe, el backend responde 400 — la UI muestra
  el mensaje).
- El listado de batches muestra `receivedAt` y `supplierLabel` (columnas
  adicionales). Si `createdAt != receivedAt`, badge "Sin fecha declarada".
- Un banner amarillo arriba: "Cuando llegue Purchasing (Fase 4),
  este flujo se reemplazará por 'Recibir orden de compra'. Por ahora,
  registra aquí."

`clients/admin-web/src/app/services/api.service.ts` agrega
`recordInventoryReception(itemId, request)` DTO con `BatchNumber`, `Quantity`,
`Unit`, `CostPerUnit`, `ExpirationDate?`, `ReceivedAt` (ISO-8601 UTC),
`SupplierLabel?`, `InvoiceReference?`, `Notes?`, `RecordedById?`,
`RecordedByLabel?`. El DTO `InventoryBatchDto` extiende con los mismos campos
opcionales.

**Texto imperativo:** *El botón "Crear lote" se reemplaza por "Recibir alimento"
con todos los campos de recepción. El banner documenta la vida útil.*

### 7. Móvil **no** se toca.

Por Art. 9 (offline-first), la reposición es labor de oficina (quien recibe
los sacos en bodega firma la factura). Ningún handler nuevo en
`clients/field-app/`. El catálogo `inventoryBatches` no viaja en el pull
del sync (esto sigue igual) y `GroupFeedConsumption` (que sí viaja)
sigue funcionando porque las recepciones no afectan su escritura.

### 8. Migración EF Core: una sola en Inventory, una sola en People.

**Inventory** — `20260813xxxxxx_AddInventoryReceptionFields.cs` (en
`src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure/Persistence/Migrations/`):

```csharp
public partial class AddInventoryReceptionFields : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        // 1. Columna nueva required con default de backfill
        mb.AddColumn<DateTimeOffset>(
            name: "received_at",
            schema: "inventory",
            table: "inventory_batches",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: null);  // EF no soporta default arbitrary en AddColumn; usamos SQL crudo abajo

        mb.Sql("""
            UPDATE inventory.inventory_batches
            SET received_at = created_at
            WHERE received_at IS NULL;
        """);

        // 2. Resto, nullable
        mb.AddColumn<string>(name: "supplier_label",     schema: "inventory", table: "inventory_batches", type: "character varying(200)", maxLength: 200, nullable: true);
        mb.AddColumn<string>(name: "invoice_reference",  schema: "inventory", table: "inventory_batches", type: "character varying(100)", maxLength: 100, nullable: true);
        mb.AddColumn<string>(name: "notes",              schema: "inventory", table: "inventory_batches", type: "character varying(500)", maxLength: 500, nullable: true);
        mb.AddColumn<Guid>(  name: "recorded_by_id",     schema: "inventory", table: "inventory_batches", type: "uuid",                       nullable: true);
        mb.AddColumn<string>(name: "recorded_by_label",  schema: "inventory", table: "inventory_batches", type: "character varying(200)", maxLength: 200, nullable: true);

        // 3. Índice en received_at para "entradas del último mes"
        mb.CreateIndex(
            name: "i_x_inventory_batches_received_at",
            schema: "inventory",
            table: "inventory_batches",
            column: "received_at");
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.DropIndex(name: "i_x_inventory_batches_received_at", schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "recorded_by_label", schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "recorded_by_id",    schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "notes",             schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "invoice_reference", schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "supplier_label",    schema: "inventory", table: "inventory_batches");
        mb.DropColumn(name: "received_at",       schema: "inventory", table: "inventory_batches");
    }
}
```

**People** — `20260813xxxxxx_SeedInventoryReceptionsManage.cs` (en
`src/Modules/People/Hato.Modules.People.Infrastructure/Persistence/Migrations/`):
mismo patrón que `20260811214054_SeedInventoryFeedStagesManage.cs` con
`code="inventory.receptions.manage"`, GUID pre-asignado, asignación al
rol admin.

## Alternativas consideradas

### B. Adelantar el módulo Purchasing de Fase 4.

- Descartada. La Fase 4 introduce mucho más de lo que el piloto necesita:
  `suppliers` como entidad, `purchase_orders` con evaluación, `purchase_receptions`
  atadas a la PO, NC, CxP, costeo por orden. Esto es trabajo de **al menos**
  4-6 ADRs. Lo que el piloto necesita hoy es trazabilidad del "qué entró,
  cuándo, de quién, con qué factura" — la mitad de eso es metadatos de la
  recepción, la otra mitad es Permiso + UI. Adelantar Purchasing es
  sobredimensionar el piloteo.
- Justificación técnica: el modelo `Supplier` debe ser configurable (Art. 8
  — datos, no enum), y configurarlo para una finca con un solo proveedor
  real es duplicar la información que el operario ya escribió en la factura.
  La mitigación acordada #4 (preservar `SupplierLabel` aunque exista
  `SupplierId`) hace de puente limpio.

### C. Dejar el endpoint `POST /batches` como está, agregar UI con fecha y proveedor como campos opcionales.

- Descartada. Sin evento de dominio, no hay forma de reconstruir "qué pasó"
  desde el feed de eventos. ADR-0004 (historial de eventos JSONB) y la
  práctica de Art. 4 ("todo cambio relevante es un evento") se violan. Sin
  campos separados, no hay cómo distinguir "fecha de creación" de "fecha de
  recepción" — son cosas distintas, y los queries de auditoría las necesitan
  por separado.
- Adicionalmente: mantiene el gap de seguridad de no pedir permiso en el
  endpoint, contrario al resto del módulo (ADR-0007).

### D. No permitir que `AddBatch` siga existiendo (eliminarlo).

- Descartada. Hay un caso real de "ajuste manual" (conciliación contra
  inventario físico de un saco que estuvo siempre ahí y no proviene de
  una compra). Eliminarlo obligaría a inventar una recepción con proveedor
  "N/A", lo cual contamina la auditoría. La regla del repo (Art. 1) más
  la mitigación acordada #4 (preservar etiquetas) prefieren deprecar y
  dejar la puerta abierta.

### E. Outbox para `InventoryReceptionRecorded`.

- Descartada por ahora. Inventario no tiene outbox; inventar outbox para un
  solo evento es deuda injustificada. Cuando llegue Fase 4 y haya un
  consumidor downstream (CxP, costeo por orden), se reabre la decisión y
  se evalúa outbox **para todos** los eventos de inventario.

### F. Permiso compartido `inventory.items.manage`.

- Descartada. Granularidad mata rigidez. Fase 4 va a introducir un rol
  "registrador de compras" que crea recepciones pero no crea ítems del
  catálogo. Si ambos permisos vienen de la misma policy, ese rol no se
  puede expresar. Crear el permiso ahora es 5 minutos; crearlo después
  es reabrir el ADR-0026 inútilmente.

## Consecuencias

### Positivas

- El piloto abre con trazabilidad mínima de recepciones: fecha declarada,
  proveedor, factura, autor. El dueño puede responder "¿cuánto le pagamos
  a X proveedor el mes pasado?" con una query SQL directa o con un query
  futuro del módulo de Reporting.
- El evento `InventoryReceptionRecorded` reabre la conversación sobre
  consumidores cross-module (reporte de consumo vs. entradas, valuación
  de stock promedio ponderado por recepción) sin obligar a implementarlos
  en esta fase.
- El gap de seguridad de `POST /batches` se cierra al pasar por el
  archivo de endpoints (no es un PR extra: la convención del módulo ya
  lo estaba pidiendo).
- El comando `RecordInventoryReceptionCommand` está **abierto a extensión
  sin romper contrato**: cuando llegue Fase 4, agregar `SupplierId?`,
  `PurchaseOrderId?`, `Currency?` como propiedades adicionales del
  record no rompe a callers que aún no las mandan. El handler los ignora
  hasta que existan las tablas. La mitigación #1 del diálogo de cierre
  queda blindada a nivel de tipos.
- `SupplierLabel` se preserva **incluso después** de que exista `SupplierId`
  FK (mitigación #4): la columna no se elimina, el dominio la sigue
  aceptando, los reports históricos siguen funcionando. La deduplicación
  texto→FK se hace por script puntual cuando haya volumen que justifique
  la limpieza.

### Negativas / costos

- `AddBatch` y `RecordReception` coexisten. Riesgo de divergencia:
  alguien crea un batch sin `ReceivedAt` y el reporte pierde ese registro.
  Mitigación: hoy el backfill pone `received_at = created_at`, así que
  **ningún** lote existente queda con `ReceivedAt` faltante. Para lotes
  nuevos, la única ruta es `RecordReception` (el banner de la UI lo
  señala). El `[Obsolete]` en `AddBatch` no rompe compilaciones pero
  mantiene la presión.
- La columna `supplier_label` queda histórica. Si en algún momento la
  finca quiere normalizar nombres ("Agropecuaria XYZ S.A." vs "Agropecuaria
  XYZ" vs "AgroXYZ"), la limpieza es trabajo de script, no del flujo
  normal. Aceptado por la mitigación #4.
- El handler de `RecordReception` hace **dos** queries a `UnitConversion`
  (no una) si la unidad coincide pero un test mal escrito podría no
  detectar un bug. Mitigación: lo cubrimos con un test que mide el
  plan de query.
- El endpoint `POST /batches` queda deprecado con `[Obsolete]` pero
  sigue funcionando. Si se cuela un caller externo que no leemos,
  seguimos rompiendo en silencio. Mitigación: log de warning en el endpoint
  con `ILogger<InventoryEndpoints>` (que ya inyectamos; sólo falta el
  log).
- `received_at` requiere un índice nuevo (`i_x_inventory_batches_received_at`).
  Con <1000 batches/finca el costo es invisible; con >10k empieza a doler.
  Aceptado por ahora — la nueva columna es la fecha **declarada** y el
  reporte "última semana" la va a querer.

### Vida útil del flujo y condición de reversa

**Vida útil declarada:** "deprecado cuando llegue Purchasing (Fase 4)".

**Disparador de reversa explícito** (sincronizado con el de BACKLOG.md):
- El piloto real pierde trazabilidad de compras (el contador no puede
  reconstruir el proveedor de un batch viejo, o el dueño pide un reporte
  de "gasto mensual por proveedor" y no hay cómo).

**Condición de reversa del propio ADR:**
1. Fase 4 arranca antes de lo previsto y se decide que el camino es otro
   (p. ej. atar las recepciones a `purchase_orders` desde el primer día,
   sin un "modo pre-Purchasing"). Entonces este ADR se marca como
   "Reemplazado por ADR-NNNN" y `AddBatch` se elimina junto con
   `RecordReception`.
2. El contador pide más de lo que `SupplierLabel` puede dar (normalización
   de proveedores, direcciones, RUC). Eso es la señal de que **sí**
   necesitamos Fase 4 ya, no en su slot original.
3. La UI admin-web crece a tal punto que mantener el banner de "esto se va"
   resulta vergonzoso. Indicador de que el tiempo de Purchasing llegó.
4. El test de backfill (filas pre-2026-08-13) muestra >5% de filas con
   `CreatedAt` ruidoso (e.g. del año pasado) que haría fea la auditoría.
   Mitigación: si pasa, se reabre y se evalúa `received_at = NULL` con
   UI explícita "Fecha desconocida".

### Lo que NO entra en este ADR (deuda rastreable)

- **Deduplicación de `SupplierLabel` → `Supplier` FK.** Disparador: Fase 4
  ya está en marcha.
- **Reporte "compras por proveedor" / "compras por período".** Disparador:
  el dueño pide ver el gasto del mes en balanceado.
- **Costeo promedio ponderado por recepción** (AVCO) para `InventoryBatch`.
  Hoy el lote tiene su costo propio; cuando se mezclen consumos de varios
  lotes, el costo por animal-día es trivial. Disparador: el reporte de
  FCR (3.5b.4) sale ruidoso por mezclar lotes con precios distintos.
- **Notificación por email al dueño** de cada recepción. Disparador:
  hablamos de <10 recepciones/mes, no se justifica automatizar.
- **Reasignación de `RecordedById` huérfano a `people.users` por nombre**
  (mismo patrón que `20260802163723_AddAuditRecordedBy.cs`). Disparador:
  el dueño edita recepciones y nota que su nombre no aparece como link.

## Refinamientos posteriores a la aceptación (post-auditoría 2026-08-13)

La auditoría de seguridad del PR #94 (ver
[Issue #93](https://github.com/JosephBano/erp-hacienda/issues/93)) encontró
3 hallazgos 🟠 Altos y 4 🟡 Medios. Los que tocan **decisión arquitectónica**
(los demás son detalle de implementación ya cubierto en el cuerpo del ADR):

- **AL-01 — `RecordedById` siempre desde el JWT subject, nunca del request.**
  El handler ignora `request.RecordedById` y lo reemplaza por
  `currentUser.UserId` (derivado del claim `sub` / `NameIdentifier`).
  Justificación: la auditoría del lote no puede ser manipulada por el
  cliente. `RecordedByLabel` se preserva si el operario tipea un valor
  no vacío (UX: "yo registré pero lo hizo el mayordomo"); si no, fallback
  al claim `name` del JWT. Aplicado en commit `bb28d94`.

- **MD-02 — Cap `Quantity` y `CostPerUnit` en 1.000.000.** Tope defensivo
  para impedir `decimal` overflow cuando el operario tipea valores
  absurdos o un script malicioso itera magnitudes máximas. Aplicado en
  commit `397a7f7`. El cap es consistente con la escala del dominio
  (consumo mensual típico de una lechería mediana está muy por debajo).

- **MD-04 — Cap `ReceivedAt ∈ [2020-01-01 UTC, UtcNow + 1min]`.** Piso
  de 2020 porque el sistema no tiene historia anterior; techo con 1 minuto
  de tolerancia por clock skew NTP. Aplicado en commit `d66e539`.

El fix **AL-02** (migración reescrita con patrón 3-step explícito
add-nullable → UPDATE backfill → ALTER COLUMN SET NOT NULL) es detalle de
implementación, no decisión arquitectónica — su comentario en el archivo
de migración lo justifica in situ.
