# spec.md — Inventario como kardex: núcleo (Fase 4 adelantada, bloque 1)

> **Qué es este documento.** Fija qué se construye en el bloque 1 del cliente nuevo, por qué,
> y qué decisiones quedan cerradas. El modelo se justifica en
> [ADR-0041](../../adr/0041-inventario-kardex-derivado.md). En qué orden y en qué commits va
> cada cosa, el desglose ejecutable y la verificación manual van en `plan.md`, `tasks.md` y
> `test-e2e.md` de esta carpeta, que se escriben después de aprobar esta spec.
>
> **El bloque 1 son cuatro specs, partidas por propósito.** Esta es el núcleo y es la
> **única dueña de las decisiones D1–D15** (sec. 3). Las otras tres las citan por número y
> no las repiten:
>
> | Spec | Propósito |
> |---|---|
> | **feature-0016** (esta) | Modelo, cálculo, migración, catálogos, saldos, kardex, saldo inicial, recepciones, salida y ajuste web, alertas |
> | [feature-0017](../feature-0017-modulos-y-submodulos/spec.md) | Submódulos (ADR-0042), sección "Módulos" y menú web filtrado |
> | [feature-0018](../feature-0018-inventario-uso-en-campo/spec.md) | Uso de inventario desde el teléfono: sync, saldos y pantalla |
> | [feature-0019](../feature-0019-preparacion-de-alimento/spec.md) | Preparación de alimento (orden de transformación) |

- **Ramas Git:** documentos en `docs/adr-0040-reorientacion-cliente-piloto` (desde
  `develop`, en `5276e14`), junto con ADR-0040, ADR-0041 y ADR-0042. Implementación en dos
  ramas: `feature/inventory-stock-ledger` (incremento 1) y
  `feature/inventory-web-movements` (incremento 2); ver `plan.md`.
- **Fecha:** 2026-09-23.
- **Fase del ROADMAP:** Fase 4, adelantada por ADR-0040. Bloque 1.
- **ADRs que respalda o respeta:** ADR-0041 (modelo), ADR-0040 (recorte), ADR-0042
  (submódulos, que esta spec siembra para `inventory`), ADR-0026 (recepción mínima, cuyo flujo de stock
  se reemplaza), ADR-0008 (sincronización), ADR-0007 (permisos).
