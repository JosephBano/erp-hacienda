# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/sync-field-app-reliability`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Evidencia de producción

> Nada más de esta rama se escribe hasta cerrarla.

- [x] **T0.1** Registrar build/versión del APK de cada teléfono en uso y versión del backend
      desplegado. **Terminado:** APK v1.0.0 (commit 0faf483 / HEAD develop), backend v1.0.0-fase3 (commit 0faf483).
- [x] **T0.2** Determinar qué significó «eliminar» en el reporte: `DisposedAt`, `DeletedAt`,
      membresía desactivada o SQL manual. **Terminado:** borrado lógico `DeletedAt` y baja `DisposedAt` no refrescaban pantallas abiertas (S1/S2); las bajas no se excluían en `herdQueries.ts:93`.
- [x] **T0.3** Determinar si el fallo de sincronización es constante o intermitente y en qué
      pantalla aparece. **Terminado:** constante en ordeño individual por S8 (`isPlausibilityConfirmed` no mapeado -> JsonException -> Rejected), blanqueado intermitentemente por S9 (`Duplicate`).
- [x] **T0.4** Decidir si el ordeño individual se usa en esta finca. **Terminado:** sí se usa en producción (bovinos de leche). Procede la corrección.

---

## Commit 1 — Arnés de contrato de push

- [x] **T1.1** Enumerar los `operationType` que el cliente encola: recorrer los
      `outbox.enqueue(...)` de `clients/field-app/src/services/`.
      **Terminado:** lista cerrada de diez operaciones (`recordMilking`, `recordAnimalEvent`, `createTreatmentCourse`, `recordGroupEvent`, `recordFeedConsumption`, `createAnimal`, `recordBirth`, `moveAnimal`, `updateAnimal`, `recordCorrection`), contrastada con los `case` de `src/Hato.Api/Sync/PushSyncCommands.cs:208-322`.
- [x] **T1.2** Escribir `clients/field-app/src/services/__tests__/pushPayloadContracts.test.ts`:
      ejercita cada servicio y captura el payload que encola.
      **Terminado:** `npm test -- pushPayloadContracts` genera `docs/contracts/push-payloads.json`
      con una entrada por tipo.
- [x] **T1.3** El fixture lleva encabezado que declara que es generado y no editable a mano.
      **Terminado:** el archivo contiene esa nota y el comando que lo regenera.
- [x] **T1.4** El test del cliente falla si un servicio encola un tipo ausente del fixture.
      **Terminado:** verificado; añadir un tipo inventado falla la prueba, y se recupera en verde.
- [x] **T1.5** Escribir `tests/Hato.Sync.IntegrationTests/SyncPushContractTests.cs`: lee el
      fixture y deserializa cada payload contra su comando con las `JsonOptions` reales de
      `PushSyncCommands` (`UnmappedMemberHandling.Disallow` incluido).
      **Terminado:** usa `PushSyncBatchCommandHandler.JsonOptions` de producción.
- [x] **T1.6** Añadir comprobación de completitud en ambos sentidos: todo `case` del
      despachador tiene entrada en el fixture, y todo tipo del fixture tiene `case`.
      **Terminado:** probado en `PushContracts_Completeness_BothDirections`.
- [x] **T1.7** Escribir `tests/Hato.Sync.IntegrationTests/SyncPushMilkingTests.cs` con
      ordeño individual, confirmación `true` y `false`. **Terminado:** el archivo existe y cubre ambos casos.
- [x] **T1.8** **Reproducción de S8.** `dotnet test --filter SyncPushContractTests` falla en
      `recordMilking` con `JsonException` de miembro no mapeado, y los otros nueve tipos pasan.
      **Terminado:** 10 pasadas, 1 fallada (`recordMilking` con `JsonException: The JSON property 'isPlausibilityConfirmed' could not be mapped to any .NET member contained in type 'RecordMilkingSessionCommand'`).

---

## Commit 2 — Confirmación de plausibilidad en ordeño

- [x] **T2.1** Añadir `IsPlausibilityConfirmed` a `MilkingSession`, con parámetro de default
      `false` en `Create` (firma actual en `MilkingSession.cs:57`).
      **Terminado:** ningún llamador existente de `Create` necesita cambiar.
- [x] **T2.2** Declarar `bool IsPlausibilityConfirmed = false` en `RecordMilkingSessionCommand`
      y pasarlo a `Create`. **Terminado:** el record refleja exactamente lo que el móvil envía.
