# ADR-0008 — Protocolo de Sincronización Offline (Pull/Push, Cursore, Tombstones y Conflictos)

- **Estado:** Aceptado
- **Fecha:** 2026-08-02

## Contexto
La aplicación móvil React Native debe operar en modo offline-first en zonas rurales sin señal celular. Los empleados deben poder descargar el catálogo y estado de la finca, realizar registros de campo (ordeño, tratamientos, partos, movimientos) en modo avión, y sincronizar bidireccionalmente con el servidor backend cuando recuperen señal sin perder ni duplicar información (Art. 1, Art. 5, Art. 9).

## Decisión

### 1. Protocolo de Lectura Incremental (Pull)
- **Endpoint:** `GET /api/v1/sync/pull?since=<cursor>&collections=<opcional>`
- **Cursor de Sincronización:** Basado en `updated_at` (UTC) + `id` como desempate para evitar la pérdida de registros creados en el mismo milisegundo.
- **Colecciones Sincronizables en v1:** `animals`, `animal_identifiers`, `animal_groups`, `group_memberships`, `species`, `breeds`, `animal_categories`, `inventory_items`, `alerts`.
- **Tombstones:** Las entidades eliminadas lógicamente (`is_deleted = true`, `deleted_at != null`) se incluyen en la respuesta del pull con la marca `isDeleted: true` para que la base local (WatermelonDB/SQLite) las remueva de las vistas locales manteniendo consistencia.
- **Estructura de Respuesta:**
  ```json
  {
    "cursor": "2026-08-02T16:50:00.0000000Z_3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "hasMore": false,
    "collections": {
      "animals": [...],
      "animalGroups": [...],
      "species": [...]
    }
  }
  ```

### 2. Protocolo de Escritura Idempotente (Push)
- **Endpoint:** `POST /api/v1/sync/push`
- **Garantía de Idempotencia:** Tabla `sync_operations` (`client_operation_id` UUID **unique**, `user_id`, `device_id`, `received_at`, `status`, `result_ref`).
- **Lote Tipado de Operaciones:** Cada operación enviada incluye su `clientOperationId` (UUIDv4 generado en el cliente) y su fecha/hora declarada de ocurrencia `occurredAt`.
- **Respuesta Granular por Operación:** El servidor responde el resultado independiente de cada operación (`accepted`, `duplicate`, `rejected`). Un error en una operación individual no aborta las demás operaciones válidas del lote.

### 3. Resolución de Conflictos
- **Eventos e Historia (Append-Only):** Inmutables (Art. 1). No hay conflictos de edición en eventos de ordeño o tratamientos; se insertan con idempotencia por `clientOperationId`.
- **Entidades Editables (p. ej., Datos de Animales):** Estrategia *Last-Write-Wins (LWW)* basada en `updated_at` reportado. Los reemplazos o inconsistencias se registran automáticamente en una bitácora de conflictos para auditoría en el panel web.

## Alternativas Descartadas
- **GraphQL Subscriptions / WebSockets:** Descartado por inestabilidad en redes celulares rurales de alta latencia o desconexión prolongada.
- **Full Database Dump en cada Sync:** Descartado por consumo excesivo de ancho de banda y batería en dispositivos móviles.

## Consecuencias
+ **Resiliencia Operativa:** Cero pérdida de datos y tolerancia total a desconexiones prolongadas.
+ **Eficiencia:** Transferencias incrementales ligeras y reintentos idempotentes.
− **Complejidad en Backend:** Requiere índices universales `(updated_at, id)` y tabla de idempotencia `sync_operations`.
