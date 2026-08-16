# tasks.md — Desglose ejecutable (archivado)

> **Documento archivado.** Casi todo está `[x]`: es el registro de lo que costó cerrar la
> Fase 3, no una lista de pendientes. La única casilla abierta es deliberada — ver T3C.2.
> Marcado según la regla de `docs/DOCUMENTACION.md` sec. 4.
>
> Checklist de la Fase 3 (ramas `feature/people-permissions` … `docs/fase-3-cierre`, más
> `fix/fase-3-sync-correctness`). Agrupada por el bloque de [`plan.md`](./plan.md) al que
> pertenece cada rama.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Bloque 3.A — Suelo en el backend

- [x] **T3A.1** `feature/people-permissions`: roles y permisos en BD, semilla que preserva
      los 3 roles previos, `PermissionAuthorizationHandler`, endpoints de roles, pantalla
      Angular de administración.
- [x] **T3A.2** `feature/people-audit-trail`: `RecordedBy` migrado a `Guid` con
      `recorded_by_label` de respaldo, interceptor de auditoría, endpoint y vista de
      bitácora.
- [x] **T3A.3** `feature/sync-protocol-pull`: ADR-0008, índices de sincronización,
      `GET /api/v1/sync/pull`, filtrado por permisos, colecciones v1.
- [x] **T3A.4** `feature/sync-protocol-push`: `sync_operations` con `client_operation_id`
      único, `POST /api/v1/sync/push`, respuesta por operación, los 10 escenarios de
      `spec.md` sec. 2.2 cubiertos en `Hato.Modules.Sync.IntegrationTests`.
- [x] **T3A.5** Hito 3.A verificado a mano: usuario con rol limitado, `pull` → `push` con
      duplicados por `curl` → `pull` → datos una sola vez.

## Bloque 3.B — La app

- [x] **T3B.1** `feature/field-app-scaffolding`: ADR-0009, esqueleto `clients/field-app`,
      login con sesión offline, CI del cliente.
- [x] **T3B.2** `feature/field-app-sync-engine`: outbox local, sincronizador con backoff y
      detección de conectividad, estados visibles, bandeja de rechazos.
- [x] **T3B.3** `feature/field-app-milking`: flujo de ordeño en ≤3 toques, marca de retiro,
      resumen y edición del día en curso.
- [x] **T3B.4** `feature/field-app-events`: tratamientos, pesajes, movimientos, selector de
      animal sin arete, fotos encoladas.
- [x] **T3B.5** `feature/field-app-births`: parto con creación de cría por UUID de cliente,
      padre dual, camadas sin `if` por especie.
- [x] **T3B.6** Hito 3.B verificado en un primer intento con teléfono real — insuficiente,
      ver T-FIX abajo.

## Corrección post-cierre (`fix/fase-3-sync-correctness`)

> Nace de la auditoría que revirtió el primer cierre de la fase (`spec.md` sec. 4), no de
> la planificación original.

- [x] **T-FIX.1** Interceptor de auditoría registrado también en los `DbContext` de
      `Livestock` — `created_at`/`updated_at` ya no quedan en `0001-01-01`.
- [x] **T-FIX.2** `recordBirth` en el push acepta `motherId`; la cría ya no llega huérfana
      al servidor.
- [x] **T-FIX.3** Outbox migrado de `localStorage` (inexistente en React Native) a
      WatermelonDB real; lo pendiente sobrevive al cierre de la app.
- [x] **T-FIX.4** Cursor `(timestamp, id)` con orden determinista y frontera por colección;
      sellado universal de marcas de tiempo.
- [x] **T-FIX.5** Idempotencia por reserva previa con índice único; UUID de cliente
      aceptado por el servidor (Art. 3).
- [x] **T-FIX.6** Suite dedicada `Hato.Sync.IntegrationTests` cubre los 10 escenarios de
      `spec.md` sec. 2.2, no solo 3 de 10 como en el primer intento.

## Bloque 3.C — Piloto y cierre

- [x] **T3C.1** `feature/field-app-pilot-hardening` (con iteraciones `-2`, `-3`): pantalla
      de estado de sincronización legible para un empleado, bandeja de conflictos y
      rechazos con resolución manual en `admin-web`, bitácora de conflictos LWW alcanzable
      desde uso real (antes, ningún cliente podía disparar un conflicto LWW fuera de una
      prueba).
- [ ] **T3C.2** El piloto real: una semana completa de registros de campo hechos por un
      empleado desde un teléfono de verdad, incluyendo días sin señal, sin pérdida ni
      duplicación de datos — el criterio de salida de la fase (`spec.md` sec. 4, cita de
      `ROADMAP.md:121-125`: *"sólo falta una cosa y es deliberadamente ajena al código: el
      piloto real... nada de trabajo de ingeniería puede sustituir esa semana"*).
      **Terminado:** una semana calendario documentada con los registros hechos, sin
      intervención de código durante la semana.
- [x] **T3C.3** Retrospectiva de `docs/fase-3-cierre` escrita en `ROADMAP.md` y trasladada
      a `spec.md` sec. 4 de esta carpeta.

---

## Cierre

- [x] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo — los 10 escenarios de
      sincronización, cubiertos por `Hato.Sync.IntegrationTests` y verificados a mano en
      los hitos 3.A y 3.B.
- [x] **TC.2** `Hato.Sync.IntegrationTests` y las pruebas de `field-app`/`admin-web`
      citadas en `spec.md` sec. 2.2 y 3 en verde en CI.
