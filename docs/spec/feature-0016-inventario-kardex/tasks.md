# tasks.md — Desglose ejecutable del núcleo del kardex

> Checklist de las ramas `feature/inventory-stock-ledger` y `feature/inventory-web-movements`,
> agrupadas por el commit de [`plan.md`](./plan.md) al que pertenecen. Cada tarea sigue el
> ciclo de pruebas primero: escribir la prueba, verla fallar, implementar lo mínimo y verla
> pasar. Las restricciones globales y el foco de revisión de `plan.md` aplican a todas.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.
>
> Comandos: `dotnet test <proyecto>` para .NET. En `clients/admin-web`: `npm run lint`,
> `npm test -- --watch=false` y `npm run build`.

---

## Commit 1.1 — Movimiento y calculador

**Contrato que producen estas tareas** (lo usan 1.3, 1.5, 2.2 y feature-0019):

```csharp
public enum MovementKind { OpeningBalance, Reception, Usage, Adjustment }
public enum AdjustmentDirection { Increase, Decrease }

public sealed class InventoryMovement : AuditableEntity {
    public static InventoryMovement OpeningBalance(Guid id, Guid itemId, decimal qtyBase, string unitRecorded,
        decimal appliedFactor, decimal unitCost, bool isCostEstimated, DateTimeOffset occurredAt, Guid? byId, string byLabel);
    public static InventoryMovement Reception(Guid id, Guid itemId, decimal qtyBase, string unitRecorded,
        decimal appliedFactor, decimal unitCost, DateTimeOffset occurredAt, Guid? invoiceId,
        string? batchNumber, DateOnly? expirationDate, Guid? byId, string byLabel, string? notes);
    public static InventoryMovement Usage(Guid id, Guid itemId, decimal qtyBase, string unitRecorded,
        decimal appliedFactor, DateTimeOffset occurredAt, Guid? groupId, Guid? byId, string byLabel, string? notes);
    public static InventoryMovement Adjustment(Guid id, Guid itemId, decimal qtyBase, AdjustmentDirection direction,
        Guid reasonId, DateTimeOffset occurredAt, Guid? byId, string byLabel, string? notes);
    public decimal SignedQuantity { get; } // + para entradas, − para salidas
}

public sealed record StockLedgerLine(InventoryMovement Movement, decimal Balance, decimal AverageCost,
    decimal Value, decimal EstimatedValue, bool IsNegative, decimal? MovementCost);

public static class StockLedgerCalculator {
    public static IReadOnlyList<StockLedgerLine> Compute(IEnumerable<InventoryMovement> movements);
}
```

- [ ] **T1.1.1** Pruebas de invariantes de `InventoryMovement`, en
      `InventoryMovementTests`: cantidad ≤ 0 se rechaza; costo < 0 se rechaza; un ajuste sin
      motivo se rechaza; `OccurredAt` en el futuro se rechaza; `SignedQuantity` es positiva
      en `OpeningBalance`, `Reception` y ajuste `Increase`, y negativa en `Usage` y ajuste
      `Decrease`. Verlas fallar, implementar, verlas pasar.
      **Terminado:** `dotnet test tests/Hato.Modules.Inventory.UnitTests --filter InventoryMovementTests` en verde.
- [ ] **T1.1.2** Regla 1, promedio ponderado. Prueba
      `Entry_with_positive_balance_recomputes_weighted_average`: recepción de 100 kg a $0,50
      y recepción de 50 kg a $0,80 dan saldo 150 y promedio **0,60**, con valor 90,00.
- [ ] **T1.1.3** Regla 3, salidas al promedio vigente. Prueba
      `Usage_is_valued_at_current_average`: tras T1.1.2, una salida de 30 kg vale 18,00 y
      deja saldo 120, valor 72,00 y promedio igual.
- [ ] **T1.1.4** Saldo negativo. Prueba `Usage_beyond_balance_goes_negative_at_last_average`:
      recepción de 10 a $2,00 y salida de 15 dan saldo −5, `IsNegative`, y la salida vale
      30,00.
- [ ] **T1.1.5** Regla 2. Prueba `Entry_after_zero_or_negative_balance_takes_entry_cost`:
      con saldo −5, una recepción de 20 a $3,00 da saldo 15 y promedio **3,00**. Otra prueba
      igual con saldo exactamente 0 tras un ajuste (foco de revisión 4).
