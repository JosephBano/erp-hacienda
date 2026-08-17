# tasks.md — Desglose ejecutable de la Fase 3.5

> Checklist de las ~17 ramas de la Fase 3.5. Cada tarea cita su sección de origen en
> [`spec.md`](./spec.md) / [`spec-3.5a.md`](./spec-3.5a.md) para que quien la marque pueda
> volver al porqué. **Casillas marcadas contra el código real** por auditoría de contexto
> limpio en la rama `docs/reestructura-documentacion` (2026-08-16): cada `[x]` cita
> archivo:línea como evidencia, nunca un documento. 61 de 84 marcadas.
>
> Convención: `[ ]` pendiente/sin evidencia suficiente · `[x]` hecho con evidencia · `[!]` bloqueada.

---

## Bloque 3.5a — Captura

### 3.5a.0 — `feature/field-app-input-guards`

- [x] **T-3.5a.0-1** Corregir `assertVolume` (`milkingService.ts:247`) a `liters > 0`
      estricto, con mensaje explicativo.
      Evidencia: `clients/field-app/src/services/milkingService.ts:299-309` — lanza si
      `liters < 0` y también si `liters === 0` ("0 litros no es un ordeño...").
- [x] **T-3.5a.0-2** `BirthScreen.tsx:33` gana quitar y editar cría, no sólo `addCalf`.
      Evidencia: `clients/field-app/src/screens/BirthScreen.tsx:33` (`addCalf`), `:42`
      (`removeCalf`), `:45-50` (`toggleCalfSex`), `:52-59` (`setCalfFarmTag`).
- [x] **T-3.5a.0-3** Pantalla de resumen antes de confirmar el parto (madre, padre, conteo
      por sexo, lista de crías).
      Evidencia: `BirthScreen.tsx:144-153` (Card con madre/padre/conteo por sexo vía
      `offspring-breakdown`), `:164-209` (lista de crías), `:225-231` (botón
      `confirm-birth`). **Nota**: no es una pantalla separada de confirmación — es un
      resumen persistente en la misma vista, mostrado antes de tocar "Registrar parto".
      Cumple la intención funcional, no la forma literal de "pantalla". El plan
      `docs/planes/field-app-parto-redesign/` rediseña `BirthScreen` como un asistente de
      cuatro pasos, lo que confirma que la versión actual todavía no lo es.
- [x] **T-3.5a.0-4** Fijar en código el principio "tres toques para lo normal, cuatro para
      lo raro".
      Evidencia: comentarios explícitos en `milkingService.ts:22,279`,
      `MilkingScreen.tsx:39`, `VaccinateScreen.tsx:26,29`, `EventsScreen.tsx:53`,
      `navigation.ts:17`.

### 3.5a.1 — `feature/livestock-group-events` (ADR-0015)

