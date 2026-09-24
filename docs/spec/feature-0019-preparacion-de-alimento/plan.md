# plan.md — Ejecución de la rama `feature/inventory-transformations`

> **Qué es este documento.** Dice en qué orden y en qué commits se construye lo que
> [`spec.md`](./spec.md) decidió. Las casillas están en [`tasks.md`](./tasks.md) y la
> verificación en [`test-e2e.md`](./test-e2e.md).

**Objetivo:** que el alimento que fabrica la finca entre al kardex con su costo real (sus
insumos al promedio más el servicio), sin contar el dinero dos veces.

**Enfoque:** una rama y **tres commits**. Empieza cuando el incremento 2 (feature-0016) está
en `develop`. Es independiente de feature-0018.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

- **Nada en el dominio sabe que es alimento** (T3, regla 3). "Preparar alimento" es solo el
  texto de la interfaz.
- **Una orden es atómica**: se crean todos sus movimientos o ninguno (spec sec. 4).
- **El costo del producto se calcula al leer**, con la regla 4 del calculador. No se guarda.
- **Dinero en `decimal`, sin redondeo intermedio** (feature-0016, regla 6).
- **Un insumo puede quedar en negativo** (D3). La orden se marca, no se rechaza.
- **Sin recetas, un solo producto por orden** (T1, T2).

## Foco de revisión

1. **Un insumo sin ninguna entrada previa** (promedio indefinido). Su valor es 0, la orden
   queda marcada "costo con insumo sin costo", y el costo del producto no se vuelve `NaN` ni
   lanza → T1.4.
2. **Una recepción de un insumo registrada después, con fecha anterior a la orden.** El costo
   del producto cambia solo, sin reescribir nada → T1.3.
3. **Unidades distintas entre insumos y producto** (sacos de soya, kg de mezcla). Todo se
   convierte a unidad base antes de calcular → T2.2.
4. **`ServiceCost` = 0.** Es válido: la orden cuesta solo sus insumos → T1.2.
5. **Una orden con el mismo insumo en dos líneas.** Se rechaza, igual que el producto
   repetido como insumo, para que el cálculo no dependa de sumar líneas en silencio → T2.1.

---

## Índice

1. [Commit 1 — Tipos de movimiento y regla 4 del calculador](#commit-1--tipos-de-movimiento-y-regla-4-del-calculador)
2. [Commit 2 — Orden de transformación, persistencia y API](#commit-2--orden-de-transformación-persistencia-y-api)
3. [Commit 3 — Pantalla "Preparar alimento"](#commit-3--pantalla-preparar-alimento)
4. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
5. [Descripción del PR](#descripción-del-pr)

---

## Commit 1 — Tipos de movimiento y regla 4 del calculador

`feat(inventory): value transformation outputs from their inputs and service cost`

**Archivos:**
- Modificar: `Domain/Movements/MovementKind.cs` (`TransformationInput`,
  `TransformationOutput`) e `InventoryMovement.cs` (fábricas y `TransformationOrderId`).
- Modificar: `Domain/Ledger/StockLedgerCalculator.cs` (regla 4, entrada con varios ítems).
- Crear: `tests/Hato.Modules.Inventory.UnitTests/Ledger/TransformationCostTests.cs`.

## Commit 2 — Orden de transformación, persistencia y API

`feat(inventory): record transformation orders`

**Archivos:**
- Crear: `Domain/Transformations/TransformationOrder.cs`,
  `Application/Transformations/RecordTransformationCommand.cs` (con validador) y
  `GetTransformationsQuery.cs`.
- Crear: la configuración EF y la migración
  `dotnet ef migrations add AddTransformationOrders --project src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure --startup-project src/Hato.Api`.
- Modificar: `InventoryEndpoints.cs` (`POST` y `GET /transformations`).
- Modificar: `UserRole.cs` y crear la migración de People: `InventoryTransformationsManage`
  para `admin` y el submódulo `inventory.transformations`, encendido.
- Crear: `tests/Hato.Modules.Inventory.IntegrationTests/Api/TransformationsApiTests.cs`.

## Commit 3 — Pantalla "Preparar alimento"

`feat(admin-web): add the prepare feed screen`

**Archivos:**
- Crear: `clients/admin-web/src/app/components/prepare-feed/` con su `.spec.ts`.
- Modificar: `api.service.ts`, `app.routes.ts` y `app.component.html` (entrada con la clave
  `inventory.transformations`).

**Verificación:** lint, pruebas y build del panel, y [`test-e2e.md`](./test-e2e.md) en
staging.

## Orden, dependencias y puntos de no retorno

```
(feature-0016 incremento 2 en develop)
  └─ 1 calculador
       └─ 2 orden + API
            └─ 3 panel
```

No hay punto de no retorno: la migración solo agrega una tabla y dos valores de enum que
ningún dato existente usa.

## Descripción del PR

**Título:** `feat(inventory): record feed preparation as transformation orders`

**Cuerpo:** qué (orden de transformación, costo del producto, pantalla); por qué (spec
sec. 1); decisiones D10 y T1–T5; qué **no** incluye (recetas, subproductos, teléfono); cómo
probar (`test-e2e.md`); riesgo (el costo depende de otros ítems, cubierto por el cálculo al
leer).