- [ ] **T1.1.6** Movimiento con fecha pasada. Prueba
      `Backdated_reception_revalues_later_usages`: la salida del día 3 se calcula con la
      recepción del día 2, aunque esta tenga un `RecordedAt` posterior (foco de revisión 2).
- [ ] **T1.1.7** Regla 5, valor estimado. Prueba
      `Estimated_opening_value_is_consumed_proportionally`: saldo inicial estimado de 100 a
      $1,00 y recepción de 100 a $2,00 (promedio 1,50). Una salida de 100 deja un valor
      estimado de **50,00** de 150,00.
- [ ] **T1.1.8** Regla 6, sin redondeo intermedio. Prueba
      `No_intermediate_rounding`: tres recepciones de 1 kg a $0,10, $0,10 y $0,11 dan
      promedio exacto `0.1033333333…m` (no `0.10m`), y la suma de los valores de las líneas
      es igual al valor final.
- [ ] **T1.1.9** Orden estable. Prueba `Same_occurredAt_orders_by_recordedAt_then_id`: dos
      movimientos con el mismo `OccurredAt` dan el mismo resultado sea cual sea el orden de
      entrada (foco de revisión 1).
- [ ] **T1.1.10** Commit `feat(inventory): add inventory movements and the stock ledger calculator`.

## Commit 1.2 — Catálogos de categorías y motivos

- [ ] **T1.2.1** Pruebas unitarias de `ItemCategory`: el código se normaliza a minúsculas;
      nombre vacío se rechaza; se puede desactivar pero no borrar. Lo mismo para
      `AdjustmentReason`, y además `migration` es `IsSystem`, no se puede elegir desde la
      interfaz ni desactivar.
- [ ] **T1.2.2** Reemplazar el enum por la entidad en `InventoryItem`. La invariante pasa a
      "etapa de alimento solo si `Category.IsFeed`". Adaptar `InventoryItemTests` sin
      cambiar lo que afirman.
      **Terminado:** `dotnet test tests/Hato.Modules.Inventory.UnitTests` en verde.
- [ ] **T1.2.3** Endpoints `GET/POST/PATCH /api/v1/inventory/categories` y
      `/adjustment-reasons`. Pruebas en `CatalogsApiTests`: 200 al listar con
      `inventory.items.read`; 201 al crear "Material genético" con
      `inventory.catalogs.manage`; 403 al crear con `registrar`; 400 con código repetido; 404
      al hacer PATCH de un id inexistente.
- [ ] **T1.2.4** Constante `SystemPermissions.InventoryCatalogsManage` y migración de People
      que la siembra para `admin`. Prueba de la migración, al estilo de
      `SeedInventoryFeedStagesManageMigrationTests`.
- [ ] **T1.2.5** Commit `feat(inventory): turn item categories and adjustment reasons into catalogs`.

## Commit 1.3 — Persistencia y migración de datos

- [ ] **T1.3.1** Prueba `StockLedgerBackfillTests.Balances_are_identical_after_backfill`
      contra PostgreSQL. Carga con el esquema previo dos ítems, tres lotes, un consumo con
      lote explícito y **un consumo PEPS repartido entre dos lotes**. Aplica la migración y
      afirma que, por ítem, el saldo de `StockLedgerCalculator` es igual a Σ `Quantity`
      previo. Verla fallar.
- [ ] **T1.3.2** Prueba `Backfill_adds_visible_migration_adjustment_when_batches_disagree`:
      si la reconstrucción por lote no cuadra, existe exactamente un `Adjustment` con motivo
      `migration` por la diferencia.
- [ ] **T1.3.3** Configuraciones EF (`InventoryMovementConfiguration`, índice
      `(item_id, occurred_at, recorded_at, id)`, `SupplierInvoiceConfiguration` y catálogos) y
      entidad `SupplierInvoice`.
- [ ] **T1.3.4** Migración `AddStockLedger`: esquema, siembra de catálogos (4 categorías, 6
      motivos), FK de categoría con los mismos códigos, y backfill SQL de la spec sec. 6.
      **Terminado:** T1.3.1 y T1.3.2 en verde.
- [ ] **T1.3.5** `verificacion-migracion.sql`: devuelve, por ítem, Σ `Quantity`, el saldo
      derivado y la diferencia. **No debe devolver filas con diferencia ≠ 0.**
      **Terminado:** corre sin errores contra la base de la prueba de T1.3.1.
- [ ] **T1.3.6** Commit `feat(inventory): persist movements and backfill them from batches and consumptions`.

## Commit 1.4 — Los flujos existentes escriben movimientos