- [x] **T2.3** Configuración EF: `IsRequired().HasDefaultValue(false)`, calcado de
      `TreatmentCourseConfiguration.cs:80`.
- [x] **T2.4** Generar la migración EF en el módulo `Production` (regla 7).
      **Terminado:** migración `20260907193624_AddMilkingSessionPlausibilityConfirmed` generada con snapshot actualizado.
- [x] **T2.5** Exponer el campo en `src/Hato.Api/Endpoints/MilkingEndpoints.cs`.
      **Terminado:** expuesto en `MilkingSessionDto` y aceptado en `RecordMilkingSessionCommand`.
- [x] **T2.6** `dotnet test --filter "SyncPushContractTests|SyncPushMilkingTests"` en verde.
      **Terminado:** 13 pruebas pasadas; `recordMilking` deserializa y persiste el flag.
- [x] **T2.7** Confirmar que **no** se relajó `UnmappedMemberHandling`.
      **Terminado:** `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` verificado intacto.
- [x] **T2.8** `dotnet test` completo en verde.
      **Terminado:** suite completa de backend pasando contra PostgreSQL real.

---

## Commit 3 — El pull deja de mentir

- [x] **T3.1** `pullChanges` (`syncEngine.ts:242`): agotar `MAX_PULL_PAGES` deja de retornar
      `{ ok: true }` y expresa trabajo pendiente.
      **Terminado:** el resultado distingue «terminé» de «me quedé sin presupuesto» retornando `ok: false` y `reason: 'pending'`.
- [x] **T3.2** Prueba con API falsa que responde siempre `hasMore: true` y colecciones
      vacías. **Terminado:** tras 200 llamadas el resultado **no** es `ok: true`, es `{ ok: false, reason: 'pending' }`.
- [x] **T3.3** Prueba de que el cursor solo avanza tras aplicar su página, y que una
      interrupción admite replay idempotente sin volver a mostrar una entidad borrada.
      **Terminado:** verificado en `syncEngine.test.ts`.
- [x] **T3.4** Ningún estado de la UI muestra «todo actualizado» con trabajo pendiente.
      **Terminado:** probado en `SyncStatusScreen.test.tsx`.
- [x] **T3.5** `npm test` completo en verde.
      **Terminado:** 36 suites pasadas, 239 pruebas pasadas.

---

## Commit 4 — `Duplicate` deja de blanquear rechazos

- [x] **T4.1** `PushSyncCommands.cs:116`: la respuesta `Duplicate` distingue una operación
      previamente **aceptada** de una previamente **rechazada**.
      **Terminado:** el cliente puede decidir sin adivinar; hoy ambas llegan iguales.
- [x] **T4.2** `syncEngine.ts:171`: `Duplicate` con `errorDetails` conserva el estado de
      rechazo y su motivo, en vez de `markSynced`.
      **Terminado:** el comentario «Accepted and Duplicate are the same outcome» se corrige o
      se elimina; hoy afirma algo que deja de ser cierto.
- [x] **T4.3** Prueba de integración: operación rechazada, respuesta perdida, reintento con la
      misma `clientOperationId`. **Terminado:** sigue rechazada, con el motivo original.
- [x] **T4.4** Prueba del motor equivalente en el cliente.
- [x] **T4.5** `dotnet test` y `npm test` completos en verde.

---

## Compuerta 1 — Reservas interrumpidas

- [x] **TG1.1** Determinar si conservar el resultado real de una reserva pendiente (D4) exige
      modificar ADR-0008. **Terminado:** respuesta razonada por escrito en el PR.
- [x] **TG1.2** Si lo modifica: **detenerse**, redactar el ADR y sacar el tema de esta rama.
      **Terminado:** el spec registra la reserva como pendiente y el commit 5 se limita a
      reintento y coordinación.
- [x] **TG1.3** Comprobar que una reserva persistida antes de una escritura en otro módulo no
      se reejecuta sin verificar sus efectos. **Terminado:** no se asume atomicidad entre los
      `DbContext` existentes (spec sec. 5).

---

## Commit 5 — Ejecución única y reintento programado

- [x] **T5.1** Una sola ejecución coordinada compartida por todos los disparadores.
      **Terminado:** dos disparadores simultáneos producen una ejecución; cubierto por prueba.