- [x] **T-3.5a.1-1** `AnimalGroup.TrackingMode` ∈ {`Individual`, `Headcount`}.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/TrackingMode.cs:14`,
      `AnimalGroup.cs:17`.
- [x] **T-3.5a.1-2** `AnimalEvent.AnimalId` XOR `GroupId`, con CHECK en BD.
      Evidencia: `AnimalEventConfiguration.cs:16-20` (`HasCheckConstraint`),
      `LivestockDbContextModelSnapshot.cs:372` (`CK_AnimalEvent_AnimalXorGroup`).
- [ ] **T-3.5a.1-3** Tipos `GroupWeighing`, `GroupMortality`, `GroupDiagnosis`;
      `EventType.Vaccination` pasa a emitirse. **NO tal como está redactado.**
      `src/Modules/Livestock/Hato.Modules.Livestock.Domain/EventEnums.cs:6-25` no define
      `GroupWeighing`/`GroupMortality`/`GroupDiagnosis`. El diseño real unifica el tipo:
      `AnimalEvent.cs:11-14` documenta explícitamente que "vaccinated this animal" y
      "vaccinated this lot" son el mismo `EventType` con sujeto distinto (`GroupId` en vez
      de `AnimalId`), no dos tipos. Solo la segunda mitad de la tarea —
      `EventType.Vaccination` emitido para grupos — está confirmada
      (`GetAnimalGroupQueries.cs:194`). Ver nota de diseño en `spec-3.5a.md`.
- [x] **T-3.5a.1-4** `LiveHeadCount` como consulta derivada, nunca contador editable.
      Evidencia: `LiveHeadCountCalculator.cs:10-18` (`Compute(activeMemberships, disposed)`,
      método estático puro, sin persistencia de un contador).
- [x] **T-3.5a.1-5** Cierre en cascada del lote al llegar a cero cabezas.
      Evidencia: `Animal.cs:152-155` (`CloseViaLotDisposal`), llamado desde
      `RecordGroupEventCommand.cs:148-151` (`animal.CloseViaLotDisposal(occurredAt)` sobre
      todos los `closedAnimalIds`).
- [x] **T-3.5a.1-6** `ResolveIndividualState(animalId, asOf)` en una sola función.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Application/Animals/ResolveIndividualStateQuery.cs`.
- [x] **T-3.5a.1-7** Endpoints de eventos grupales + `recordGroupEvent` en
      `PushSyncCommands.cs`.
      Evidencia: `src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs:16-18`
      (`POST /{id}/events`), `src/Hato.Api/Sync/PushSyncCommands.cs:240`
      (`Deserialize<RecordGroupEventCommand>`), `clients/field-app/src/services/eventService.ts:261`
      (`recordGroupEvent`).
- [x] **T-3.5a.1-8** Migración EF Core + reflejo en `SyncPullQueries` y esquema local móvil.
      Evidencia: migración
      `20260806191243_AddAnimalGroupTrackingModeAndGroupEvents.cs`;
      `src/Hato.Api/Sync/SyncPullQueries.cs:32,79` (`SyncAnimalGroupDto`);
      `clients/field-app/src/database/schema.ts:62` y `models.ts:47` (`tracking_mode`).

### 3.5a.2 — `feature/livestock-treatment-detail` (split A/B/C)

