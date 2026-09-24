# spec.md — Uso de inventario desde el teléfono (bloque 1, incremento 3)

> **Qué es este documento.** Fija cómo el personal de campo registra el uso de alimento y
> fármacos desde el teléfono, sin señal, y cómo ve el saldo. El modelo está en
> [ADR-0041](../../adr/0041-inventario-kardex-derivado.md). `plan.md`, `tasks.md` y
> `test-e2e.md` de esta carpeta dicen el orden, las casillas y la verificación en el
> dispositivo.
>
> **Parte del bloque 1.** Las decisiones del bloque viven en
> [feature-0016](../feature-0016-inventario-kardex/spec.md) sec. 3. Esta spec implementa la
> parte de campo de **D3** (aviso sin bloqueo), **D5**, **D7** y **D9**.

- **Rama Git:** `feature/inventory-field-usage` (desde `develop`, después de mergear el
  incremento 2).
- **Fecha:** 2026-09-23.
- **Fase del ROADMAP:** Fase 4 adelantada, bloque 1.
- **ADRs que respalda o respeta:** ADR-0041, ADR-0008 (protocolo de sincronización),
  ADR-0009 (stack móvil), ADR-0042 (submódulo `inventory.usages`).
- **Reglas duras de `AGENTS.md`:** 5, 6 (cantidades con unidad) y **10 (ninguna operación de
  registro depende de red)**.

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: sincronización](#5-diseño-sincronización)
6. [Diseño: teléfono](#6-diseño-teléfono)
7. [Riesgos y deuda](#7-riesgos-y-deuda)
8. [Criterios de aceptación](#8-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El alimento y los fármacos se usan en el galpón, donde no siempre hay señal. Si el uso se
registra después en la oficina, se olvida o se anota mal, y el saldo deja de coincidir con
la bodega. El cliente quiere que su personal lo registre en el momento, desde el teléfono.

## 2. Hallazgos verificados

Leyendo `develop` en `5276e14`:

1. **El teléfono ya envía consumos de alimento por lote** con la operación
   `recordfeedconsumption` (`src/Hato.Api/Sync/PushSyncCommands.cs:303-324`), encolada por
   `FeedConsumptionService` (`clients/field-app/src/services/feedConsumptionService.ts:39-56`).
   Hay teléfonos instalados que la usan.
2. **El pull ya trae `inventoryItems`** con el permiso `inventory.items.read`
   (`src/Hato.Api/Sync/SyncPullQueries.cs:346`, `:431`), pero ningún saldo.
3. **La pantalla de consumo por lote existe**
   (`clients/field-app/src/screens/LotEventsScreen.tsx`), y solo registra alimento hacia un
   grupo.
4. **Después de feature-0016**, el servidor acepta cualquier `Usage` sin rechazar por saldo, y
   `recordfeedconsumption` ya produce un `Usage`.

## 3. Decisiones fijadas

Además de las citadas de feature-0016:

- **C1 — Una operación de sync nueva y genérica**, `recordinventoryusage`, que sirve para
  cualquier ítem (alimento, fármaco, insumo) con destino opcional.
  `recordfeedconsumption` no se toca.
- **C2 — Los saldos bajan completos en cada pull**, en la colección `inventoryBalances`, sin
  cursor incremental: son derivados y pocos.
- **C3 — El saldo mostrado es el último recibido menos las salidas propias pendientes**, con
  la fecha del cálculo a la vista. No se suman las salidas de otros teléfonos: el teléfono
  no las conoce.
- **C4 — Si el saldo mostrado queda negativo, se avisa y se deja registrar.** El aviso dice
  que es "según lo último que sabe este teléfono".

## 4. Alcance

### Entra

- Operación `recordinventoryusage` en el push.
- Colección `inventoryBalances` en el pull, con su tabla en WatermelonDB (migración del
  esquema local).
- Pantalla "Registrar uso": ítem, cantidad en la unidad o presentación que elija, fecha
  (por defecto ahora) y destino opcional (grupo o nota).
- Saldo con fecha en la lista de ítems y en la pantalla de uso.
- La entrada respeta los submódulos `inventory` e `inventory.usages`.

### No entra

- Recepciones, ajustes, conteo o transformación desde el teléfono. Van en el panel (D7).
- Vincular el uso a un animal o tratamiento (D5).
- Cambiar la pantalla de consumo por lote existente.
- Saldos por bodega.

## 5. Diseño: sincronización

**Push:** `recordinventoryusage`

```json
{ "itemId": "uuid", "quantity": 3, "unit": "saco40kg",
  "occurredAt": "2026-10-02T13:20:00Z", "groupId": "uuid|null", "notes": "texto|null" }
```

- El id de la operación es el id del `Usage`. Si llega dos veces, la segunda es un no-op
  (ADR-0008).
- La unidad se convierte con las conversiones del ítem. Una unidad desconocida **sí** se
  rechaza, con motivo legible en la bitácora de sync, porque no se puede saber cuánto salió.
  Es el único rechazo posible, y se detecta antes en el teléfono (el selector solo ofrece
  unidades conocidas).
- **Nunca se rechaza por saldo** (D3).

**Pull:** `inventoryBalances`

```json
{ "itemId": "uuid", "balance": 118.5, "unit": "kg", "computedAt": "2026-10-02T12:00:00Z" }
```

- Permiso: `inventory.items.read`.
- Siempre completa. El teléfono reemplaza su tabla en cada pull que la traiga.
- No lleva costos: el campo no los necesita, y así no circulan por teléfonos que se pueden
  perder.

## 6. Diseño: teléfono

- **Tabla local `inventory_balances`**: `item_id`, `balance`, `unit` y `computed_at`.
- **`InventoryUsageService`**: encola `recordinventoryusage` en el outbox de WatermelonDB en
  la misma escritura que el registro local, igual que `FeedConsumptionService`.
- **Saldo mostrado** = `balance` − Σ usos propios pendientes del ítem, convertidos a unidad
  base. Etiqueta: "118,5 kg (al sincronizar de hoy, 7:02)".
- **Aviso:** si el saldo mostrado menos lo que se va a registrar da negativo, un diálogo en
  la propia pantalla (no `Alert` del sistema) ofrece "Registrar igual" o "Revisar".
- **Visibilidad:** la entrada del menú de actividades usa `canShow('inventory.usages')`
  (feature-0017).

## 7. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| El saldo del teléfono engaña al usuario | Siempre se muestra con su fecha (C3). La alerta del servidor atrapa el negativo real |
| Un uso con unidad desconocida se pierde | El selector solo ofrece unidades del ítem. Si aun así llega, el rechazo queda visible en la bitácora de sync |
| La migración de WatermelonDB rompe instalaciones | Migración aditiva y prueba de actualización desde la versión instalada |
| La app vieja sigue enviando `recordfeedconsumption` | Se sigue aceptando (feature-0016) |

Deuda anotada en `docs/BACKLOG.md`: retirar `recordfeedconsumption` cuando ningún teléfono
la envíe.

## 8. Criterios de aceptación

1. Un uso registrado en modo avión aparece en el saldo local al instante y sube al recuperar
   señal, sin duplicarse aunque se reintente.
2. Un uso que deja el saldo local negativo muestra el aviso, se registra si se confirma y el
   servidor lo acepta.
3. Un uso con fecha de ayer, sincronizado hoy, queda en su lugar en el kardex del panel.
4. `inventoryBalances` no incluye costos.
5. Con `inventory.usages` apagado, "Registrar uso" no aparece en el teléfono tras
   sincronizar. Un uso ya encolado sube igual.
6. La app actualizada desde la versión instalada conserva el outbox pendiente.
7. Prueba de integración: `recordinventoryusage` idempotente, con unidad desconocida
   rechazada y con saldo negativo aceptado.
