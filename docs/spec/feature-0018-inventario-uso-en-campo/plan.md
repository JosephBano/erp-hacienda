# plan.md — Ejecución de la rama `feature/inventory-field-usage`

> **Qué es este documento.** Dice en qué orden y en qué commits se construye lo que
> [`spec.md`](./spec.md) decidió. Las casillas están en [`tasks.md`](./tasks.md) y la
> verificación en un teléfono físico en [`test-e2e.md`](./test-e2e.md).

**Objetivo:** que el personal registre el uso de alimento y fármacos desde el galpón, sin
señal, viendo un saldo con fecha y con aviso sin bloqueo.

**Enfoque:** una rama y **cuatro commits**. Empieza cuando el incremento 2 (feature-0016) está
en `develop`, porque reutiliza `RecordUsageCommand`.

**Compuerta:** E2E-5 (actualizar desde la versión instalada sin perder el outbox) tiene que
pasar en un teléfono físico antes de promover.

**Dependencia de producción:** distribución de Android (feature-0014).

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

- **Ninguna operación de registro depende de red** (regla 10). El registro local y la
  entrada del outbox se escriben en la misma transacción de WatermelonDB.
- **El servidor nunca rechaza un uso por saldo** (D3). El único rechazo posible es una
  unidad desconocida.
- **El contrato de `recordfeedconsumption` no cambia** (C1).
- **`inventoryBalances` no lleva costos** (spec sec. 5).
- **Los diálogos son de la propia pantalla**, no `Alert` del sistema.
- **La migración del esquema local es aditiva.** No toca tablas existentes ni
  `sync_outbox`.

## Foco de revisión

1. **Dos usos del mismo ítem pendientes en el mismo teléfono.** El saldo mostrado descuenta
   los dos, no solo el último → T3.3.
2. **Un uso registrado en sacos cuando el saldo está en kg.** El descuento local convierte
   con el factor del ítem, igual que el servidor → T3.3.
3. **Un pull que llega mientras hay usos pendientes.** El nuevo `balance` ya puede incluir
   algunos que el servidor aceptó. No se descuentan dos veces: solo se restan las entradas
   del outbox que siguen pendientes → T3.4.
4. **Un ítem que el servidor deja de enviar** (desactivado o sin permiso). Desaparece del
   saldo local y no queda un saldo viejo huérfano → T2.3.
5. **Registrar con la hora del teléfono mal puesta** (en el futuro). El servidor rechaza una
   fecha futura (invariante de ADR-0041), así que el teléfono no deja elegir una fecha
   posterior a "ahora" → T3.2.

---

## Índice

1. [Commit 1 — Operación de sync `recordinventoryusage`](#commit-1--operación-de-sync-recordinventoryusage)
2. [Commit 2 — Colección `inventoryBalances`](#commit-2--colección-inventorybalances)
3. [Commit 3 — Servicio y saldo local](#commit-3--servicio-y-saldo-local)
4. [Commit 4 — Pantalla "Registrar uso"](#commit-4--pantalla-registrar-uso)
5. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
6. [Descripción del PR](#descripción-del-pr)

---

## Commit 1 — Operación de sync `recordinventoryusage`

`feat(sync): accept inventory usages pushed from the field`

**Archivos:**
- Modificar: `src/Hato.Api/Sync/PushSyncCommands.cs` (nuevo `case`, payload
  `RecordInventoryUsagePushPayload`), que delega en `RecordUsageCommand` de feature-0016.
- Crear: `tests/Hato.Sync.IntegrationTests/SyncPushInventoryUsageTests.cs`.

**Verificación:** `dotnet test tests/Hato.Sync.IntegrationTests` en verde.

## Commit 2 — Colección `inventoryBalances`

`feat(sync): pull derived inventory balances to the field`

**Archivos:**
- Modificar: `src/Hato.Api/Sync/SyncPullQueries.cs` (colección completa, permiso
  `inventory.items.read`, DTO `StockBalanceDto` de feature-0016).
- Modificar: `clients/field-app/src/database/schema.ts`, `migrations.ts` y `models.ts`
  (tabla `inventory_balances`), y `services/syncEngine.ts` (reemplazo completo de la tabla).
- Crear: `tests/Hato.Sync.IntegrationTests/SyncPullInventoryBalancesTests.cs` y las pruebas
  del motor en `clients/field-app/src/services/__tests__/`.

## Commit 3 — Servicio y saldo local

`feat(field-app): queue inventory usages and show balances with their date`

**Archivos:**
- Crear: `clients/field-app/src/services/inventoryUsageService.ts` e
  `inventoryBalanceService.ts`, con sus pruebas.
- Modificar: `services/__tests__/pushPayloadContracts.test.ts` (contrato del payload nuevo).

## Commit 4 — Pantalla "Registrar uso"

`feat(field-app): add the record usage screen`

**Archivos:**
- Crear: `clients/field-app/src/screens/RecordUsageScreen.tsx` y su prueba.
- Modificar: `screens/ActivitiesHub.tsx` (entrada con `canShow('inventory.usages')`) y
  `App.tsx` (ruta).

**Verificación:** `npm run typecheck && npm run lint && npm test` en `clients/field-app`, y
[`test-e2e.md`](./test-e2e.md) en un teléfono físico.

## Orden, dependencias y puntos de no retorno

```
(feature-0016 incremento 2 en develop)
  └─ 1 push
       └─ 2 pull + esquema local ── PUNTO DE NO RETORNO en teléfonos
            └─ 3 servicios
                 └─ 4 pantalla ── COMPUERTA: E2E-5 en teléfono físico
```

**Punto de no retorno: la migración del esquema local (commit 2) instalada en un teléfono.**
WatermelonDB no baja de versión. Por eso la migración es aditiva y E2E-5 es compuerta.

## Descripción del PR

**Título:** `feat: record inventory usage from the field, offline`

**Cuerpo:** qué (operación de sync, saldos con fecha, pantalla); por qué (spec sec. 1);
decisiones C1–C4 y D3/D5/D7/D9; qué **no** incluye (recepciones, ajustes o transformación en
el teléfono; vínculo con animales); cómo probar (`test-e2e.md`, en teléfono físico); riesgo
(migración local sin vuelta atrás, mitigada con E2E-5).
