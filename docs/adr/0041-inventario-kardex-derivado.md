# ADR-0041 — Inventario como kardex derivado de movimientos inmutables

- **Estado:** Propuesto
- **Fecha:** 2026-09-23
- **Fase del roadmap:** Fase 4 adelantada, bloque 1 (ADR-0040)
- **Relacionado:** ADR-0004 (historial de eventos en JSONB), ADR-0007 (permisos en BD),
  ADR-0008 (protocolo de sincronización), ADR-0042 (submódulos, con los que se ocultan las
  partes de este inventario), ADR-0026 (recepción mínima pre-Purchasing, que este ADR **reemplaza** en su
  flujo de stock), Art. 1, Art. 6, Art. 8
- **Especificaciones:** `docs/spec/feature-0016-inventario-kardex/` (núcleo),
  `feature-0018-inventario-uso-en-campo/` (teléfono) y
  `feature-0019-preparacion-de-alimento/` (transformación)

## Contexto

El cliente del bloque 1 necesita llevar su bodega de alimento y fármacos en el sistema, y
que el saldo coincida con lo que cuenta en físico. El modelo actual no lo permite:

- **El saldo es una columna que se sobrescribe.** `InventoryBatch.Quantity`
  (`InventoryItem.cs:176`) guarda lo que queda, y `DeductQuantity` (`InventoryItem.cs:241`)
  la reduce en cada consumo. La cantidad original de cada lote se pierde. Un saldo que no
  cuadra no se puede explicar movimiento por movimiento.
- **La única salida es el consumo de alimento por grupo** (`RecordGroupFeedConsumptionCommand`).
  No hay salidas de fármacos, ajustes, merma ni transformación.
- **Una salida que excede el saldo se rechaza** (`RecordGroupFeedConsumptionCommand.cs`,
  "Stock insuficiente"). Pero la app registra sin señal (regla 10 de `AGENTS.md`): un uso
  real hecho en el galpón puede rebotar al sincronizar y dejar un hueco en la historia.
- **Las categorías son un enum** (`ItemCategory`: `Medicine`, `Feed`, `Supply`,
  `Product`). Registrar un tipo de bien nuevo, como semen o pajuelas, exige cambiar código
  (contra el Art. 8).
Lo acordado con el dueño y el cliente, que este ADR convierte en modelo, está en la
especificación, sec. 3.

## Decisión

**El stock se registra solo como movimientos inmutables. Saldo, costo promedio y valor se
calculan siempre recorriendo esos movimientos, y nunca se guardan.**

1. **`InventoryMovement` es la única fuente de verdad del stock.** Solo se agregan filas
   nuevas, nunca se editan ni se borran (Art. 1). Cada movimiento guarda lo que ocurrió:
   ítem, tipo, cantidad en unidad base, unidad y factor con que se registró, **fecha real**
   (`OccurredAt`, en UTC), fecha de registro, autor, e id generado en el cliente (para la
   idempotencia offline, ADR-0008). Los tipos:

   | Tipo | Código | Signo | Costo |
   |---|---|---|---|
   | Saldo inicial | `OpeningBalance` | + | Declarado, con marca `IsCostEstimated` |
   | Recepción | `Reception` | + | Declarado. Factura opcional |
   | Uso | `Usage` | − | Sale a promedio. Destino opcional (grupo o nota) |
   | Ajuste | `Adjustment` | ± | Sale a promedio. Motivo obligatorio |
   | Insumo de transformación | `TransformationInput` | − | Sale a promedio |
   | Producto de transformación | `TransformationOutput` | + | Derivado de insumos + servicio |

2. **Costo promedio ponderado, calculado al leer.** `StockLedgerCalculator` es un servicio
   de dominio puro, sin base de datos. Ordena los movimientos por (`OccurredAt`,
   fecha de registro, id) y devuelve, en cada punto, saldo, costo promedio y valor.
   - Solo las entradas (`OpeningBalance`, `Reception`, `TransformationOutput`) cambian el
     promedio. Las salidas salen al promedio vigente en su fecha.
   - Un movimiento offline que llega tarde con fecha pasada **se ubica solo en su lugar**.
     No hay que reescribir nada, porque no hay nada derivado guardado.
   - El costo de un `TransformationOutput` es (valor de sus insumos al promedio de su fecha
     + costo del servicio) ÷ cantidad producida. Por eso el cálculo cruza ítems en orden
     cronológico.

3. **Una salida que deja el saldo negativo se acepta y se marca.** La salida se valora al
   último promedio conocido. Si llega una entrada con el saldo en cero o negativo, el
   promedio pasa a ser el costo de esa entrada. El negativo genera una alerta crítica que
   se cierra sola cuando el saldo vuelve a ser positivo. El teléfono avisa, pero no bloquea.

4. **Documentos que agrupan movimientos:**
   - `SupplierInvoice` (factura recibida): número, fecha, proveedor en texto (el `Supplier`
     como entidad llega con Purchasing) y las recepciones que trajo. Una recepción sin
     factura puede llevar una nota del respaldo que tenga.
   - `TransformationOrder` (orden de transformación, término ya existente en el glosario):
     sus insumos, su producto y un **costo de servicio** como monto sin ítem de inventario.
     **La receta (`BillOfMaterials`) es opcional y no se implementa en este bloque:** cada
     orden se registra tal como ocurrió. En la interfaz se llama "Preparar alimento", pero
     el modelo sirve para cualquier transformación.