- [ ] **T1.4.1** Cambiar `RecordFeedConsumptionFifoApiTests`: el caso "stock insuficiente"
      ahora afirma **201** y un saldo negativo en `GET /balances`. Verlo fallar.
- [ ] **T1.4.2** `RecordGroupFeedConsumptionCommand` crea un `Usage` con `GroupId`. Se
      retiran `DeductFromBatchesFifo` y `ResolveConsumptionBatchId`.
- [ ] **T1.4.3** `RecordInventoryReceptionCommand` y `AddBatch` crean una `Reception`. Las
      pruebas de ADR-0026 quedan en verde sin cambiar su contrato.
- [ ] **T1.4.4** Eliminar `DeductQuantity` y toda escritura de `InventoryBatch.Quantity`.
      **Terminado:** `grep -rn "DeductQuantity\|\.Quantity -=\|Quantity = " src/Modules/Inventory --include=*.cs | grep -v Migrations` no devuelve nada.
- [ ] **T1.4.5** `ExpiringBatchesReader` usa lo restante derivado por PEPS. Prueba: un lote
      que vence en 10 días pero ya consumido **no** aparece; uno que conserva 5 kg sí.
- [ ] **T1.4.6** Prueba en `SyncPushFeedConsumptionTests` con el payload exacto que envía
      hoy `feedConsumptionService.ts`: se acepta y produce un `Usage`, también cuando excede
      el saldo.
- [ ] **T1.4.7** `dotnet test Hato.sln` completo en verde.
- [ ] **T1.4.8** Commit `refactor(inventory): route receptions and feed consumptions through movements`.

## Commit 1.5 — Saldos, kardex y saldo inicial en la API

- [ ] **T1.5.1** `GET /balances`. Pruebas en `StockBalancesApiTests`: 200 con saldo, promedio,
      valor, `isNegative` y `estimatedValuePercent`; 401 sin sesión.
- [ ] **T1.5.2** `GET /items/{id}/ledger?from&to`. Pruebas en `ItemLedgerApiTests`: líneas
      en orden y con saldo acumulado; el saldo de la primera línea del rango incluye lo
      anterior a `from`; 404 con ítem inexistente; 400 con `from > to`.
- [ ] **T1.5.3** `POST /opening-balances`. Pruebas en `OpeningBalancesApiTests`: 201 y
      movimientos `OpeningBalance` con `IsCostEstimated`; 403 con `registrar`; 400 con líneas
      vacías; **400 con el mismo ítem dos veces en la hoja, sin crear ningún movimiento**
      (foco de revisión 5).
- [ ] **T1.5.4** Prueba `Item_without_movements_has_zero_balance`: aparece en
      `GET /balances` con saldo 0 y valor 0 (foco de revisión 3).
- [ ] **T1.5.5** Constante `InventoryOpeningManage`, migración de People que la siembra para
      `admin` y migración que siembra los submódulos `inventory.receptions`,
      `inventory.usages` e `inventory.adjustments`, encendidos.
- [ ] **T1.5.6** DTO `StockBalanceDto(Guid ItemId, decimal Balance, string Unit, DateTimeOffset ComputedAt)`
      en `Contracts`, para feature-0018.
- [ ] **T1.5.7** Commit `feat(inventory): expose balances, item ledger and opening balances`.

## Commit 1.6 — Pantallas del panel

- [ ] **T1.6.1** `stock-balances`: prueba que un ítem negativo se marca, y que el % estimado
      se muestra con un decimal. Implementar.
- [ ] **T1.6.2** `item-ledger`: prueba que las fechas se muestran en `America/Guayaquil` y
      que el filtro de fechas llama a la API con UTC.
- [ ] **T1.6.3** `opening-count`: hoja con todos los ítems activos, cantidad, unidad y
      costo. Prueba que las filas vacías no se envían y que el envío marca `isCostEstimated`.
- [ ] **T1.6.4** `inventory-categories`: crear, renombrar y desactivar; la marca "es
      alimento".
- [ ] **T1.6.5** `inventory-batches-section` muestra lo restante derivado, no `Quantity`.
- [ ] **T1.6.6** **Terminado:** lint, pruebas y build de `clients/admin-web` en verde.
- [ ] **T1.6.7** Commit `feat(admin-web): add stock balances, ledger, opening count and category screens`.

## Cierre del incremento 1

- [ ] **TC1.1** `dotnet test Hato.sln` completo en verde.
- [ ] **TC1.2** Desplegar en staging y correr `verificacion-migracion.sql`: **ninguna fila
      con diferencia ≠ 0**. Es compuerta: si falla, no se promueve.
