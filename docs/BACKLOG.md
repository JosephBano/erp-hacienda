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

## Ideas sin fase asignada

- **Fotos de eventos**: la app de campo ya guarda la referencia local (`photoUri`) y la
  marca explícitamente como `photoUploaded: false`; la subida real depende del módulo de
  adjuntos (`feature/shared-attachments`, Fase 4).