- [x] **T5.2** `retryDelayMs` (`syncEngine.ts:87`) obtiene consumidor real: planificación de
      reintento mientras la app está activa. **Terminado:**
      `rg -n 'retryDelayMs' clients/field-app/src` encuentra un consumidor, no solo la definición.
- [x] **T5.3** Disparador por vuelta a primer plano (`AppState`), además del de conectividad
      ya existente en `start()`.
- [x] **T5.4** Un rechazo de negocio **no** entra en bucle de reintentos.
      **Terminado:** cubierto por prueba con una operación rechazada.
- [x] **T5.5** Caducidad de sesión informa cómo recuperarla y conserva los registros locales.
- [x] **T5.6** `npm test` completo en verde.

---

## Commit 6 — Las vistas reflejan la base

- [x] **T6.1** El motor notifica al contenedor cuando una sincronización aplica cambios, no
      solo al cambiar módulos. **Terminado:** existe la señal y hay prueba de que se emite.
- [x] **T6.2** `App.tsx:130,178` refresca el hato y demás estado con esa notificación.
- [x] **T6.3** `SyncStatusScreen.tsx:63,72` coherente con lo anterior.
- [x] **T6.4** Si desaparece el animal seleccionado: se informa, se impide enviar contra la
      selección obsoleta y **el formulario ya escrito no se descarta**.
      **Terminado:** cubierto por prueba de contenedor; regla dura 10.
- [x] **T6.5** Prueba de que la pantalla abierta refleja el cambio sin reiniciar, sin cambiar
      pestaña y sin un segundo botón.
- [x] **T6.6** `npm test` completo en verde.

---

## Commit 7 — Recuperación manual segura

- [x] **T7.1** `resetMirror` (`syncEngine.ts:188`) se coordina con `running`: exclusión mutua
      con la sincronización.
- [x] **T7.2** `SyncStatusScreen.tsx:77` no invoca la recuperación antes de comprobar
      conectividad ni deja el dispositivo sin el único catálogo utilizable sin conexión.
- [x] **T7.3** La prueba existente de que `sync_outbox` sobrevive intacto sigue en verde.
      **Terminado:** regla dura 10; sin esta prueba el commit no entra.
- [x] **T7.4** Prueba nueva: recuperación interrumpida a mitad no pierde formularios
      pendientes ni registros exclusivamente locales.
- [x] **T7.5** `npm test` completo en verde.

---

## Commit 8 — Diagnóstico persistente

- [ ] **T8.1** `loggerService.ts:59,65` persiste en almacenamiento nativo disponible, sin
      depender de `localStorage`. **Terminado:** **sin dependencias nuevas** (D5); si hiciera
      falta una, detenerse y proponer ADR (regla 2).
- [ ] **T8.2** Cada registro lleva fecha UTC, versión de app y esquema, identificador de
      intento, etapa fallida, colección, conteos y operación correlacionable.
- [ ] **T8.3** Retención acotada, definida y probada.
- [ ] **T8.4** Acción explícita del usuario para compartir el diagnóstico.
      **Terminado:** no se envía nada a terceros automáticamente.
- [ ] **T8.5** Revisión de que no se registran JWT, contraseñas ni payloads completos.
      **Terminado:** revisión manual documentada en el PR, más prueba de que un payload con
      un campo sensible no aparece íntegro en el registro.
- [ ] **T8.6** Prueba (b) de `spec.md` sec. 2.1 invertida: sin `localStorage`, un logger
      nuevo **recupera** el error registrado por el anterior.
- [ ] **T8.7** `npm test` completo en verde.

---

## Cierre

- [ ] **TC.1** `dotnet test` completo en verde, contra PostgreSQL real (regla 5).
- [ ] **TC.2** `npm test` completo en `clients/field-app`. Las seis omisiones existentes
      siguen siendo seis; esta rama no las aumenta.
- [ ] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) completo sobre **SQLite nativo** en
      dispositivo real. Las pruebas del motor corren sobre LokiJS y no bastan para aceptar
      en campo (spec sec. 2.1).
- [ ] **TC.4** Actualizar `ROADMAP.md`, que conserva un estado anterior al reporte
      (spec sec. 1). **Terminado:** refleja lo que esta rama cierra y lo que sigue abierto.
- [ ] **TC.5** Anotar en `docs/BACKLOG.md` la deuda que esta rama detectó y no arregló
      (regla 9), incluida la reserva de la Compuerta 1 si quedó fuera.
- [ ] **TC.6** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluido el
      resultado de la Compuerta 0 y la salida del fallo de T1.8.
