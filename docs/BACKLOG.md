# BACKLOG.md — Ideas fuera de la fase actual

> Regla (ROADMAP.md, "Reglas anti-estancamiento" #2): ideas nuevas van aquí, no a la fase
> en curso. Se revisan al cerrar cada fase — algunas suben a la fase siguiente, otras se
> descartan con una línea explicando por qué.

## De la Fase 3 (sync + app de campo)

- **Extender el borrado lógico a otras entidades sincronizables.** Hoy sólo `Animal` tiene
  un método `Delete()` real (con la invariante "no eliminar con historia"). `AnimalGroup`,
  `InventoryItem` y las entidades de Breeding tienen la columna `deleted_at` heredada de
  `AuditableEntity` y el filtro de consulta, pero ninguna operación de dominio la asigna.
  Implementar cuando aparezca un caso de uso real (p. ej., "borré un lote por error").

- **Extender LWW a otras entidades editables.** `Animal.Update()` es el único caso —
  el que ADR-0008 nombra explícitamente como ejemplo ("Datos de Animales"). Si
  `AnimalGroup` u otra entidad gana un flujo de edición real, necesita el mismo patrón:
  campo de "última edición declarada" distinto de `UpdatedAt`, más la detección de
  conflicto en el handler.

- **Resolución manual de operaciones rechazadas desde el panel.** La bandeja de
  sincronización (`/sync` en admin-web) hoy es de solo lectura. El plan original mencionaba
  "resolución manual" para el piloto — pero eso requiere un endpoint de reintento
  (`POST /api/v1/sync/operations/{id}/retry` o similar) que no existe: hoy la única forma
  de corregir una operación rechazada es que el empleado la vuelva a registrar. Justificado
  sólo si el piloto real muestra que los rechazos son frecuentes y no triviales de
  re-capturar a mano.

- ~~**`ng test` de `admin-web` está roto**, sin relación con nada de este trabajo:~~
  `app.component.spec.ts` importa un símbolo `App` que no existe (`AppComponent` es el
  nombre real).~~ **Resuelto** el 2026-08-11 por `feature/admin-web-inventory-detail` (PR
  en curso): el spec importa `AppComponent` correctamente y la suite corre verde
  (13 archivos, 68 tests passing). ~~Bajo prioridad — no bloquea CI porque el pipeline
  no corre `ng test` — pero hay que arreglarlo antes de que el panel dependa de esa suite
  para algo real.~~

## De Fase 2 (heredado, seguía pendiente)

- **`Lactation` es un tipo de dominio sin ciclo de vida implementado.** Existe la entidad
  y su tabla, pero ninguna operación llama `Lactation.Start()` — ni al registrar un parto,
  ni al iniciar el ordeño de una vaca nueva. Antes de mostrar "lactancias activas" en
  cualquier panel, hay que decidir el disparador real: ¿se abre automáticamente al parto
  (Breeding) o manualmente al primer ordeño (Production)? Es una decisión de dominio, no
  una casilla de UI.

## De la Fase 3.5 (adaptación porcina) — diferido a propósito

> Lo que salió del levantamiento del 2026-08-05 y **deliberadamente no entra** en 3.5.
> Lo que sí entra está en `PLAN-FASE-3-5-PORCINO.md`; lo que ya quedó decidido en los
> ADR-0015/0016/0017 no se duplica acá.

- **Escaneo QR y carnetización desde el nacimiento.** El cliente lo mencionó como deseable
  ("sería difícil ver código por código el animal") y él mismo lo puso después del aretado:
  primero tiene que funcionar el resto. `AnimalIdentifier` ya soporta el tipo `RFID`
  (ADR-0006) y ADR-0015 hace que pasar un lote a modo `Individual` no requiera migración,
  así que el terreno está preparado. **Disparador:** que el cliente decida aretar, que
  según él ocurre si el FCR demuestra resultados. Mientras tanto, el buscador por
  identificador de 3.5a.9 cubre la necesidad real.

- **Densidad de corral (cabezas/m²).** Necesita el área de cada corral, que es parte de
  `Paddock`/`Grazing` — **Fase 5**. **Disparador:** cuando se abra el módulo de potreros.

- **Consumo de agua por lote.** No lo pidió nadie; se anota porque en porcinos una caída de
  consumo de agua precede a la de alimento como señal de enfermedad. **Disparador:** que la
  alerta de divergencia de consumo de alimento (sec.4.4 del plan) resulte demasiado tardía en
  el piloto.

- **Ambiente del corral (temperatura, humedad).** Requiere sensores que no existen en la
  finca. YAGNI (Art. 17). **Disparador:** que aparezca hardware instalado.

- **Costo por lote y por kg producido, en dinero.** El FCR de 3.5b se calcula **en kg** a
  propósito: está completo sin contabilidad y no adelanta trabajo de otra fase. Convertirlo
  a dinero es **Fase 4**, donde ya está previsto el centro de costo "porcinos".
  **Disparador:** apertura de la Fase 4.

- **Anclas adicionales del plan sanitario** (p. ej. "a los N días del primer celo").
  ADR-0016 fija cuatro anclas (`Nacimiento`, `InicioDeLote`, `Parto`, `Destete`) que cubren
  todo lo levantado. Agregar una quinta es catálogo *más* código en el resolutor de fechas.
  **Disparador:** un ítem real del cronograma del cliente que no se pueda expresar con las
  cuatro.

### Cerradas en `feature/admin-web-inventory-detail` (PR en curso, 2026-08-11)

- **UI admin-web para `InventoryItem`, `InventoryBatch`, `UnitConversion`, `FeedStage`.**
  ADR-0020 lo declaraba como deuda pendiente ("Pantallas de catálogos que faltan"), y
  quedaba como deuda rastreable de "P2/P3 del informe post-mortem". La nueva pantalla
  `/inventory/items/:id` muestra los datos del ítem, los lotes (con creación inline), las
  conversiones de unidad (con creación inline), y — sólo para `Category=Feed` — la etapa
  de alimento. La pestaña `inventory` de `/catalogs` ahora tiene una acción "Detalle"
  que navega ahí. **Estado:** mergeada en este PR; pendiente `BACKLOG.md` se cierra acá.
  **Lo que sigue faltando (deuda explícita, no resuelto en este PR):**
  desactivación/eliminación de lotes (no hay endpoint en el backend), edición del nombre/
  descripción del ítem (sólo lectura hoy), admin CRUD de feed-stages más allá de activar/
  desactivar (no hay pantalla; el panel sólo se conecta vía curl).

### Deuda explícita por desacople del piloto (ADR-0024, 2026-08-08)

> Estas piezas viven como **deuda rastreable** porque el inicio del piloto real no las
> exige como requisito (ver sub-criterio "Para abrir el piloto real" en
> `docs/planes/PLAN-FASE-3-5-PORCINO.md`). Quedan acá con disparador explícito, no se
> "esconden" en un criterio vago. Cuando el disparador ocurra, la entrada se promueve a
> tarea concreta de la rama o sub-plan correspondiente.

- **3.5a.7.1–5 — UI del sujeto "lote" (pesaje muestral, baja con causa, vacunación de
  lote, diagnóstico grupal, consumo en sacos).** Mergeada al `develop` por PR #88
  (rama `feature/field-app-lot-registration`, commit `04ff02f`, 2026-08-09), con
  `TapBudget` validado. Lo que queda pendiente es el **reorden** de las seis actividades
  del sujeto lote cuando el cliente responda sec.7-C del plan con las frecuencias reales.
  **Disparador:** respuesta del cliente a `PLAN-FASE-3-5-PORCINO.md` sec.7-C. Cuando
  ocurra, este ítem sale del backlog y entra a la tarea explícita de reorden.

- **3.5a.8 — Corrección de registros desde el teléfono (ADR-0017).** Sin esto, la
  corrección durante el piloto se hace por re-registro manual (lo que el criterio
  completo de 3.5a explícitamente rechaza como prueba). Aceptado por ADR-0024 mientras
  la deuda esté rastreable. **Disparador:** cualquier rechazo o dedupe en el outbox del
  cliente móvil durante el piloto que no pueda corregirse volviendo a registrar — o el
  operario reporta "no encuentro cómo arreglar lo que escribí mal".

- **3.5a.3 — Causas configurables de muerte de lechón.** Sin catálogo de causas, la
  mortalidad se registra sin causa (válido para el piloto; el índice de madres de 3.5b.6
  queda incompleto hasta que esto exista). **Disparador:** el cliente pide distinguir
  causas durante el piloto, o el índice de madres (3.5b.6) necesita la causa antes de
  que se cierre el primer ciclo de engorde.

## Bloqueante transversal (descubierto durante el piloto)

- **No hay forma trazable de "rellenar" inventario de comida desde el panel.** Existe
  `POST /api/v1/inventory/items/{itemId}/batches` que crea un `InventoryBatch` con cantidad,
  costo y vencimiento opcional — pero sin `ReceivedAt` propio, sin proveedor, sin factura,
  y sin evento de dominio. La UI admin-web (`InventoryBatchesSectionComponent`) lo expone
  como botón "Crear lote"; el **móvil no tiene UI** para reponer (solo consumir; el catálogo
  `inventoryBatches`/`unitConversions` no viaja en el pull del sync). El dueño puede registrar
  que llegó alimento, pero la fila queda asociada al `created_at` del sistema, no a una fecha
  declarada de recepción, y no se puede reconstruir qué proveedor entregó qué. **El consumo
  desde lote (3.5a.7) mergeado depende de este flujo para no trabajar contra stocks vacíos
  sin historia.** Solución acordada: comando mínimo `RecordInventoryReceptionCommand` con
  `ReceivedAt` (requerido) y `SupplierLabel`/`InvoiceReference`/`Notes` (opcionales, texto
  libre); endpoint dedicado `POST /api/v1/inventory/items/{itemId}/receptions`; permiso
  nuevo `inventory.receptions.manage`; UI admin-web "Recibir alimento"; comando diseñado
  abierto a extensión para que Fase 4 (Purchasing) lo envuelva con `SupplierId`/`PurchaseOrderId`
  FK sin romper contrato. Móvil **no** se toca: la reposición es labor de oficina, no del
  operario en el potrero (Art. 9). **Vida útil:** deprecado cuando llegue
  `Purchase/PurchaseReception` de Fase 4 — los batches existentes preservan `SupplierLabel`
  como etiqueta histórica; Fase 4 añade un script de deduplicación texto→`Supplier`. Cubierto
  por ADR-0026.
  **Disparador:** el piloto real pierde trazabilidad de compras, o el contador pide
  reconstruir el proveedor de un batch viejo y no se puede.

## Ideas sin fase asignada

- **Fotos de eventos**: la app de campo ya guarda la referencia local (`photoUri`) y la
  marca explícitamente como `photoUploaded: false`; la subida real depende del módulo de
  adjuntos (`feature/shared-attachments`, Fase 4).