- [ ] **TC1.3** E2E-1 a E2E-4 de [`test-e2e.md`](./test-e2e.md) en staging.
- [ ] **TC1.4** PR con la descripción de `plan.md`.

---

## Commit 2.1 — Recepciones con factura opcional

- [ ] **T2.1.1** Pruebas en `RecordReceptionsApiTests`: 201 con factura y tres líneas (una
      `SupplierInvoice` y tres `Reception` enlazadas); 201 sin factura y con nota de
      respaldo; 400 con costo negativo; 400 con número de factura vacío si se envía
      `invoice`; 403 con `registrar`.
- [ ] **T2.1.2** `RecordReceptionsCommand` y su validador. `POST /items/{id}/receptions`
      delega con una línea.
      **Terminado:** `RecordInventoryReceptionApiTests` y `RecordInventoryReceptionAuthApiTests` sin cambios y en verde.
- [ ] **T2.1.3** Commit `feat(inventory): record multi-line receptions with an optional supplier invoice`.

## Commit 2.2 — Salidas y ajustes

- [ ] **T2.2.1** Pruebas en `UsagesApiTests`: 201 con destino de grupo; 201 sin destino;
      **201 cuando deja el saldo negativo**; 400 con unidad desconocida; 201 idempotente con
      el mismo `id` (un solo movimiento).
- [ ] **T2.2.2** Pruebas en `AdjustmentsApiTests`: 201 disminución por "merma"; 201 aumento
      por "conteo físico"; 400 sin motivo; 400 con el motivo `migration`; **403 con
      `registrar`** (criterio 6 de la spec).
- [ ] **T2.2.3** `RecordUsageCommand` (lo reusa feature-0018) y `RecordAdjustmentCommand`.
      `RecordGroupFeedConsumptionCommand` delega en `RecordUsageCommand`.
- [ ] **T2.2.4** Constantes y migración de People: `InventoryUsagesRecord` para `admin`,
      `registrar`, `veterinarian` y todo rol con `inventory.feed-consumptions.record`;
      `InventoryAdjustmentsManage` solo para `admin`. Prueba de la migración.
- [ ] **T2.2.5** Commit `feat(inventory): record usages and reasoned adjustments`.

## Commit 2.3 — Alertas con cierre automático

- [ ] **T2.3.1** `AlertResolveTests`: `Resolve` marca `IsResolved` y `ResolvedAt`; resolver
      dos veces no cambia la primera fecha; una alerta descartada puede resolverse después.
- [ ] **T2.3.2** Pruebas en `StockAlertsTests`: el saldo negativo genera
      `INVENTORY_NEGATIVE_BALANCE` crítica; tras una recepción que lo corrige, la siguiente
      generación la resuelve; el stock bajo genera `INVENTORY_LOW_STOCK`; **no se duplica**
      una alerta activa; con `inventory` apagado no se genera ninguna; una alerta
      `INVENTORY_BATCH_EXPIRING` se resuelve cuando lo restante del lote llega a 0.
- [ ] **T2.3.3** `IStockAlertsReader` en `Contracts` e implementación. `GenerateAlertsCommand`
      lo consume. Migración de Tasks para `is_resolved` y `resolved_at`.
- [ ] **T2.3.4** Commit `feat(tasks): add stock alerts that resolve themselves`.

## Commit 2.4 — Pantallas de movimientos

- [ ] **T2.4.1** `stock-reception`: prueba que la factura es opcional y que se pueden agregar
      y quitar líneas.
- [ ] **T2.4.2** `stock-usage`: prueba que el saldo negativo muestra un aviso y deja guardar.
- [ ] **T2.4.3** `stock-adjustment`: prueba que el motivo es obligatorio y que el motivo
      `migration` no aparece en la lista.
- [ ] **T2.4.4** `adjustment-reasons`: crear y desactivar.
- [ ] **T2.4.5** **Terminado:** lint, pruebas y build del panel en verde.
- [ ] **T2.4.6** Commit `feat(admin-web): add reception, usage, adjustment and reason screens`.

## Cierre del incremento 2

- [ ] **TC2.1** `dotnet test Hato.sln` completo en verde.
- [ ] **TC2.2** E2E-5 a E2E-9 de [`test-e2e.md`](./test-e2e.md) en staging.
- [ ] **TC2.3** PR con la descripción de `plan.md`.