- [x] **T-3.5a.2-A1** Catálogo `administration_routes`, ampliable desde el panel.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/AdministrationRoute.cs`;
      `clients/admin-web/src/app/services/api.service.ts:690-708`
      (`getAdministrationRoutes`, `createAdministrationRoute`, `activate/deactivate`,
      `updateAdministrationRouteLabel`).
- [x] **T-3.5a.2-A2** `TreatmentReason` ∈ {`Scheduled`, `Curative`, `Preventive`}.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/TreatmentReason.cs`
      (catálogo, no enum C#) + `AnimalEvent.cs:22-27`
      (`KnownTreatmentReasons = {"scheduled","curative","preventive"}`).
- [x] **T-3.5a.2-A3** Payload: `route_id`, `reason`, `batch_id`, `applied_by` ≠ `recorded_by`.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Application/Events/RecordAnimalEventCommand.cs:11-29`
      (`RouteId`, `Reason`, `BatchId`, `RecordedById`, `AppliedByUserId` como parámetros
      distintos).
- [x] **T-3.5a.2-A8** `health_plan_item_id` nullable desde ya (forward-compat 3.5b.1).
      Evidencia: `AnimalEvent.cs:82-89` (`HealthPlanItemId` nullable, comentario explícito
      de forward-compat).
- [x] **T-3.5a.2-B4** Tres formas de dosis: Absoluta, Por peso, Por cabeza.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/DoseKind.cs:6-12`
      (`absolute`, `per_weight`, `per_head` documentados; tabla, no enum).
- [x] **T-3.5a.2-B5** Dosis calculada y administrada se guardan las dos.
      Evidencia: `TreatmentCourseApplication.cs:24-34`
      (`CalculatedDoseAmount`/`CalculatedDoseUnit` y `AdministeredDoseAmount`/
      `AdministeredDoseUnit`, comentario: "never reconciled").
- [x] **T-3.5a.2-B6** Dosis opcional; si está, lleva unidad (Art. 10).
      Evidencia: `TreatmentCourseApplication.cs:105-129` (`ValidateDoseCoupling`).
- [x] **T-3.5a.2-B7** Campo de observación libre (`TreatmentNotes`).
      Evidencia: `TreatmentCourseApplication.cs:40` (`public string? Notes`). Nombre real del
      campo es `Notes`, no `TreatmentNotes` — mismo propósito, nombre distinto.
- [x] **T-3.5a.2-B9** `TreatmentCourse`: serie de días con un único retiro.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/TreatmentCourse.cs:6-9`
      (comentario: "modelled as **one** series... withdrawal period is computed from
      Applications's last AppliedAt, never per loose application");
      `WithdrawalPeriod.cs:23` (`TreatmentCourseId`).
- [x] **T-3.5a.2-C10** Vacunación como camino propio, separado de tratamiento.
      Evidencia: `clients/field-app/src/screens/VaccinateScreen.tsx` y
      `clients/field-app/src/screens/TreatScreen.tsx` son pantallas distintas.
- [x] **T-3.5a.2-C11** Pantalla de campo: vía y motivo sin sumar toques al caso normal.
      Evidencia: `TreatScreen.tsx:41` (comentario "Route and reason default to sensible
      values"), `:64-85` (`routeId`/`reasonKey` con default automático).

### 3.5a.3 — `feature/livestock-mortality-causes`

- [x] **T-3.5a.3-1** Catálogo `mortality_causes`, ampliable desde el panel.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/MortalityCause.cs`;
      `clients/admin-web/src/app/components/catalogs/catalogs.component.ts` (gestiona
      mortality causes).
- [x] **T-3.5a.3-2** `DisposalType.Death` y `GroupMortality` ganan `cause_id`.
      Evidencia: `AnimalEvent.cs:55` (`CauseId`, comentario cita explícitamente
      "DisposalType.Death gana cause_id, y GroupMortality lo lleva también").
- [x] **T-3.5a.3-3** Registro de baja de lechón desde el móvil, madre resuelta
      automáticamente.
      Evidencia: `clients/field-app/src/services/eventService.ts:287-313` (`recordDisposal`,
      comentario "The mother is resolved by the caller from the already-synced herd
      (Animal.motherId)"); `clients/field-app/src/screens/EventsScreen.tsx:309-311`
      (muestra `Madre: ...` resuelta de `animal.motherId` sin que el operador la escriba).

### 3.5a.4 — `feature/breeding-nursing-cohort` (estructural)

- [x] **T-3.5a.4-1** `NursingCohort`: agrupa camadas nacidas en días consecutivos.
      Evidencia: `src/Modules/Breeding/Hato.Modules.Breeding.Domain/NursingCohort.cs`.
- [x] **T-3.5a.4-2** `weaning_date` derivada, días de lactancia configurables por especie.
      Evidencia: `NursingCohort.cs:11` (`weaning_date = max(birth_date) + days_of_lactation`),
      `:70` (`ComputeWeaningDate`); `Species.cs:33` (`DaysOfLactation` por especie).
- [x] **T-3.5a.4-3** Peso al nacer por lechón en el flujo de parto.
      Evidencia: `src/Modules/Breeding/Hato.Modules.Breeding.Application/Birthings/RecordBirthingCommand.cs:120-138`
      (`BirthWeightKg` propagado); `clients/field-app/src/screens/BirthScreen.tsx:68-87`
      (`setCalfWeight`).
- [x] **T-3.5a.4-4** Operación clasificación por peso (reparte cohorte en lotes `Headcount`).
      Evidencia: `src/Modules/Breeding/Hato.Modules.Breeding.Application/Cohorts/ClassifyCohortByWeightCommand.cs`.
- [x] **T-3.5a.4-5** `Birthing.RecordWeaning` integrado con la cohorte, sin duplicarse.
      Evidencia: `src/Modules/Breeding/Hato.Modules.Breeding.Application/Cohorts/RecordCohortWeaningCommand.cs:82-104`
      (llama a `birthing.RecordWeaning(...)` por cada parto y luego
      `cohort.RecordWeaning(weaningDate, totalWeaned, ...)`).

### 3.5a.5 — `feature/inventory-unit-conversions`

- [x] **T-3.5a.5-1** `unit_conversions {item_id, from_unit, to_unit, factor}`.
      Evidencia: `src/Modules/Inventory/Hato.Modules.Inventory.Domain/UnitConversion.cs:8-46`.
- [x] **T-3.5a.5-2** `GroupFeedConsumption` guarda registrado y base.
      Evidencia: `src/Modules/Inventory/Hato.Modules.Inventory.Infrastructure/Persistence/Migrations/20260807133639_AddFeedConsumptionRecordedQuantities.cs`
      + `GroupFeedConsumption.cs`.
- [x] **T-3.5a.5-3** `feed_stage` en ítems de categoría `Feed`.
      Evidencia: `src/Modules/Inventory/Hato.Modules.Inventory.Domain/FeedStage.cs:24-48`.
- [x] **T-3.5a.5-4** Registro de consumo por lote desde el móvil, en sacos.
      Evidencia: `clients/field-app/src/screens/LotEventsScreen.tsx` (actividad `'feed'`).

### 3.5a.6 — `feature/livestock-plausibility-ranges`

- [x] **T-3.5a.6-1** Rangos configurables por especie/categoría: plausible y absoluto, para
      peso y litros.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/PlausibilityRange.cs:19-35`.
- [x] **T-3.5a.6-2** Sincronizados al móvil, evaluados localmente (Art. 9).
      Evidencia: `src/Hato.Api/Sync/SyncPullQueries.cs:47,279,521-522`
      (`SyncPlausibilityRangeDto`); `clients/field-app/src/services/plausibilityService.ts`.
- [x] **T-3.5a.6-3** UI de confirmación para valor improbable.
      Evidencia: `clients/field-app/src/screens/MilkingScreen.tsx:86-98`,
      `TreatScreen.tsx:134-176`, `LotEventsScreen.tsx:132-143` (todos con
      `isPlausibilityConfirmed`).
- [x] **T-3.5a.6-4** Semilla con valores razonables porcino/bovino.
      Evidencia: migración `20260808053500_SeedPlausibilityRanges.cs`.
- [x] **T-3.5a.6-5** Fail-open verificado: sin rango configurado, no bloquea nada.
      Evidencia: `PlausibilityRange.cs:15-17` (comentario ADR-0022 sec.3 fail-open);
      prueba `clients/field-app/tests/plausibilityService.test.ts`.

### 3.5a.7 — `feature/field-app-lot-registration`

- [x] **T-3.5a.7-1** Pesaje muestral del lote (cuántos, pesos, promedio calculado).
      Evidencia: `clients/field-app/src/screens/LotEventsScreen.tsx:133-134`
      (`sampleCount`, `avgKg`), `:305` (`activity === 'weighing'`).
- [x] **T-3.5a.7-2** Baja del lote con causa y cantidad.
      Evidencia: `LotEventsScreen.tsx:345` (`activity === 'disposal'`).
- [x] **T-3.5a.7-3** Vacunación/tratamiento de lote completo.
      Evidencia: `LotEventsScreen.tsx:382-414` (`activity === 'vaccination' || 'treatment'`).
- [x] **T-3.5a.7-4** Diagnóstico grupal ("hay uno enfermo"), cantidad + observación.
      Evidencia: `LotEventsScreen.tsx:420` (`activity === 'diagnosis'`).
- [x] **T-3.5a.7-5** Consumo de alimento del lote en sacos.
      Evidencia: `LotEventsScreen.tsx:112,444` (`activity === 'feed'`).
- [ ] **T-3.5a.7-6** Ficha del lote (cabezas vivas, peso promedio, última vacunación,
      enfermos, alimento del período). **Parcial, no completo (2 de 5 datos).**
      `src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/GetAnimalGroupQueries.cs:145-208`
      (`AnimalGroupSummaryDto`) solo calcula `liveHeadCount`,
      `headsAffectedByDiagnosis`, `lastVaccinationAt`, `lastDisposalAt`, `lastTreatmentAt` —
      no hay **peso promedio** ni **alimento del período** en ningún lado del backend.
      Y del lado móvil, `clients/field-app/src/screens/LotSubjectScreen.tsx:88-94` solo
      renderiza `liveHeadCount` y `headsAffectedByDiagnosis`; `lastVaccinationAt` existe en
      el DTO (`clients/field-app/src/services/animalGroupsApi.ts:5`) pero no se muestra en
      pantalla. Ver `docs/BACKLOG.md`.

### 3.5a.8 — `feature/field-app-corrections` (ADR-0017)

- [x] **T-3.5a.8-1** `OutboxStatus` gana `cancelled`, con migración local.
      Evidencia: `clients/field-app/src/services/outbox.ts:6`
      (`'pending' | 'synced' | 'rejected' | 'cancelled'`).
- [x] **T-3.5a.8-2** Camino A: cancelar entrada aún no sincronizada.
      Evidencia: `outbox.ts:159-200` (`cancelPending`).
- [x] **T-3.5a.8-3** Camino B: evento `Correction` con `RelatedEventId` desde `result_ref`.
      Evidencia: `clients/field-app/src/services/eventService.ts:334-350`
      (`recordCorrection`, comentario "the original event id is the server's id (the
      resultRef...)"). **Limitación real** (no invalida la marca, pero limita su alcance):
      `clients/field-app/src/screens/TodayScreen.tsx:159-167` — solo
      `recordAnimalEvent` es corregible desde el teléfono hoy; `recordBirth` y
      `recordMilking` quedan explícitamente diferidos a 3.5b según el comentario in situ.
- [x] **T-3.5a.8-4** Transición `pending → cancelled` atómica; cae a camino B sin preguntar
      si ya no es `pending`.
      Evidencia: `outbox.ts:159-200` (`cancelPending` retorna
      `'alreadySynced'`/`'alreadyRejected'` cuando la fila ya no es `pending`, permitiendo
      que el llamador (`TodayScreen.tsx`) recurra al camino B).
- [x] **T-3.5a.8-5** Pantalla "lo que registré hoy": lista del día con estado de sync y
      acción de corregir.
      Evidencia: `clients/field-app/src/screens/TodayScreen.tsx:140-153` (título "Lo que
      registré hoy"), `:181-194` (botones de corregir/cancelar por fila).
- [x] **T-3.5a.8-6** Ventana: mismo día calendario para el registrador; sin límite para
      admin.
      Evidencia:
      `src/Modules/Livestock/Hato.Modules.Livestock.Application/Events/RecordCorrectionCommand.cs:17,58-68`
      (comentario "same calendar day for the field operator (ADMIN-FROM-PANEL has no...)" +
      chequeo de ventana).

### 3.5a.9 — `feature/field-app-herd-navigation` (split A/B)

- [x] **T-3.5a.9-B1** Navegación según el árbol de `spec.md` sec. 2.3, primer nivel =
      sujeto.
      Evidencia: `clients/field-app/src/screens/navigation.ts:9-24`
      (`'animal-subject'`, `'lot-subject'` como primer nivel).
- [ ] **T-3.5a.9-B2** Búsqueda por identificador y filtro por lote en el selector.
      **Parcial.** Búsqueda confirmada:
      `clients/field-app/src/screens/AnimalSubjectScreen.tsx:59-69` (`needle`, filtra por
      `label`). **Filtro por lote no encontrado** en `AnimalSubjectScreen.tsx` ni en
      `EventsScreen.tsx` — no hay ningún control que filtre el picker de animales por
      `groupId`.
- [x] **T-3.5a.9-B3** "Recientes": últimos animales sobre los que este teléfono registró
      algo.
      Evidencia: `AnimalSubjectScreen.tsx:40,59` (`recentIds`, comentario "show recent
      first").
- [x] **T-3.5a.9-A4** Ocultar Ordeño por interruptor explícito, `FarmModule { key, enabled,
      disabled_reason }`.
      Evidencia: `src/Modules/People/Hato.Modules.People.Domain/FarmModule.cs:22-24,51-62`.
- [x] **T-3.5a.9-A4a** No se borra nada (Production, MilkingScreen, endpoints, pruebas).
      Evidencia: `src/Modules/Production/` intacto (Domain/Application/Infrastructure/
      Contracts); `clients/field-app/src/screens/MilkingScreen.tsx` presente sin cambios
      estructurales de baja.
- [x] **T-3.5a.9-A4b** Se oculta la entrada, jamás el camino de los datos.
      Evidencia: `clients/field-app/src/services/moduleVisibility.ts` controla solo
      visibilidad de navegación, no borra tablas ni endpoints.
- [x] **T-3.5a.9-A4c** `Species.IsMilkable` no se elimina.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/Species.cs:23`.
- [x] **T-3.5a.9-A4d** Filtrado evaluado sin red (Art. 9).
      Evidencia: `clients/field-app/src/services/moduleVisibility.ts:43,70`
      (`this.database.get<FarmModule>('farm_modules')` — lectura local WatermelonDB, no
      `fetch`); `App.tsx:129-148` (`productionVisible` viene de la lectura local, no de una
      llamada de red en el camino crítico).
- [x] **T-3.5a.9-B5** Documentar escaneo QR como paso siguiente natural (ticket en
      `docs/BACKLOG.md`, no trabajo de esta fase).
      Evidencia: `docs/BACKLOG.md:416` ("Escaneo QR y carnetización desde el nacimiento.").

---

## Bloque 3.5b — Análisis y automatización

### 4.1 — `feature/livestock-health-plans` (ADR-0016, estructural)

- [x] **T-4.1-1** `HealthPlan` + `HealthPlanItem`: ancla, desfase, ventana, filtro
      especie/categoría/sexo.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/HealthPlanItem.cs:36-66`
      (`Anchor`, `AnchorOffsetDays`, `ComplianceWindowDays`, `AppliesToCategoryId`,
      `AppliesToSex`).
- [x] **T-4.1-2** Asignable a lote o individuo.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Migrations/20260808055359_AddHealthPlans.cs:61`
      (`CK_HealthPlanAssignment_AnimalXorGroup`).
- [x] **T-4.1-3** Cumplimiento es el evento (Art. 4), sin botón "marcar como hecho".
      Evidencia: ningún método `MarkDone`/`MarkAsDone` en `HealthPlan*.cs` ni en
      `clients/admin-web/src/app` (búsqueda negativa verificada).

### 4.2 — `feature/tasks-health-plan-alerts`

- [ ] **T-4.2-1** Generador `HEALTH_PLAN_ITEM_DUE` en `GenerateAlertsCommand.cs`.
      **No existe.** `src/Modules/Tasks/Hato.Modules.Tasks.Application/Alerts/GenerateAlertsCommand.cs`
      (138 líneas) solo genera `UPCOMING_BIRTH`, `WITHDRAWAL_PERIOD_ACTIVE`,
      `PENDING_PREGNANCY_CHECK`, `INVENTORY_BATCH_EXPIRING`. Cero menciones de
      `HEALTH_PLAN_ITEM_DUE` en todo el repo. No existe rama ni commit alguno para
      `feature/tasks-health-plan-alerts`.
- [ ] **T-4.2-2** `AlertAlreadyActiveAsync` para no duplicar.
      El método genérico existe (`GenerateAlertsCommand.cs:136`) pero nunca se invoca para
      `HEALTH_PLAN_ITEM_DUE` porque ese generador no existe.

### 4.3 — `feature/inventory-feeding-standards`

- [ ] **T-4.3-1** Tabla `{especie, etapa, peso_desde, peso_hasta, ración_kg_día}`.
      No se encontró ninguna clase, migración ni tabla relacionada con estándares de
      alimentación en `src/Modules/Inventory`.
- [ ] **T-4.3-2** Ración de cerda lactante con tope (`base_kg`, `por_cría_kg`, `max_kg`).
      No encontrado.
- [ ] **T-4.3-3** Salida en la app con cálculo de sacos recomendados.
      No encontrado en `clients/field-app/src`.

### 4.4 — `feature/analytics-lot-fcr`

- [ ] **T-4.4-1** Cálculo de FCR por lote (kg alimento ÷ kg ganados).
      No se encontró ninguna clase/consulta con "FCR" ni "conversión alimenticia" en
      ningún módulo backend.
- [ ] **T-4.4-2** Alerta de divergencia contra el estándar, umbral configurable.
      No encontrado (depende de 4.3/4.4-1, que tampoco existen).

### 4.5 — `feature/livestock-animal-traits` (ADR-0018, split A/B/C)

- [ ] **T-3.5b.5-A1** `AnimalTrait` + `TraitObservation`, `kind` ∈ {Conductual,
      Morfológica, Manejo}.
      No existe ningún archivo `AnimalTrait.cs` / `TraitObservation.cs` en el repo.
- [ ] **T-3.5b.5-A2** "Se observan, no se asignan": `CurrentDisposition` derivado.
      No encontrado.
- [ ] **T-3.5b.5-A3** `contexto` opcional.
      No encontrado (depende de A1).
- [ ] **T-3.5b.5-A4** Cuatro tipos de valor, sin unidad ni decimal libre.
      No encontrado.
- [ ] **T-3.5b.5-B5** Absorbe `SelectionCriterion`, drena `MaternalBehaviorAssessment`.
      `SelectionCriterion`/`MaternalBehaviorAssessment` no aparecen en el código actual —
      no se puede verificar una migración que no tiene origen ni destino visibles.
- [ ] **T-3.5b.5-C6** `visible_como_advertencia` en la ficha del animal.
      No encontrado.
- [ ] **T-3.5b.5-C7** Versionado por clonado de definiciones usadas (ADR-0018 sec. 9).
      No encontrado.

### 4.6 — `feature/breeding-maternal-index`

- [ ] **T-4.6-1** KPIs derivados de eventos contables (no almacenados).
      No encontrado ningún cálculo de KPI de maternidad en `src/Modules/Breeding`.
- [ ] **T-4.6-2** `MaternalIndex` con pesos configurables, ordenable en el panel.
      No existe clase `MaternalIndex` en el repo.

### 4.7 — `feature/tasks-swine-alerts`

- [ ] **T-4.7-1** Alerta destete → celo (4–7 días).
      No encontrada en `src/Modules/Tasks`.
- [ ] **T-4.7-2** Retiro en carne bloqueante (`WithdrawalTarget.Meat`), rechaza no advierte.
      `WithdrawalTarget.Meat` existe como valor de enum y se usa para **crear** el período de
      retiro (`RecordAnimalEventCommand.cs:178-182`), pero no se encontró ninguna
      validación que **bloquee** una venta/disposición durante un retiro de carne activo —
      ni en Livestock ni en Tasks.

---

## Cierre

- [ ] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo (partes V y E2E).
      No verificable como tarea de código — es un procedimiento manual. No hay evidencia de
      ejecución (ni reporte, ni artefacto de resultados) dentro del repo para
      `docs/planes/fase-3-5/test-e2e.md`.
- [ ] **TC.2** Verificar contra `docs/BACKLOG.md` sección 3.5 que la deuda declarada en
      `plan.md` sec. 3 sigue anotada allí.
      No verificable por código — es una tarea de auditoría documental cruzada, no una
      implementación.
