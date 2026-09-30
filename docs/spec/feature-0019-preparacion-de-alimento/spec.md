# spec.md — Preparación de alimento (bloque 1, incremento 4)

> **Qué es este documento.** Fija cómo se registra en el kardex el alimento que la finca
> fabrica: insumos que salen, un costo de servicio opcional y un producto que entra con su
> costo. El modelo está en [ADR-0041](../../adr/0041-inventario-kardex-derivado.md),
> decisión 4. `plan.md`, `tasks.md` y `test-e2e.md` de esta carpeta dicen el orden, las
> casillas y la verificación.
>
> **Parte del bloque 1.** Las decisiones del bloque viven en
> [feature-0016](../feature-0016-inventario-kardex/spec.md) sec. 3. Esta spec implementa
> **D10** y siembra su submódulo (D12).

- **Rama Git:** `feature/inventory-transformations` (desde `develop`, después de mergear el
  incremento 2).
- **Fecha:** 2026-09-23.
- **Fase del ROADMAP:** Fase 4 adelantada, bloque 1. Adelanta una parte mínima de la Fase 5
  (Transformation), sin recetas.
- **ADRs que respalda o respeta:** ADR-0041, ADR-0042.
- **Reglas duras de `AGENTS.md`:** 1, **3 (nada específico de alimento en el dominio)**, 5,
  6 (dinero en `decimal`) y 8 (término `TransformationOrder`, ya en el glosario).

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Decisiones fijadas](#2-decisiones-fijadas)
3. [Alcance](#3-alcance)
4. [Diseño: modelo y costo](#4-diseño-modelo-y-costo)
5. [Diseño: API y panel](#5-diseño-api-y-panel)
6. [Riesgos y deuda](#6-riesgos-y-deuda)
7. [Criterios de aceptación](#7-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El cliente compra soya, maíz y un núcleo vitamínico, los mezcla o los hace pellets, a veces
pagando un servicio, y ensaca el resultado. Es el alimento que más usa, y **no entra por
compra**. Sin este flujo, el único atajo sería registrar la mezcla como recepción sin
factura, y eso cuenta el dinero dos veces: al comprar los insumos y al "recibir" la mezcla.

No hay hallazgos propios que verificar: hoy no existe nada de transformación en el código
(los términos `BillOfMaterials` y `TransformationOrder` solo están en `docs/GLOSSARY.md`). Los
hallazgos del inventario están en feature-0016, sec. 2.

## 2. Decisiones fijadas

Además de D10:

- **T1 — Sin recetas.** Cada orden se registra tal como ocurrió: qué insumos y cuánto, qué
  servicio y cuánto costó, cuánto producto salió.
- **T2 — Un solo producto por orden.** Varios productos o subproductos llegan con la BOM
  (Fase 5).
- **T3 — Genérico.** Nada en el dominio sabe que es alimento. La interfaz dice "Preparar
  alimento" porque es el caso del cliente. Cualquier ítem puede ser insumo o producto.
- **T4 — La merma del proceso no es un movimiento aparte.** Queda implícita en la diferencia
  entre los kg de insumos y los kg de producto. Se muestra como dato en la orden, no se
  registra.
- **T5 — El servicio cuenta como dinero invertido; los insumos no**, porque ya contaron al
  comprarse. Importa para el resumen del bloque 2.

## 3. Alcance

### Entra

- Tipos de movimiento `TransformationInput` y `TransformationOutput`.
- Entidad `TransformationOrder` con `OccurredAt`, `ServiceCost`, `ServiceDescription` y
  `Notes`.
- Regla 4 del `StockLedgerCalculator` (feature-0016, sec. 5.3).
- `POST /api/v1/inventory/transformations` y su consulta.
- Pantalla "Preparar alimento" en el panel.
- Permiso `inventory.transformations.manage` (solo `admin`) y submódulo
  `inventory.transformations`.

### No entra

- Recetas (BOM), subproductos y órdenes planificadas. Fase 5.
- Preparar desde el teléfono (D7).
- Anular una orden. Se corrige con ajustes, como cualquier otro error (Art. 1).

## 4. Diseño: modelo y costo

- **Una orden es atómica:** sus movimientos se crean en la misma transacción, o ninguno.
- **Invariantes:** al menos un insumo, exactamente un producto, cantidades mayores que
  cero, el producto no puede ser también insumo de la misma orden, y `ServiceCost` ≥ 0.
- **Costo del producto (regla 4 del calculador):**

```
costo_unitario = (Σ cantidad_insumo × promedio_insumo(OccurredAt) + ServiceCost) ÷ cantidad_producto
```

- El promedio de cada insumo es el vigente en la fecha de la orden, y se calcula igual que
  cualquier salida.
- **Dependencia entre ítems:** el kardex del producto necesita los kardex de sus insumos
  hasta `OccurredAt`. El servicio de consulta reúne esos movimientos y se los da al
  calculador, que sigue sin leer la base.
- **Insumo en negativo:** se permite (D3). Su valor sale al último promedio conocido, y la
  orden queda marcada "costo con insumo en negativo" hasta que ese saldo se corrija.
- **Insumo sin ninguna entrada previa:** no hay promedio. Se valora en 0 y la orden queda
  marcada "costo con insumo sin costo". Cuando llegue una entrada con fecha anterior, el
  costo se corrige solo.

## 5. Diseño: API y panel

- `POST /api/v1/inventory/transformations`:
  `{ occurredAt, inputs: [{itemId, quantity, unit}], output: {itemId, quantity, unit}, serviceCost, serviceDescription, notes }`.
  Responde con el id y el costo unitario calculado.
- `GET /api/v1/inventory/transformations?from&to`: órdenes con insumos, producto, costo y la
  merma implícita (T4).
- **Pantalla "Preparar alimento":** agregar insumos con su saldo actual a la vista, costo de
  servicio opcional, producto y cantidad. Antes de guardar muestra el costo unitario
  resultante.
- La entrada respeta `inventory.transformations` (feature-0017).

## 6. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| El costo depende de movimientos de otros ítems que llegan tarde | Se calcula al leer (ADR-0041): cuadra solo cuando llegan |
| Se duplica el dinero en el resumen del bloque 2 | T5 queda fijada aquí y la spec del bloque 2 la cita y la prueba |
| Aparece la necesidad de recetas | Fase 5. `TransformationOrder` puede referenciar una BOM sin cambiar lo existente |

## 7. Criterios de aceptación

1. Una orden con 300 kg de maíz a $0,40 de promedio, 100 kg de soya a $0,60 y 10 kg de
   núcleo a $2,00, más $20 de servicio, que produce 400 kg, da un costo unitario de
   **$0,5500 por kg**: (120 + 60 + 20 + 20) ÷ 400 = 220 ÷ 400. La merma implícita que muestra
   es de 10 kg (410 kg de insumos − 400 kg de producto).
2. Una orden sin insumos, o con el producto repetido como insumo, se rechaza con Problem
   Details.
3. Si falla la escritura de un movimiento, no queda ninguno de la orden.
4. Con `inventory.transformations` apagado, "Preparar alimento" no aparece en el panel, y el
   endpoint sigue aceptando.
5. Un usuario `registrar` recibe 403 en `POST /transformations`.
6. Ningún archivo del dominio de inventario contiene la palabra "feed" en la lógica de
   transformación (`grep`).
