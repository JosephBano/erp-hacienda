# tasks.md — Desglose ejecutable de `feature/inventory-transformations`

> Checklist agrupada por el commit de [`plan.md`](./plan.md). Cada tarea sigue el ciclo de
> pruebas primero. Las restricciones globales y el foco de revisión de `plan.md` aplican a
> todas.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

**Contratos que agrega esta rama** (sobre los de feature-0016):

```csharp
public enum MovementKind { OpeningBalance, Reception, Usage, Adjustment, TransformationInput, TransformationOutput }

public static InventoryMovement TransformationInput(Guid id, Guid itemId, decimal qtyBase, string unitRecorded,
    decimal appliedFactor, DateTimeOffset occurredAt, Guid orderId, Guid? byId, string byLabel);
public static InventoryMovement TransformationOutput(Guid id, Guid itemId, decimal qtyBase, string unitRecorded,
    decimal appliedFactor, DateTimeOffset occurredAt, Guid orderId, Guid? byId, string byLabel);

public sealed class TransformationOrder : AuditableEntity {
    public DateTimeOffset OccurredAt { get; }
    public decimal ServiceCost { get; }
    public string? ServiceDescription { get; }
    public string? Notes { get; }
}

// El calculador recibe también los costos de servicio por orden:
public static IReadOnlyList<StockLedgerLine> Compute(IEnumerable<InventoryMovement> movements,
    IReadOnlyDictionary<Guid, decimal>? serviceCostByOrder = null);
```

---

## Commit 1 — Tipos de movimiento y regla 4 del calculador

- [ ] **T1.1** Prueba `Output_cost_is_inputs_at_average_plus_service_over_quantity`, el
      criterio 1 de la spec: maíz 300 kg a promedio $0,40, soya 100 kg a $0,60, núcleo 10 kg a
      $2,00 y servicio $20, producto 400 kg → costo unitario **0,55** y valor 220,00. Las
      salidas de insumos bajan los saldos de maíz, soya y núcleo.
- [ ] **T1.2** Prueba `Zero_service_cost_is_valid`: la misma orden con servicio 0 → 0,50
      (foco de revisión 4).
- [ ] **T1.3** Prueba `Late_backdated_input_reception_changes_output_cost`: si se agrega
      después una recepción de maíz con fecha anterior a la orden que sube su promedio a
      0,46, el costo del producto pasa a (138 + 60 + 20 + 20) ÷ 400 = **0,595** (foco de
      revisión 2).
- [ ] **T1.4** Prueba `Input_without_prior_entries_is_valued_at_zero_and_flagged`: el
      insumo vale 0, la línea del producto lleva la marca, y no hay excepción ni `NaN` (foco
      de revisión 1).
- [ ] **T1.5** Prueba `Output_entry_updates_product_average_with_rule_1`: el producto ya
      tenía 100 kg a $0,50; tras la orden de T1.1, el promedio es (50 + 220) ÷ 500 = **0,54**.
- [ ] **T1.6** Implementar los tipos y la regla 4. Las pruebas de feature-0016 siguen en
      verde.
- [ ] **T1.7** Commit `feat(inventory): value transformation outputs from their inputs and service cost`.

## Commit 2 — Orden de transformación, persistencia y API

- [ ] **T2.1** Pruebas en `TransformationsApiTests`: 201 con el costo unitario calculado en la
      respuesta; 400 sin insumos; 400 con el producto repetido como insumo; **400 con el
      mismo insumo en dos líneas** (foco de revisión 5); 400 con `serviceCost` negativo; 403
      con `registrar` (criterio 5 de la spec).
- [ ] **T2.2** Prueba: insumos en `saco40kg` y producto en kg dan el mismo costo que todo en
      kg (foco de revisión 3).
- [ ] **T2.3** Prueba de atomicidad: si falla la escritura (por ejemplo, un ítem inexistente
      en la segunda línea), no queda ningún movimiento ni la orden (criterio 3).
- [ ] **T2.4** Prueba: con `inventory.transformations` apagado, el `POST` se acepta (criterio
      4).
- [ ] **T2.5** `GET /transformations?from&to` devuelve insumos, producto, costo y merma
      implícita. Prueba con el caso de T1.1: merma 10 kg.
- [ ] **T2.6** Entidad, configuración EF, migración `AddTransformationOrders`, comando,
      consulta, endpoints, permiso y siembra del submódulo.
- [ ] **T2.7** **Terminado:** `grep -rni "feed" src/Modules/Inventory/Hato.Modules.Inventory.Domain/Transformations src/Modules/Inventory/Hato.Modules.Inventory.Application/Transformations`
      no devuelve nada (criterio 6).
- [ ] **T2.8** Commit `feat(inventory): record transformation orders`.

## Commit 3 — Pantalla "Preparar alimento"

- [ ] **T3.1** `prepare-feed.component.spec.ts`: agregar y quitar insumos; cada insumo muestra
      su saldo actual; el costo unitario estimado se muestra antes de guardar; un insumo que
      quedaría en negativo muestra aviso y deja guardar.
- [ ] **T3.2** Prueba: con `inventory.transformations` apagado, la entrada no está en el menú.
- [ ] **T3.3** **Terminado:** lint, pruebas y build del panel en verde.
- [ ] **T3.4** Commit `feat(admin-web): add the prepare feed screen`.

---

## Cierre

- [ ] **TC.1** `dotnet test Hato.sln` completo en verde.
- [ ] **TC.2** [`test-e2e.md`](./test-e2e.md) completo en staging.
- [ ] **TC.3** PR con la descripción de `plan.md`.
