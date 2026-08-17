# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/field-app-parto-redesign`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Tarea 0 ✅ CERRADA el 2026-08-16

- [x] **T0.1** Consulta ejecutada contra la base real. **0 filas** para `101`/`102`/`103`
      en `livestock.animal_identifiers`.
- [x] **T0.2** Ampliada a un barrido de toda columna de texto de los seis esquemas:
      **0 coincidencias**. Resultado registrado en `spec.md` sec. 2.4.
- [x] **T0.3** Hipótesis confirmada: son filas huérfanas en el dispositivo. **El plan
      procede.**
- [x] **T0.4** Hallazgo adicional: la etiqueta de respaldo de `loadHerd` colapsa a
      `Sin arete · 000000` para 53 de 67 animales (`spec.md` sec. 2.8, decisión D11).

---

## Commit 1 — Saneamiento del pull en el servidor

- [x] **T1.1** `grep` de `healthPlans`, `healthPlanItems`, `healthPlanAssignments` en todo
      el repo para confirmar que ningún consumidor los lee del pull.
      **Terminado:** lista de coincidencias revisada; ninguna es un consumidor real.
- [x] **T1.2** Retirar las tres colecciones de `RequiredPermissionByCollection`, de
      `SyncCollectionsDto` y de las lecturas de `GetSyncPullQueryHandler`
      (`src/Hato.Api/Sync/SyncPullQueries.cs`).
- [x] **T1.3** Borrar los DTOs `SyncHealthPlanDto`, `SyncHealthPlanItemDto` y
      `SyncHealthPlanAssignmentDto` si quedan sin uso.
- [x] **T1.4** Actualizar las pruebas de integración de sync que afirmen sobre ellas.
- [x] **T1.5** `dotnet test` completo en verde.

---

## Commit 2 — Saneamiento del motor de sync en el cliente

- [x] **T2.1** `applyCollections` (`syncEngine.ts`): ante colección desconocida, registrar
      por `loggerService` y contarla como error de sincronización en vez de `continue`.
      **Terminado:** existe una prueba que falla si se vuelve a poner el `continue` mudo.
- [x] **T2.2** `applyRow` (`syncEngine.ts:365`): eliminar `record.isDeleted = false`.
- [x] **T2.3** Implementar el reset de tablas espejo: borra las tablas de
      `TABLE_BY_COLLECTION` y la fila `pull_cursor` de `sync_meta`.
      **Nunca `sync_outbox` ni `milk_yields`.**
- [x] **T2.4** Acción "Rehacer descarga" en `SyncStatusScreen.tsx`, con confirmación
      explícita en español llano que diga qué se conserva y qué se vuelve a bajar.
- [x] **T2.5** El reset dispara `syncNow()` al confirmar.
- [x] **T2.6** Prueba: colección desconocida → error visible.
- [x] **T2.7** Prueba: fila con `isDeleted: true` del servidor no queda en `false`.
- [x] **T2.8** **Prueba: rehacer descarga vacía las espejo y `sync_outbox` sobrevive
      intacto.** Sin esta prueba el commit no entra (regla dura 10).
- [x] **T2.9** `npm test` completo en verde.

---

## Commit 3 — Preñeces y servicios en el pull

- [x] **T3.1** `SyncPregnancyDto(Id, DamId, ServiceId, Status, ExpectedBirthDate,
      CreatedAt, UpdatedAt, IsDeleted)` implementando `ISyncRow`.
- [x] **T3.2** `SyncBreedingServiceDto(Id, DamId, ServiceType, SireAnimalId, StrawId,
      CreatedAt, UpdatedAt, IsDeleted)` implementando `ISyncRow`.
- [x] **T3.3** Ambas en `SyncCollectionsDto` y en `RequiredPermissionByCollection` bajo
      `SystemPermissions.BreedingEventsRead`.
- [x] **T3.4** Inyectar `IBreedingDbContext` en `GetSyncPullQueryHandler` y agregar sus dos
      llamadas a `ReadAsync`.
- [x] **T3.5** Verificar que **no se tocó** ningún archivo de
      `Hato.Modules.Breeding.Domain`. **Terminado:** `git diff --stat` no lista archivos de
      ese proyecto.
- [x] **T3.6** Prueba de integración: usuario con `breeding.events.read` recibe ambas.
- [x] **T3.7** Prueba de integración: usuario sin el permiso no las recibe **ni pidiéndolas
      por nombre** en `collections=`.
- [x] **T3.8** Prueba de integración: registrar un parto pasa la preñez a `Completed` y el
      cambio viaja en el siguiente pull.
- [x] **T3.9** Prueba de integración: el cursor avanza bien con las colecciones nuevas.
- [x] **T3.10** `dotnet test` completo en verde.

---

## Commit 4 — Espejo local y `loadPregnantDams`

- [x] **T4.1** `schema.ts`: `tableSchema` de `pregnancies`
      (`dam_id`, `service_id`, `status`, `expected_birth_date`, `is_deleted`,
      `server_created_at`, `server_updated_at`).
- [x] **T4.2** `schema.ts`: `tableSchema` de `breeding_services`
      (`dam_id`, `service_type`, `sire_animal_id`, `straw_id`, `is_deleted`,
      `server_created_at`, `server_updated_at`).
- [x] **T4.3** `SCHEMA_VERSION` de 10 a 11.
- [x] **T4.4** `migrations.ts`: paso `toVersion: 11` con los dos `createTable`.
      **Terminado:** una base en v10 con datos abre en v11 sin perderlos.
