# plan.md — Ejecución del núcleo del kardex (incrementos 1 y 2)

> **Qué es este documento.** Dice en qué orden y en qué commits se construye lo que
> [`spec.md`](./spec.md) decidió. El desglose con casillas, pasos TDD y criterio de terminado
> está en [`tasks.md`](./tasks.md). La verificación manual, que también es el guion de
> aceptación con el cliente, está en [`test-e2e.md`](./test-e2e.md).

**Objetivo:** que el cliente cargue su bodega con un conteo inicial y la lleve desde el
panel (recepciones, salidas y ajustes), con un saldo que siempre se explica movimiento por
movimiento.

**Enfoque:** **dos ramas**, una por incremento, cada una con su PR y su promoción a `main`.
- `feature/inventory-stock-ledger` (incremento 1): **seis commits**.
- `feature/inventory-web-movements` (incremento 2): **cuatro commits**.

**Compuertas que detienen el trabajo si fallan:**
- La prueba de equivalencia de la migración (commit 1.3).
- `verificacion-migracion.sql` corrido en staging antes de promover el incremento 1.

**Spec:** [`spec.md`](./spec.md). **Modelo:** [ADR-0041](../../adr/0041-inventario-kardex-derivado.md).

---

## Restricciones globales

- **Nada se edita ni se borra en `InventoryMovement`.** Las correcciones son movimientos
  nuevos (Art. 1, D1).
- **Saldo, promedio y valor nunca se guardan**, se calculan al leer (D1).
- **Dinero en `decimal`, sin redondeo intermedio.** Se redondea solo al presentar, a 2
  decimales para dinero y 4 para costo unitario (spec sec. 5.3, regla 6).
- **Fechas en UTC en persistencia.** `America/Guayaquil` solo en la interfaz (regla 6 de
  `AGENTS.md`).
- **`POST /usages` y la sync nunca rechazan por saldo** (D3).
- **El contrato de `POST /feed-consumptions` y de `recordfeedconsumption` no cambia**
  (spec sec. 7.1).
- **Cada migración de esquema es nueva.** No se edita ninguna ya mergeada (regla 7).
- **Pruebas de persistencia y API contra PostgreSQL real** con `TestDatabase` de
  `tests/Hato.TestSupport`. Si Docker no levanta contenedores, `HATO_TEST_POSTGRES` (regla 5).
- **Ninguna dependencia NuGet o npm nueva** (regla 2).
- **Nada de `if` por categoría o producto.** Lo que varía es dato (Art. 8).

## Foco de revisión

Situaciones que la spec implica y que ninguna prueba de una sola tarea cubre por sí sola.
Cada una tiene su prueba asignada en `tasks.md`:

1. **Dos movimientos con el mismo `OccurredAt`.** El orden tiene que ser estable (por
   `RecordedAt` y luego por `Id`), o el saldo intermedio cambia entre consultas → T1.1.9.
2. **Una recepción con fecha anterior a salidas ya registradas** (factura cargada tarde).
   Las salidas posteriores tienen que revalorarse al nuevo promedio sin tocar nada guardado
   → T1.1.6.
3. **Un ítem sin ningún movimiento.** `GET /balances` lo devuelve con saldo 0 y valor 0, no
   lo omite ni falla → T1.5.4.
4. **Un ajuste que deja el saldo exactamente en 0 y una entrada posterior.** Se aplica la
   regla 2 (el promedio pasa a ser el costo de la entrada), no una división por cero →
   T1.1.5.
5. **Una hoja de conteo con el mismo ítem dos veces.** Se rechaza la hoja entera con
   Problem Details: sumarlas en silencio escondería un error de conteo → T1.5.3.

---

## Índice

