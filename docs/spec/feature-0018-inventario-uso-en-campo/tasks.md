# tasks.md — Desglose ejecutable de `feature/inventory-field-usage`

> Checklist agrupada por el commit de [`plan.md`](./plan.md). Cada tarea sigue el ciclo de
> pruebas primero. Las restricciones globales y el foco de revisión de `plan.md` aplican a
> todas.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

**Contratos:**

```csharp
// Push (servidor)
public sealed record RecordInventoryUsagePushPayload(Guid ItemId, decimal Quantity, string Unit,
    DateTimeOffset OccurredAt, Guid? GroupId, string? Notes);
// Pull: StockBalanceDto(Guid ItemId, decimal Balance, string Unit, DateTimeOffset ComputedAt), de feature-0016.
```

```ts
// Teléfono
export interface InventoryUsageInput {
  itemId: string; quantity: number; unit: string; occurredAt: Date; groupId?: string; notes?: string;
}
export class InventoryUsageService { record(input: InventoryUsageInput): Promise<string /* id del uso */> }
export interface DisplayedBalance { itemId: string; balance: number; unit: string; computedAt: Date; pendingCount: number }
export class InventoryBalanceService {
  displayed(itemId: string): Promise<DisplayedBalance | null>;
  wouldGoNegative(itemId: string, quantity: number, unit: string): Promise<boolean>;
}
```

---

## Commit 1 — Operación de sync `recordinventoryusage`

- [ ] **T1.1** Pruebas en `SyncPushInventoryUsageTests`: se acepta y crea un `Usage`; la
      misma operación dos veces crea **un** solo movimiento; con saldo insuficiente se
      acepta; con unidad desconocida se rechaza con un motivo legible; con `groupId` lo
      guarda; con un usuario sin `inventory.usages.record` se rechaza por permiso, igual que
      las demás operaciones.
- [ ] **T1.2** Prueba: con `inventory.usages` apagado, la operación **se acepta** (ADR-0019,
      regla 4).
- [ ] **T1.3** Implementar el `case "recordinventoryusage"` delegando en `RecordUsageCommand`.
- [ ] **T1.4** Commit `feat(sync): accept inventory usages pushed from the field`.

## Commit 2 — Colección `inventoryBalances`

- [ ] **T2.1** Pruebas en `SyncPullInventoryBalancesTests`: la colección llega completa en
      cada pull, aunque el cursor sea reciente; no incluye ningún campo de costo; sin
      `inventory.items.read` no llega.
- [ ] **T2.2** Implementar en `SyncPullQueries.cs`.
- [ ] **T2.3** Teléfono: tabla `inventory_balances` (migración aditiva) y reemplazo completo en
      el motor. Prueba: un ítem que ya no viene en el pull desaparece de la tabla local (foco
      de revisión 4).
- [ ] **T2.4** Prueba: la migración del esquema local conserva las filas de `sync_outbox`.
- [ ] **T2.5** Commit `feat(sync): pull derived inventory balances to the field`.

## Commit 3 — Servicio y saldo local

- [ ] **T3.1** `inventoryUsageService.test.ts`: `record` escribe el uso local y la entrada
      `recordinventoryusage` del outbox **en la misma escritura**; si la escritura falla, no
      queda ninguna de las dos.
- [ ] **T3.2** Prueba: `record` rechaza en el teléfono una `occurredAt` posterior a "ahora"
      (foco de revisión 5).
- [ ] **T3.3** `inventoryBalanceService.test.ts`: saldo 100 kg y dos usos pendientes de 1
      `saco40kg` y 10 kg dan un saldo mostrado de **50 kg**, con `pendingCount` 2 (focos de
      revisión 1 y 2). `wouldGoNegative` con 60 kg devuelve `true`.
- [ ] **T3.4** Prueba: después de un pull cuyo `balance` ya incluye uno de los dos usos (que
      salió del outbox), el saldo mostrado solo descuenta el que sigue pendiente (foco de
      revisión 3).
- [ ] **T3.5** Ampliar `pushPayloadContracts.test.ts` con el contrato de
      `recordinventoryusage`, con los mismos nombres de campo que `RecordInventoryUsagePushPayload`.
- [ ] **T3.6** Commit `feat(field-app): queue inventory usages and show balances with their date`.

## Commit 4 — Pantalla "Registrar uso"

- [ ] **T4.1** `RecordUsageScreen.test.tsx`: el selector de unidad solo ofrece las unidades
      del ítem; la fecha por defecto es ahora; se muestra "118,5 kg (al sincronizar de hoy,
      7:02)".
- [ ] **T4.2** Prueba: si `wouldGoNegative`, aparece el diálogo propio con "Registrar igual"
      y "Revisar". "Registrar igual" guarda y "Revisar" no.
- [ ] **T4.3** Prueba: con `inventory.usages` apagado, la entrada no aparece en
      `ActivitiesHub`.
- [ ] **T4.4** **Terminado:** `npm run typecheck && npm run lint && npm test` en verde.
- [ ] **T4.5** Commit `feat(field-app): add the record usage screen`.

---

## Cierre

- [ ] **TC.1** `dotnet test Hato.sln` completo en verde.
- [ ] **TC.2** [`test-e2e.md`](./test-e2e.md) completo en un teléfono físico. E2E-5 es
      compuerta.
- [ ] **TC.3** PR con la descripción de `plan.md`.
