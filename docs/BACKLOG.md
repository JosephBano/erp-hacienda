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

- **`ng test` de `admin-web` está roto**, sin relación con nada de este trabajo:
  `app.component.spec.ts` importa un símbolo `App` que no existe (`AppComponent` es el
  nombre real). Bajo prioridad — no bloquea CI porque el pipeline no corre `ng test` — pero
  hay que arreglarlo antes de que el panel dependa de esa suite para algo real.

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

### Deuda explícita por desacople del piloto (ADR-0024, 2026-08-08)

> Estas piezas viven como **deuda rastreable** porque el inicio del piloto real no las
> exige como requisito (ver sub-criterio "Para abrir el piloto real" en
> `docs/planes/PLAN-FASE-3-5-PORCINO.md`). Quedan acá con disparador explícito, no se
> "esconden" en un criterio vago. Cuando el disparador ocurra, la entrada se promueve a
> tarea concreta de la rama o sub-plan correspondiente.

- **3.5a.7.1–5 — UI del sujeto "lote" (pesaje muestral, baja con causa, vacunación de
  lote, diagnóstico grupal, consumo en sacos).** Sin estas pantallas, el flujo viejo
  (`recordAnimalEvent` con `GroupId` directo) cubre la captura grupal a través de las
  pantallas individuales — documentado como deuda, no como UX. La pieza 3.5a.7 tarea 6
  (endpoint ficha del lote) sí está mergeada (PR #66). **Disparador:** (a) el cliente
  responde las preguntas de `PLAN-FASE-3-5-PORCINO.md` sec.7-C (frecuencias reales), y/o
  (b) la compuerta sec.2.3 se cierra para el segundo nivel del árbol (vía ADR-0021 o un
  sucesor). Cuando ocurra cualquiera de los dos, 3.5a.7.1–5 sale del backlog y entra a
  la rama `feature/field-app-lot-registration` con `TapBudget` validado contra el
  operador.

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

## Ideas sin fase asignada

- **Fotos de eventos**: la app de campo ya guarda la referencia local (`photoUri`) y la
  marca explícitamente como `photoUploaded: false`; la subida real depende del módulo de
  adjuntos (`feature/shared-attachments`, Fase 4).