**Rama `feature/inventory-stock-ledger` (incremento 1)**
1. [Commit 1.1 — Movimiento y calculador](#commit-11--movimiento-y-calculador)
2. [Commit 1.2 — Catálogos de categorías y motivos](#commit-12--catálogos-de-categorías-y-motivos)
3. [Commit 1.3 — Persistencia y migración de datos](#commit-13--persistencia-y-migración-de-datos)
4. [Commit 1.4 — Los flujos existentes escriben movimientos](#commit-14--los-flujos-existentes-escriben-movimientos)
5. [Commit 1.5 — Saldos, kardex y saldo inicial en la API](#commit-15--saldos-kardex-y-saldo-inicial-en-la-api)
6. [Commit 1.6 — Pantallas del panel](#commit-16--pantallas-del-panel)

**Rama `feature/inventory-web-movements` (incremento 2)**

7. [Commit 2.1 — Recepciones con factura opcional](#commit-21--recepciones-con-factura-opcional)
8. [Commit 2.2 — Salidas y ajustes](#commit-22--salidas-y-ajustes)
9. [Commit 2.3 — Alertas con cierre automático](#commit-23--alertas-con-cierre-automático)
10. [Commit 2.4 — Pantallas de movimientos](#commit-24--pantallas-de-movimientos)

11. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
12. [Descripción de los PR](#descripción-de-los-pr)

---

## Commit 1.1 — Movimiento y calculador

`feat(inventory): add inventory movements and the stock ledger calculator`

**Por qué acá:** es dominio puro, sin base ni API. Todo lo demás depende de sus tipos y de
su resultado, y es lo que más reglas tiene (spec sec. 5.3).

**Archivos:**
- Crear: `src/Modules/Inventory/Hato.Modules.Inventory.Domain/Movements/InventoryMovement.cs`,
  `MovementKind.cs`, `AdjustmentDirection.cs`.
- Crear: `src/Modules/Inventory/Hato.Modules.Inventory.Domain/Ledger/StockLedgerCalculator.cs`
  y `StockLedgerLine.cs`.
- Crear: `tests/Hato.Modules.Inventory.UnitTests/Ledger/StockLedgerCalculatorTests.cs` y
  `tests/Hato.Modules.Inventory.UnitTests/Movements/InventoryMovementTests.cs`.

**Verificación:** `dotnet test tests/Hato.Modules.Inventory.UnitTests` en verde, con las
pruebas nuevas de T1.1.

## Commit 1.2 — Catálogos de categorías y motivos

`feat(inventory): turn item categories and adjustment reasons into catalogs`

**Por qué acá:** el enum `ItemCategory` tiene que convertirse en entidad antes de la
migración de datos (1.3), que ya escribe la FK. Los motivos de ajuste existen antes de que
la migración cree el ajuste `migration`.

**Archivos:**
- Crear: `Domain/Catalogs/ItemCategory.cs` (entidad), `Domain/Catalogs/AdjustmentReason.cs`.
- Borrar: `Domain/ItemCategory.cs` (el enum). Modificar: `InventoryItem.cs`, donde la
  invariante de etapa de alimento pasa a leer `Category.IsFeed`.
- Crear: `Application/Catalogs/` (consultas y comandos de ambos catálogos, con validadores
  FluentValidation).
- Modificar: `src/Hato.Api/Endpoints/InventoryEndpoints.cs` (rutas `/categories` y
  `/adjustment-reasons`).
- Modificar: `src/Modules/People/Hato.Modules.People.Domain/UserRole.cs` (constante
  `InventoryCatalogsManage`) y crear la migración de People que la siembra para `admin`.
- Crear: pruebas unitarias de ambos catálogos y
  `tests/Hato.Modules.Inventory.IntegrationTests/Api/CatalogsApiTests.cs`.

**Verificación:** las pruebas de `InventoryItemTests` sobre la etapa de alimento siguen en
verde con la categoría como entidad.

## Commit 1.3 — Persistencia y migración de datos

`feat(inventory): persist movements and backfill them from batches and consumptions`

**Por qué acá:** los flujos (1.4) no pueden escribir movimientos antes de que exista su
tabla, y la tabla no puede existir vacía en staging: tiene que nacer con la historia
reconstruida.

**Archivos:**
- Crear: `Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs`,
  `SupplierInvoiceConfiguration.cs` y la configuración de los catálogos.
- Crear: `Domain/Invoices/SupplierInvoice.cs` (solo la entidad; su endpoint es del 2.1).
- Crear la migración con
  `dotnet ef migrations add AddStockLedger --project src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure --startup-project src/Hato.Api`.
  Contiene el esquema, la siembra de los catálogos y el **backfill en SQL** (spec sec. 6,
  pasos 1 a 4).
- Crear: `docs/spec/feature-0016-inventario-kardex/verificacion-migracion.sql`.
- Crear: `tests/Hato.Modules.Inventory.IntegrationTests/Migrations/StockLedgerBackfillTests.cs`.

**Verificación (compuerta):** `StockLedgerBackfillTests` en verde. Si no cuadra, **no se
sigue**: se corrige la migración.

## Commit 1.4 — Los flujos existentes escriben movimientos

`refactor(inventory): route receptions and feed consumptions through movements`

**Por qué acá:** después de 1.3, cualquier camino que siga escribiendo solo en
`InventoryBatch.Quantity` crea divergencia. Este commit cierra todos esos caminos a la vez.

**Archivos:**
- Modificar: `Application/Receptions/RecordInventoryReceptionCommand.cs` (crea una
  `Reception`), `InventoryItem.RecordReception` y `AddBatch`.
- Modificar: `Application/Consumptions/RecordGroupFeedConsumptionCommand.cs`: crea un `Usage`
  con `GroupId`, **ya no descuenta ni rechaza por saldo**, y se retira
  `DeductFromBatchesFifo`.
- Modificar: `Domain/InventoryItem.cs`: `DeductQuantity` y toda escritura de `Quantity` se
  eliminan (criterio 1 de la spec).
- Modificar: `Application/CrossModule/ExpiringBatchesReader.cs`, que lee lo restante
  derivado (spec sec. 5.4).
- Modificar las pruebas existentes que afirmaban el rechazo por saldo
  (`RecordFeedConsumptionFifoApiTests.cs`), que ahora afirman aceptación y saldo negativo.
- Crear: prueba de integración en `tests/Hato.Sync.IntegrationTests/SyncPushFeedConsumptionTests.cs`
  con el payload actual de la app instalada.

**Verificación:** `grep -rn "DeductQuantity\|\.Quantity -=\|Quantity = " src/Modules/Inventory --include=*.cs | grep -v Migrations`
no devuelve nada. La suite completa en verde.

## Commit 1.5 — Saldos, kardex y saldo inicial en la API

`feat(inventory): expose balances, item ledger and opening balances`

**Por qué acá:** necesita el calculador (1.1), los datos (1.3) y los flujos ya coherentes
(1.4). Es lo que consume el panel (1.6).

**Archivos:**
- Crear: `Application/Ledger/GetStockBalancesQuery.cs`, `GetItemLedgerQuery.cs` y
  `Application/Opening/RecordOpeningBalancesCommand.cs` (con validador).
- Crear: `Contracts/StockBalanceContracts.cs` (el DTO que también usará feature-0018).
- Modificar: `InventoryEndpoints.cs` (rutas `/balances`, `/items/{id}/ledger` y
  `/opening-balances`).
- Modificar: `UserRole.cs` y crear la migración de People: `InventoryOpeningManage`, sembrado
  para `admin`.
- Crear la migración de People que siembra los submódulos `inventory.receptions`,
  `inventory.usages` e `inventory.adjustments` (spec sec. 8.3).
- Crear: `tests/Hato.Modules.Inventory.IntegrationTests/Api/StockBalancesApiTests.cs`,
  `ItemLedgerApiTests.cs` y `OpeningBalancesApiTests.cs`.

**Verificación:** cada endpoint cubre 200/201, 400, 401/403 y 404, como exige
`docs/PROTOCOLO-DE-TRABAJO.md`.

## Commit 1.6 — Pantallas del panel

`feat(admin-web): add stock balances, ledger, opening count and category screens`

**Por qué acá:** es lo último del incremento y lo que el cliente usa el día del conteo.

**Archivos:**
- Crear bajo `clients/admin-web/src/app/components/`: `stock-balances/`, `item-ledger/`,
  `opening-count/` e `inventory-categories/`, cada uno con su `.spec.ts`.
- Modificar: `services/api.service.ts`, `app.routes.ts` y `app.component.html` (entrada
  "Inventario").
- Modificar: `components/inventory-batches-section/`, que deja de mostrar
  `Quantity` como saldo y muestra lo restante derivado.

**Verificación:** `npm run lint`, `npm test -- --watch=false` y `npm run build` en
`clients/admin-web`. Después, E2E-1 a E2E-4 de `test-e2e.md` en staging.

---

## Commit 2.1 — Recepciones con factura opcional

`feat(inventory): record multi-line receptions with an optional supplier invoice`

**Por qué acá:** es la entrada del día a día. Sin recepciones no se corrigen los negativos
que abren las alertas del 2.3.

**Archivos:**
- Crear: `Application/Receptions/RecordReceptionsCommand.cs` (varias líneas, `invoice`
  opcional).
- Modificar: `InventoryEndpoints.cs` (`POST /receptions`). `POST /items/{id}/receptions`
  delega en el nuevo comando con una sola línea.
- Crear: `tests/Hato.Modules.Inventory.IntegrationTests/Receptions/RecordReceptionsApiTests.cs`.

**Verificación:** las pruebas de ADR-0026 existentes (`RecordInventoryReceptionApiTests`)
siguen en verde sin cambios de contrato.

## Commit 2.2 — Salidas y ajustes

`feat(inventory): record usages and reasoned adjustments`

**Archivos:**
- Crear: `Application/Usages/RecordUsageCommand.cs` (el mismo que usará la sync de
  feature-0018) y `Application/Adjustments/RecordAdjustmentCommand.cs`.
- Modificar: `RecordGroupFeedConsumptionCommand.cs`, que delega en `RecordUsageCommand`.
- Modificar: `InventoryEndpoints.cs` (`POST /usages` y `POST /adjustments`).
- Modificar: `UserRole.cs` y crear la migración de People: `InventoryUsagesRecord` para
  `admin`, `registrar`, `veterinarian` **y todo rol que ya tenga
  `inventory.feed-consumptions.record`**; `InventoryAdjustmentsManage` solo para `admin`.
- Crear: `Api/UsagesApiTests.cs` y `Api/AdjustmentsApiTests.cs`.

## Commit 2.3 — Alertas con cierre automático

`feat(tasks): add stock alerts that resolve themselves`

**Archivos:**
- Modificar: `src/Modules/Tasks/Hato.Modules.Tasks.Domain/Alert.cs`: `Resolve(DateTimeOffset at)`,
  `IsResolved` y `ResolvedAt`. Crear la migración de Tasks.
- Crear: `src/Modules/Inventory/Hato.Modules.Inventory.Contracts/StockAlertsContracts.cs`
  (`IStockAlertsReader`) y su implementación en `Application/CrossModule/`.
- Modificar: `GenerateAlertsCommand.cs` (alertas de saldo negativo y stock bajo, y cierre
  automático de las tres de inventario).
- Crear: `tests/Hato.Modules.Tasks.UnitTests/Domain/AlertResolveTests.cs` y
  `tests/Hato.Modules.Tasks.IntegrationTests/Api/StockAlertsTests.cs`.

## Commit 2.4 — Pantallas de movimientos

`feat(admin-web): add reception, usage, adjustment and reason screens`

**Archivos:**
- Crear bajo `components/`: `stock-reception/`, `stock-usage/`, `stock-adjustment/` y
  `adjustment-reasons/`, cada uno con su `.spec.ts`.
- Modificar: `api.service.ts`, `app.routes.ts` y `app.component.html`.

**Verificación:** lint, pruebas y build del panel. Después, E2E-5 a E2E-9 en staging.

---

## Orden, dependencias y puntos de no retorno

```
1.1 calculador
  └─ 1.2 catálogos
       └─ 1.3 persistencia + backfill ── COMPUERTA: equivalencia
            └─ 1.4 flujos existentes ── PUNTO DE NO RETORNO en staging
                 └─ 1.5 API de lectura y saldo inicial
                      └─ 1.6 panel ── COMPUERTA: verificacion-migracion.sql en staging
                           └─ (promoción del incremento 1)
                                └─ 2.1 recepciones
                                     └─ 2.2 salidas y ajustes
                                          └─ 2.3 alertas
                                               └─ 2.4 panel
```

**Punto de no retorno: el despliegue de 1.4 en staging.** Desde ahí los consumos ya no
descuentan `Quantity`. Revertir el código no restaura los saldos viejos, que dejan de
actualizarse. La columna sigue existiendo, así que los datos anteriores no se pierden. Pero
volver atrás exige una migración que recalcule `Quantity` a partir de los movimientos, y por
eso la compuerta de 1.3 va antes.

## Descripción de los PR

**PR del incremento 1**

**Título:** `feat(inventory): stock ledger core, migration and opening count`

**Cuerpo:** qué (kardex derivado, catálogos, migración, saldos, kardex, conteo inicial); por
qué (spec sec. 1, ADR-0041); decisiones D1, D2, D3, D6 y D11; qué **no** incluye
(recepciones multilínea, salidas web, ajustes, alertas, teléfono y transformación); cómo
probar (`test-e2e.md` E2E-1 a E2E-4); riesgo (el punto de no retorno de 1.4 y cómo se
mitiga).

**PR del incremento 2**

**Título:** `feat(inventory): web receptions, usages, adjustments and stock alerts`

**Cuerpo:** qué, por qué, decisiones D3, D4, D8 y D9; no incluye el teléfono (feature-0018)
ni la transformación (feature-0019); cómo probar (E2E-5 a E2E-9).