5. **Catálogos en base de datos (Art. 8):**
   - **`ItemCategory` pasa de enum a entidad editable.** Se siembran las cuatro categorías
     actuales con los mismos identificadores de texto. La regla "la etapa de alimento solo
     aplica a alimento" (`InventoryItem.cs:65`) pasa a ser una marca `IsFeed` en la
     categoría.
   - **`AdjustmentReason`**: se siembran merma, diferencia de pesaje, conteo físico,
     vencimiento, otro y migración.

6. **Los lotes se conservan como atributos de la recepción.** Número y vencimiento siguen
   existiendo. Lo que queda de cada lote se deriva por PEPS **solo para la alerta de
   vencimiento**, nunca para valorar.

7. **Migración sin pérdida.** Una migración nueva reconstruye una `Reception` por lote (lo
   que queda más lo consumido) y un `Usage` por cada `GroupFeedConsumption`. Luego verifica,
   por ítem, que el saldo derivado sea igual a la suma actual de `Quantity`. Si hay
   diferencia, crea un `Adjustment` con motivo "migración". `InventoryBatch.Quantity` deja
   de escribirse y de leerse, y **se elimina en una migración posterior**, cuando se haya
   verificado en staging.

## Alternativas consideradas

**Guardar en cada movimiento el saldo y el promedio del momento (kardex de papel).** Leer
es inmediato. Pero un movimiento offline con fecha pasada obliga a recalcular y reescribir
todas las filas posteriores. Eso choca con el Art. 1 y es justo donde se esconden los
errores de sincronización que ya sufrió el piloto anterior (`docs/ROADMAP.md`, Fase 3,
2026-09-07). Descartada.

**Movimientos como eventos en el historial JSONB (ADR-0004).** Sería coherente con
Livestock. Pero un kardex se consulta por ítem y por rango de fechas, y se suma: una tabla
tipada con índice por (ítem, fecha) es la forma natural de hacerlo. Pagar proyecciones y
deserialización en cada lectura no compra nada aquí. Descartada.

**Valoración PEPS por lote.** Es más precisa, y la app ya descuenta en ese orden. Pero es
más compleja de explicar y de calcular, y el cliente eligió promedio ponderado, que es el
método habitual para insumos en Ecuador. PEPS queda solo para saber qué lote vence.
Descartada como método de valoración.

**Rechazar las salidas que dejan saldo negativo, como hoy.** El saldo nunca sería
negativo, pero un uso real hecho sin señal podría rebotar al sincronizar. Sincronizar antes
de registrar tampoco lo evita: el saldo del teléfono siempre es una foto del pasado.
Descartada.

**Agregar "material genético" al enum de categorías.** Resuelve el semen hoy, pero el
próximo bien que no encaje vuelve a necesitar un desarrollador. Descartada por el Art. 8.

**Recetas (BOM) desde este bloque.** Las pidió el ROADMAP para la Fase 5. El cliente no las
pidió, y registrar cada preparación tal como ocurrió cubre su caso. Diferida, sin cerrar la
puerta: `TransformationOrder` puede referenciar una BOM en el futuro.

## Consecuencias

**Lo bueno.**
- Cada saldo se explica movimiento por movimiento.
- Los registros offline tardíos cuadran solos, y ningún uso real se pierde.
- Un bien o un motivo de ajuste nuevo es un dato, no código.
- La merma y el porcentaje de valor estimado quedan como cifras medibles.

**Lo malo.**
- Cada lectura de saldo recorre movimientos. A la escala de una finca (cientos a pocos
  miles de movimientos por ítem al año) son milisegundos. Si crece, se agrega un **cierre
  por período** que guarda saldos de apertura sellados, sin cambiar el modelo. Queda en
  `docs/BACKLOG.md`.
- El costo de una transformación depende de otros ítems, lo que hace el cálculo menos
  local.
- La migración reconstruye el detalle por lote de forma aproximada cuando un consumo PEPS
  se repartió entre lotes (guardó un solo `BatchId`). El total por ítem sí es exacto.

**Lo que hay que vigilar.**
- **Las pajuelas tienen su propio stock** (`SemenStraw.CurrentQuantity`, en Breeding).
  Encender Breeding junto con este inventario sin unirlos daría dos stocks del mismo semen.
  **Unirlos es condición para activar Breeding con inventario** (`docs/BACKLOG.md`).
- **Los tratamientos no descuentan fármacos** (`TreatmentCourse.ProductId` sigue siendo
  una referencia suelta). El cliente registra el uso sin vincularlo al animal. Vincularlos es
  trabajo futuro.
- La operación de sync `recordfeedconsumption` se sigue aceptando por compatibilidad con
  las apps ya instaladas. Se retira solo cuando ningún teléfono la use.

**Condición de reversa.** Se reabre si el tiempo de cálculo del saldo pasa de ~200 ms en
el panel con datos reales (primero se agrega el cierre por período). También si el cliente o
su contador necesitan una valoración distinta del promedio ponderado.