- **Reglas duras de `AGENTS.md` que gobiernan este trabajo:** 1 (no editar historia), 3
  (nada de `if` por producto), 5 (pruebas contra PostgreSQL real), 6 (dinero en `decimal`,
  cantidades con unidad, UTC), 7 (migraciones nuevas), 8 (glosario), 9 (un propósito por
  PR).

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: modelo y cálculo del kardex](#5-diseño-modelo-y-cálculo-del-kardex)
6. [Diseño: migración de lo existente](#6-diseño-migración-de-lo-existente)
7. [Diseño: API y panel web](#7-diseño-api-y-panel-web)
8. [Diseño: permisos, alertas y submódulos de inventario](#8-diseño-permisos-alertas-y-submódulos-de-inventario)
9. [Entrega por incrementos](#9-entrega-por-incrementos)
10. [Riesgos y deuda](#10-riesgos-y-deuda)
11. [Criterios de aceptación](#11-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El cliente del bloque 1 tiene una granja porcina. Compra alimento y fármacos, fabrica parte
de su alimento (mezcla soya, maíz y un núcleo vitamínico, a veces pagando un servicio de
peletizado) y no lleva hoy un registro formal de su bodega. Quiere saber qué tiene, qué
usó, cuánto le costó y, en el bloque siguiente, cuánto vendió.

Además puso condiciones que pesan tanto como las funciones:
- La operación es delicada y **no tolera fallos**.
- Cualquier fallo en un bloque ya iniciado se atiende. Lo no contemplado que surja después
  se planifica y no es un hotfix.
- **No revisa pruebas**: acepta usando.
- Quiere **desarrollo continuo**.

El dueño del proyecto fijó el recorte con el cliente el 2026-09-23 (ADR-0040, decisión 3).
Lo expresado por el cliente y cada decisión que salió de ahí están registrados, con fecha, en
el anexo de levantamiento de la tesis (privado, fuera del repositorio).

## 2. Hallazgos verificados

Comprobados leyendo `develop` en `5276e14`.

### 2.1 El saldo es una columna que se sobrescribe

`InventoryBatch.Quantity` (`src/Modules/Inventory/Hato.Modules.Inventory.Domain/InventoryItem.cs:176`)
guarda lo que queda, y `DeductQuantity` (`:241`) lo reduce. No se guarda la cantidad
original del lote en ninguna parte.

### 2.2 La única salida es el consumo de alimento por grupo, y rechaza lo que excede el saldo

`DeductQuantity` solo lo llama `RecordGroupFeedConsumptionCommand`
(`src/Modules/Inventory/Hato.Modules.Inventory.Application/Consumptions/RecordGroupFeedConsumptionCommand.cs`).
Cuando el consumo excede el saldo, lanza "Stock insuficiente" y la operación se rechaza,
aunque haya llegado por sync (`src/Hato.Api/Sync/PushSyncCommands.cs:303`, operación
`recordfeedconsumption`).

### 2.3 Un consumo PEPS repartido entre lotes guarda un solo lote

`ResolveConsumptionBatchId` (`RecordGroupFeedConsumptionCommand.cs:184-192`) devuelve un
único `BatchId`, aunque el descuento haya recorrido varios lotes. El detalle por lote no se
puede reconstruir exacto; el total por ítem sí.

### 2.4 Los tratamientos no descuentan fármacos

`TreatmentCourse.ProductId`
(`src/Modules/Livestock/Hato.Modules.Livestock.Domain/TreatmentCourse.cs:40-45`) es una
referencia sin restricción, y su comentario declara el contrato estricto como trabajo de la
Fase 4.

### 2.5 Las categorías son un enum

`ItemCategory` (`src/Modules/Inventory/Hato.Modules.Inventory.Domain/ItemCategory.cs`)
tiene `Medicine`, `Feed`, `Supply` y `Product`. La regla de la etapa de alimento depende de
él (`InventoryItem.cs:65`).

### 2.6 `MinStock` se guarda, pero ninguna alerta lo lee

Aparece en el comando de creación y en las consultas, pero no en
`src/Modules/Tasks/Hato.Modules.Tasks.Application/Alerts/GenerateAlertsCommand.cs`, que
solo genera alertas de vencimiento de lotes (`:113-134`) además de las de Livestock y
Breeding.

### 2.7 Las pajuelas tienen su propio stock

`SemenStraw.CurrentQuantity`
(`src/Modules/Breeding/Hato.Modules.Breeding.Domain/SemenStraw.cs:13`) se descuenta con
`UseStraw()` (`:51`), fuera del inventario.

### 2.8 Los interruptores de módulo: el teléfono los respeta, el panel no

- Siembra: `production` (apagado), `livestock`, `inventory`, `breeding`, `tasks` y
  `people`
  (`src/Modules/People/Hato.Modules.People.Infrastructure/Persistence/Migrations/20260807031453_AddFarmModulesAndSettingsPermissions.cs:74-86`).
- El teléfono evalúa la visibilidad localmente (`clients/field-app/src/services/moduleVisibility.ts`).
- En el panel web, los interruptores solo aparecen como una pestaña de Catálogos
  (`clients/admin-web/src/app/components/catalogs/catalogs.component.ts:125`), y ningún
  otro componente los lee: **el menú del panel no oculta los módulos apagados.**

### 2.9 Roles sembrados

`admin`, `registrar` y `veterinarian`
(`src/Modules/People/Hato.Modules.People.Infrastructure/Persistence/Migrations/20260802162528_AddRbacPermissions.cs:187-192`).

## 3. Decisiones fijadas

Todas se consultaron con el dueño el 2026-09-23, y las que afectan al negocio también con el
cliente. Son las decisiones de **todo el bloque 1**. Entre corchetes, la spec que implementa
cada una cuando no es esta.

- **D1 — El stock es un kardex derivado.** Solo se guardan movimientos inmutables. Saldo,
  promedio y valor se calculan siempre al leer (ADR-0041, decisiones 1 y 2).
- **D2 — Valoración a costo promedio ponderado.** *(Aprobado por el cliente.)*
- **D3 — Una salida que deja el saldo negativo se acepta y se marca.** El teléfono muestra
  el saldo con la fecha de la última sincronización y avisa, pero no bloquea. Se descartó
  "sincronizar antes de registrar" porque viola la regla 10 y no garantiza nada: el saldo
  del teléfono siempre es una foto del pasado. [La aceptación en el servidor, aquí; el aviso
  en el teléfono, feature-0018.]
- **D4 — Recepciones con o sin factura.** La factura es un respaldo opcional.
- **D5 — Salidas de fármacos sin vínculo al animal.** "Se usó X el día Y", con destino
  opcional. *(Pedido del cliente.)*
- **D6 — Inventario inicial por conteo físico.** Se registra con movimientos
  `OpeningBalance` a precio actual, marcados como estimados. **No cuentan como dinero
  invertido.** Se hace desde la interfaz, sin desarrollador. *(Aprobado por el cliente, que
  pidió una guía para hacerlo solo.)*
- **D7 — Canales.** El teléfono registra salidas y consulta saldos. El panel web hace todo
  lo demás, y el administrador también registra salidas desde la web. *(Aprobado por el
  cliente.)* [Teléfono: feature-0018.]
- **D8 — Ajuste solo para administrador, con motivo obligatorio** tomado de un catálogo.
  Cubre merma, diferencia de pesaje y conteo. *(Pedido del cliente.)*
- **D9 — El kardex cuenta en unidad base (kg o lb).** El costal es una conversión nominal.
  La diferencia real de peso se corrige con un ajuste "diferencia de pesaje".
- **D10 — Preparación de alimento mínima y sin recetas**, modelada como
  `TransformationOrder` genérica. *(Aprobado por el cliente.)* [feature-0019.]
- **D11 — Categorías como catálogo editable.** *(Aprobado por el cliente, a raíz del semen y
  las pajuelas.)*
- **D12 — Submódulos jerárquicos, creados solo para inventario.** [Mecanismo: feature-0017 y
  ADR-0042. Siembra de los de inventario: aquí y en feature-0019.]
- **D13 — Sección "Módulos" propia en el panel, y un menú web que oculta lo apagado.**
  *(Pedido del cliente.)* [feature-0017.]
- **D14 — A producción solo llega lo terminado.** Lo incompleto se frena en la promoción
  `develop` → `main`, no con los interruptores, porque estos son del cliente.
- **D15 — Ventas y resumen van en el bloque 2**, no en este.

## 4. Alcance

### Entra

- Modelo de movimientos (`OpeningBalance`, `Reception`, `Usage`, `Adjustment`), cálculo del
  kardex, factura recibida y catálogos de categorías y de motivos de ajuste.
- Migración de lotes y consumos existentes, sin pérdida.
- Endpoints REST por acción y consultas de saldos y kardex.
- Pantallas del panel: Saldos, Kardex, Conteo inicial, Nueva recepción, Salida, Ajuste,
  Categorías y Motivos de ajuste.
- Siembra de los submódulos `inventory.receptions`, `inventory.usages` e
  `inventory.adjustments`, y que las pantallas nuevas los respeten.
- Permisos sembrados por rol. Alertas de saldo negativo, stock bajo y vencimiento derivado,
  con cierre automático.
- Que `POST /feed-consumptions` y la operación de sync `recordfeedconsumption` sigan
  funcionando, ahora como `Usage` (el comando que las atiende cambia en esta spec).
- Entradas nuevas en `GLOSSARY.md`.

### No entra

- **Mecanismo de submódulos, sección "Módulos" y menú web filtrado.** Es feature-0017. Esta
  spec solo siembra filas de submódulo, que el mecanismo actual ignora sin fallar hasta que
  llegue feature-0017.
- **Operación de sync nueva, saldos en el teléfono y pantalla "Registrar uso".** Es
  feature-0018.
- **Preparación de alimento** (`TransformationInput`, `TransformationOutput`,
  `TransformationOrder`). Es feature-0019.
- **Bodegas o ubicaciones.** Diferidas por decisión del dueño. El modelo no las impide: se
  agregan como atributo del movimiento.
- **Vincular fármacos a animales o tratamientos.** El cliente no lo pidió ahora (D5).
- **Ventas y resumen.** Bloque 2 (D15).
- **Activos, pagos, cuentas por pagar y comprobantes.** Fuera del recorte (ADR-0040).
- **Unir `SemenStraw` al kardex.** Queda como condición para activar Breeding junto con
  inventario (sec. 10).
- **Eliminar `InventoryBatch.Quantity`.** Va en una migración posterior (sec. 6).
- **Poner en marcha producción.** Es una dependencia, no parte de este trabajo (sec. 9).

## 5. Diseño: modelo y cálculo del kardex

### 5.1 Movimiento

`InventoryMovement`: `Id` (UUID, lo genera el cliente cuando viene del teléfono), `ItemId`,
`Kind`, `QuantityInBaseUnit` (siempre positiva; el signo lo da `Kind`, salvo en
`Adjustment`, que lleva `Direction`), `UnitRecorded`, `AppliedFactor`, `OccurredAt` (UTC),
`RecordedAt`, `RecordedById`, `RecordedByLabel` y `Notes`. Según el tipo, también:
- `UnitCost` e `IsCostEstimated`: solo en `OpeningBalance` y `Reception`.
- `SupplierInvoiceId`, `BatchNumber` y `ExpirationDate`: solo en `Reception`.
- `GroupId`: opcional en `Usage`.
- `AdjustmentReasonId`: obligatorio en `Adjustment`.

`Kind` es un enum de comportamiento del dominio, no de producto: cada tipo tiene reglas
distintas en el cálculo, así que no es configuración (Art. 8 no aplica). feature-0019 le
agrega `TransformationInput` y `TransformationOutput`.

Las invariantes viven en el dominio: cantidad mayor que cero, costo no negativo, motivo en
todo ajuste, costo en toda entrada declarada, y ninguna fecha real en el futuro.

### 5.2 Documentos y catálogos

- `SupplierInvoice`: `Number`, `IssuedAt`, `SupplierLabel`, `Notes` y sus recepciones.
- `ItemCategory` (entidad): `Code`, `Name`, `IsFeed` e `IsActive`. Se siembran `medicine`,
  `feed`, `supply` y `product`.
- `AdjustmentReason`: `Code`, `Name` e `IsActive`. Se siembran `shrinkage` (merma),
  `weighing_difference`, `physical_count`, `expiry`, `other` y `migration`. El último no
  se puede elegir desde la interfaz.

### 5.3 `StockLedgerCalculator`

Es una función pura: recibe los movimientos de uno o más ítems y devuelve las líneas del
kardex. Cada línea lleva el movimiento, el saldo, el costo promedio, el valor, `IsNegative`
y la parte del valor que es estimada.

Orden: (`OccurredAt`, `RecordedAt`, `Id`). Reglas, cada una con su prueba unitaria:
1. Una entrada con saldo previo positivo da promedio = (valor previo + cantidad × costo) ÷
   (saldo previo + cantidad).
2. Una entrada con saldo previo en cero o negativo da promedio = costo de la entrada.
3. Toda salida se valora al promedio vigente, o al último conocido si el saldo es negativo.
4. *(Reservada para feature-0019: costo del producto de una transformación.)*
5. **Valor estimado:** la parte del valor que viene de `OpeningBalance` con
   `IsCostEstimated` se reparte proporcionalmente en cada salida, igual que el promedio.
6. Todo el dinero en `decimal`. El redondeo se hace **solo al presentar**, a 2 decimales
   para dinero y 4 para costos unitarios. En los cálculos intermedios no se redondea.

El calculador recibe la lista de movimientos ya ordenada o la ordena él; nunca lee la base.
Así feature-0019 puede darle movimientos de varios ítems sin cambiar su contrato.

### 5.4 Lotes para vencimiento

Las recepciones con `ExpirationDate` forman los lotes. Lo que queda de cada lote se deriva
por PEPS sobre las salidas del ítem. Solo lo usa la alerta de vencimiento (sec. 8.2).

## 6. Diseño: migración de lo existente

Producción arranca vacía (ADR-0040). **Staging tiene los datos del piloto anterior**, que no
se borran, y es donde la migración se ensaya de verdad.

1. Por cada `InventoryBatch` se crea una `Reception` con fecha `ReceivedAt`, cantidad =
   `Quantity` actual + consumos cuyo `BatchId` es ese lote, y su costo, proveedor, factura
   en texto, lote y vencimiento.
2. Por cada `GroupFeedConsumption` se crea un `Usage` con `GroupId`, fecha `ConsumedAt`,
   el mismo autor y las mismas cantidades.
3. Por cada ítem: si el saldo derivado difiere de Σ `Quantity`, se crea un `Adjustment` con
   motivo `migration` por la diferencia. El total queda exacto (hallazgo 2.3).
4. El enum `ItemCategory` pasa a ser FK hacia el catálogo, con los mismos códigos.
5. `InventoryBatch.Quantity` deja de escribirse y leerse, pero **no se elimina aquí.**

Verificación:
- Una prueba de integración contra PostgreSQL carga el estado actual (incluido un consumo
  PEPS repartido entre dos lotes), migra y afirma que el saldo por ítem es idéntico.
- La misma verificación se guarda como `verificacion-migracion.sql` en esta carpeta, para
  correrla en staging después del despliegue.

## 7. Diseño: API y panel web

### 7.1 REST (`/api/v1/inventory`)

| Método y ruta | Acción | Permiso |
|---|---|---|
| `POST /opening-balances` | Hoja de conteo: varias líneas (ítem, cantidad, unidad, costo, estimado) | `inventory.opening.manage` |
| `POST /receptions` | Recepción de varias líneas, con `invoice` opcional | `inventory.receptions.manage` |
| `POST /usages` | Salida | `inventory.usages.record` |
| `POST /adjustments` | Ajuste con motivo | `inventory.adjustments.manage` |
| `GET /balances` | Por ítem: saldo, promedio, valor, negativo y % estimado | `inventory.items.read` |
| `GET /items/{id}/ledger?from&to` | Líneas del kardex | `inventory.items.read` |
| `GET/POST/PATCH /categories`, `/adjustment-reasons` | Catálogos | `inventory.catalogs.manage` para escribir |

- Los errores van en Problem Details (RFC 7807).
- **`POST /usages` nunca rechaza por saldo** (D3). El comando que lo atiende es el mismo que
  usarán la sync (feature-0018) y `POST /feed-consumptions`.
- `POST /items/{id}/receptions` (ADR-0026) sigue funcionando: crea una recepción de una
  línea, sin factura.
- `POST /items/{id}/batches` (ya marcado `Obsolete`) se redirige al mismo camino.
- `POST /feed-consumptions` y la operación de sync `recordfeedconsumption` crean un `Usage`
  con `GroupId`, sin cambiar su contrato.

### 7.2 Panel web

- Pantallas: Saldos (con marca de negativo y % estimado), Kardex por ítem, Conteo inicial
  (hoja con todos los ítems activos), Nueva recepción (factura opcional, varias líneas),
  Salida, Ajuste, Categorías y Motivos de ajuste.
- Cada pantalla de acción respeta su submódulo (sec. 8.3). Hasta que llegue feature-0017 el
  panel no filtra el menú, así que el submódulo se respeta solo cuando exista el filtro.

## 8. Diseño: permisos, alertas y submódulos de inventario

### 8.1 Permisos sembrados

| Permiso | admin | registrar | veterinarian |
|---|---|---|---|
| `inventory.items.read` | ✅ | ✅ | ✅ |
| `inventory.usages.record` | ✅ | ✅ | ✅ |
| `inventory.opening.manage`, `inventory.receptions.manage` | ✅ | — | — |
| `inventory.adjustments.manage` | ✅ | — | — |
| `inventory.catalogs.manage` | ✅ | — | — |

Es solo la siembra: el administrador puede cambiarla desde el panel (ADR-0007).
feature-0019 agrega `inventory.transformations.manage`.

### 8.2 Alertas

Las genera Tasks, leyendo por contratos de `Hato.Modules.Inventory.Contracts` (Art. 6).

| Código | Gravedad | Condición | Cierre |
|---|---|---|---|
| `INVENTORY_NEGATIVE_BALANCE` | Crítica | Saldo < 0 | Automático cuando saldo ≥ 0 |
| `INVENTORY_LOW_STOCK` | Advertencia | 0 ≤ saldo < `MinStock` | Automático cuando saldo ≥ `MinStock` |
| `INVENTORY_BATCH_EXPIRING` (existe) | Advertencia / crítica | Vence en ≤ 30 días **y le queda algo** (sec. 5.4) | Automático cuando se agota |

**Cierre automático:** es nuevo en `Alert`. El generador marca como resueltas las alertas
cuya condición ya no se cumple, registrando que fue el sistema y cuándo. El descarte manual
sigue existiendo. Una alerta de inventario solo se genera si `inventory` está encendido.

### 8.3 Submódulos de inventario

Se siembran `inventory.receptions`, `inventory.usages` e `inventory.adjustments`, todos
encendidos. Saldos, Kardex, Conteo inicial y Categorías dependen solo de `inventory`. El
mecanismo que los evalúa es de feature-0017 (ADR-0042).

## 9. Entrega por incrementos

Cada incremento es una rama, un PR y una promoción a `main` **cuando está completo y
verificado con su `test-e2e.md` en staging** (D14). Mapa del bloque 1 entero:

| # | Spec | Contenido | Qué obtiene el cliente |
|---|---|---|---|
| 0 | feature-0017 | Submódulos, sección "Módulos" y menú filtrado | Oculta lo que no usa |
| 1 | **feature-0016** | Modelo, cálculo, migración, categorías, Saldos, Kardex, Conteo inicial | Carga su bodega el día del conteo y la ve valorada |
| 2 | **feature-0016** | Recepciones con y sin factura, salida web, ajustes, motivos y alertas | Lleva la bodega desde la oficina, y el sistema le avisa |
| 3 | feature-0018 | Uso desde el teléfono | El personal registra desde el galpón |
| 4 | feature-0019 | Preparar alimento | Su alimento fabricado entra con su costo |

El incremento 0 no depende de ningún otro y puede salir primero. Las alertas pasan al
incremento 2 porque sin recepciones ni ajustes no hay forma de cerrar un saldo negativo.

**Dependencias que bloquean el incremento 1 en producción**, fuera de este trabajo:
- Panel web desplegado en producción (hoy solo está la API, `docs/PRODUCCION.md` sec. 5.3).
- Restauración de backup probada (feature-0013, D6).

El `test-e2e.md` de cada incremento cumple tres funciones: es la verificación antes de
promover, **el guion de aceptación con el cliente** y **el borrador del manual de esa
acción**, para el documento de entrega.

## 10. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| El cálculo al leer se vuelve lento con los años | Umbral de reversa en ADR-0041 (~200 ms). Cierre por período anotado en `docs/BACKLOG.md` |
| La migración no cuadra en staging | Ajuste `migration` visible, prueba de equivalencia y `verificacion-migracion.sql`. La columna vieja sigue ahí hasta verificar |
| Cambiar el comando de consumo rompe a los teléfonos instalados | Contrato de `recordfeedconsumption` intacto, con prueba de integración usando el payload actual |
| Se enciende Breeding y hay dos stocks de semen | Condición anotada en `docs/BACKLOG.md` y ADR-0041 |
| El cliente enciende algo incompleto | A producción no llega nada incompleto (D14) |
| Un saldo negativo pasa desapercibido | Alerta crítica, marca en Saldos y cierre solo cuando se corrige |
| Redondeos que descuadran el valor | `decimal` sin redondeo intermedio y prueba de valor total = Σ líneas |
| Sale a producción sin backups probados | Dependencia explícita (sec. 9) |

Deuda que este trabajo deja anotada en `docs/BACKLOG.md`: eliminar
`InventoryBatch.Quantity`, cierre por período, unir `SemenStraw` al kardex, vincular fármacos
con tratamientos y bodegas.

## 11. Criterios de aceptación

1. `InventoryBatch.Quantity` no se escribe en ningún camino de código (`grep` sobre
   `DeductQuantity` y sobre asignaciones a `Quantity` sin resultados fuera de la migración).
2. Las pruebas unitarias de `StockLedgerCalculator` cubren las reglas 1, 2, 3, 5 y 6 de la
   sec. 5.3, la salida con fecha pasada registrada después y el saldo negativo.
3. La prueba de integración de la migración afirma que el saldo por ítem es igual antes y
   después, incluido un consumo PEPS repartido entre lotes.
4. Un `POST /usages` que deja el saldo negativo se acepta, y `INVENTORY_NEGATIVE_BALANCE`
   aparece. Tras una recepción que lo corrige, la alerta queda resuelta sin intervención.
5. Un `recordfeedconsumption` con el payload de la app instalada hoy se acepta y produce un
   `Usage`.
6. Un usuario `registrar` recibe 403 en `POST /adjustments`.
7. Crear la categoría "Material genético" desde el panel y registrar una recepción de
   pajuelas en ella no requiere ningún cambio de código.
8. Una hoja de conteo inicial cargada desde el panel produce `OpeningBalance` marcados como
   estimados, y Saldos muestra el % de valor estimado.
9. Cada incremento tiene su `test-e2e.md` ejecutado en staging antes de promoverse.
10. `GLOSSARY.md` contiene cada término nuevo de esta spec.