- [x] **T4.5** `models.ts`: modelos `Pregnancy` y `BreedingService`, registrados en
      `modelClasses`.
- [x] **T4.6** `syncEngine.ts`: `pregnancies` y `breedingServices` en
      `TABLE_BY_COLLECTION`.
- [x] **T4.7** `herdQueries.ts`: `loadPregnantDams` con la interfaz `PregnantDam` del spec
      sec. 6.3.
- [x] **T4.8** Resolución de `sireLabel` según las cuatro filas de la tabla del spec 6.3.
- [x] **T4.9** Filtrar `status === 'Active'` y `!isDeleted`; excluir animales borrados;
      ordenar por fecha probable de parto ascendente.
- [x] **T4.10** `birthService.ts`: enviar `pregnancyId`; dejar de rellenar padre desde la
      pantalla.
- [x] **T4.10b** `loadHerd`: corregir la etiqueta de respaldo (D11) para que distinga
      animales cuyos ids comparten prefijo. **Terminado:** una prueba con dos ids del
      patrón real `00000000-0000-5000-8000-XXXXXXXX0000` produce dos etiquetas distintas.
- [x] **T4.11** Prueba por cada fila de la tabla de `sireLabel`, incluido el semental
      ausente localmente → `sin registrar`, nunca un id crudo.
- [x] **T4.12** Prueba: preñeces `Completed` y `Aborted` quedan fuera.
- [x] **T4.13** Prueba: el orden es por fecha probable de parto ascendente.
- [x] **T4.14** Prueba en `schema.test.ts`: la migración 10 → 11 conserva los datos.
- [x] **T4.15** `npm test` completo en verde.

---

## Commit 5 — Asistente de parto en cuatro pasos

- [x] **T5.1** Crear `src/screens/birth/` con el contenedor de pasos y el estado
      compartido del asistente.
- [x] **T5.2** Paso 1 — Elegir madre: lista de preñadas con fecha probable de parto.
- [x] **T5.3** Paso 1 — `EmptyState` que explique que solo aparecen hembras con preñez
      activa y que la preñez se registra en el panel.
- [x] **T5.4** Paso 2 — Confirmar datos: padre de **solo lectura**, fecha del parto (hoy por
      defecto, editable), dificultad (`Normal` por defecto).
- [x] **T5.5** Paso 3 — Crías: header fijo (total y desglose M/F), lista scrolleable, footer
      fijo con `+Hembra` / `+Macho` y avanzar.
- [x] **T5.6** Paso 3 — conservar arete opcional, peso opcional con validación de decimal
      positivo, cambio de sexo y quitar cría (3.5a.0 #2 y 3.5a.4).
- [x] **T5.7** Paso 4 — Resumen y confirmación; encolar y volver a Inicio.
- [x] **T5.8** Indicador de progreso de cuatro puntos en todos los pasos.
- [x] **T5.9** Atrás conserva lo ya cargado.
- [x] **T5.10** `App.tsx`: dejar de pasar `dams`/`sires`; pasar las preñadas.
- [x] **T5.11** Verificar que ningún archivo de `src/screens/birth/` pase de ~150 líneas.
- [x] **T5.12** Prueba: el paso 1 lista solo preñadas y muestra el `EmptyState` cuando no
      hay ninguna.
- [x] **T5.13** Prueba: **no existe ningún `testID` `sire-*`** en toda la pantalla.
- [x] **T5.14** Prueba: camada editable — agregar, quitar, cambiar sexo, arete, peso.
- [x] **T5.15** Prueba: el peso rechaza valores no positivos.
- [x] **T5.16** Prueba: retroceder de paso conserva las crías cargadas.
- [x] **T5.17** Prueba: confirmar encola con `pregnancyId` y **sin** padre.
- [x] **T5.18** Prueba: camada de 20 — contador y botones siguen accesibles.
- [x] **T5.19** `npm test` completo en verde.

---

## Commit 6 — Inicio y limpieza

- [ ] **T6.1** `ActivitiesHub.tsx`: activar `scrollable` y revisar el espaciado entre
      grupos, respetando el mínimo de 64pt del tema.
- [ ] **T6.2** Verificar que **no se alteró el orden de los sujetos** (spec sec. 8).
- [ ] **T6.3** Eliminar `src/screens/HomeScreen.tsx`.
- [ ] **T6.4** Eliminar su import en `src/App.tsx`.
- [ ] **T6.5** Eliminar `tests/HomeScreen.test.tsx`.
- [ ] **T6.6** `BACKLOG.md`: anotar la reconciliación automática de existencia.
- [ ] **T6.7** `BACKLOG.md`: anotar `MilkingScreen`, `EventsScreen`, `TreatScreen` y
      `LotEventsScreen` con el mismo problema de espacio.
- [ ] **T6.8** `npm test` completo en verde.

---

## Cierre

- [ ] **TC.1** `dotnet test` completo en verde.
- [ ] **TC.2** `npm test` completo en verde en `clients/field-app`.
- [ ] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) completo sobre un dispositivo real.
- [ ] **TC.4** Los nueve criterios de aceptación del spec sec. 10, verificados uno por uno.
- [ ] **TC.5** Abrir el PR con la descripción de [`plan.md`](./plan.md), sección
      "Descripción del PR".
