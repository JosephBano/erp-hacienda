# tasks.md — Desglose ejecutable de la Fase 3.5

> Checklist de las ~17 ramas de la Fase 3.5. Cada tarea cita su sección de origen en
> [`spec.md`](./spec.md) / [`spec-3.5a.md`](./spec-3.5a.md) para que quien la marque pueda
> volver al porqué. **Todas las casillas quedan `[ ]`** en este traslado — las marca el
> agente de la compuerta A contra el código real, no este commit.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Bloque 3.5a — Captura

### 3.5a.0 — `feature/field-app-input-guards`

- [ ] **T-3.5a.0-1** Corregir `assertVolume` (`milkingService.ts:247`) a `liters > 0`
      estricto, con mensaje explicativo.
- [ ] **T-3.5a.0-2** `BirthScreen.tsx:33` gana quitar y editar cría, no sólo `addCalf`.
- [ ] **T-3.5a.0-3** Pantalla de resumen antes de confirmar el parto (madre, padre, conteo
      por sexo, lista de crías).
- [ ] **T-3.5a.0-4** Fijar en código el principio "tres toques para lo normal, cuatro para
      lo raro".

### 3.5a.1 — `feature/livestock-group-events` (ADR-0015)

- [ ] **T-3.5a.1-1** `AnimalGroup.TrackingMode` ∈ {`Individual`, `Headcount`}.
- [ ] **T-3.5a.1-2** `AnimalEvent.AnimalId` XOR `GroupId`, con CHECK en BD.
- [ ] **T-3.5a.1-3** Tipos `GroupWeighing`, `GroupMortality`, `GroupDiagnosis`;
      `EventType.Vaccination` pasa a emitirse.
- [ ] **T-3.5a.1-4** `LiveHeadCount` como consulta derivada, nunca contador editable.
- [ ] **T-3.5a.1-5** Cierre en cascada del lote al llegar a cero cabezas.
- [ ] **T-3.5a.1-6** `ResolveIndividualState(animalId, asOf)` en una sola función.
- [ ] **T-3.5a.1-7** Endpoints de eventos grupales + `recordGroupEvent` en
      `PushSyncCommands.cs`.
- [ ] **T-3.5a.1-8** Migración EF Core + reflejo en `SyncPullQueries` y esquema local móvil.

### 3.5a.2 — `feature/livestock-treatment-detail` (split A/B/C)

- [ ] **T-3.5a.2-A1** Catálogo `administration_routes`, ampliable desde el panel.
- [ ] **T-3.5a.2-A2** `TreatmentReason` ∈ {`Scheduled`, `Curative`, `Preventive`}.
- [ ] **T-3.5a.2-A3** Payload: `route_id`, `reason`, `batch_id`, `applied_by` ≠ `recorded_by`.
- [ ] **T-3.5a.2-A8** `health_plan_item_id` nullable desde ya (forward-compat 3.5b.1).
- [ ] **T-3.5a.2-B4** Tres formas de dosis: Absoluta, Por peso, Por cabeza.
- [ ] **T-3.5a.2-B5** Dosis calculada y administrada se guardan las dos.
- [ ] **T-3.5a.2-B6** Dosis opcional; si está, lleva unidad (Art. 10).
- [ ] **T-3.5a.2-B7** Campo de observación libre (`TreatmentNotes`).
- [ ] **T-3.5a.2-B9** `TreatmentCourse`: serie de días con un único retiro.
- [ ] **T-3.5a.2-C10** Vacunación como camino propio, separado de tratamiento.
- [ ] **T-3.5a.2-C11** Pantalla de campo: vía y motivo sin sumar toques al caso normal.

### 3.5a.3 — `feature/livestock-mortality-causes`

- [ ] **T-3.5a.3-1** Catálogo `mortality_causes`, ampliable desde el panel.
- [ ] **T-3.5a.3-2** `DisposalType.Death` y `GroupMortality` ganan `cause_id`.
- [ ] **T-3.5a.3-3** Registro de baja de lechón desde el móvil, madre resuelta
      automáticamente.

### 3.5a.4 — `feature/breeding-nursing-cohort` (estructural)

- [ ] **T-3.5a.4-1** `NursingCohort`: agrupa camadas nacidas en días consecutivos.
- [ ] **T-3.5a.4-2** `weaning_date` derivada, días de lactancia configurables por especie.
- [ ] **T-3.5a.4-3** Peso al nacer por lechón en el flujo de parto.
- [ ] **T-3.5a.4-4** Operación clasificación por peso (reparte cohorte en lotes `Headcount`).
- [ ] **T-3.5a.4-5** `Birthing.RecordWeaning` integrado con la cohorte, sin duplicarse.

### 3.5a.5 — `feature/inventory-unit-conversions`

- [ ] **T-3.5a.5-1** `unit_conversions {item_id, from_unit, to_unit, factor}`.
- [ ] **T-3.5a.5-2** `GroupFeedConsumption` guarda registrado y base.
- [ ] **T-3.5a.5-3** `feed_stage` en ítems de categoría `Feed`.
- [ ] **T-3.5a.5-4** Registro de consumo por lote desde el móvil, en sacos.

### 3.5a.6 — `feature/livestock-plausibility-ranges`

- [ ] **T-3.5a.6-1** Rangos configurables por especie/categoría: plausible y absoluto, para
      peso y litros.
- [ ] **T-3.5a.6-2** Sincronizados al móvil, evaluados localmente (Art. 9).
- [ ] **T-3.5a.6-3** UI de confirmación para valor improbable.
- [ ] **T-3.5a.6-4** Semilla con valores razonables porcino/bovino.
- [ ] **T-3.5a.6-5** Fail-open verificado: sin rango configurado, no bloquea nada.

### 3.5a.7 — `feature/field-app-lot-registration`

- [ ] **T-3.5a.7-1** Pesaje muestral del lote (cuántos, pesos, promedio calculado).
- [ ] **T-3.5a.7-2** Baja del lote con causa y cantidad.
- [ ] **T-3.5a.7-3** Vacunación/tratamiento de lote completo.
- [ ] **T-3.5a.7-4** Diagnóstico grupal ("hay uno enfermo"), cantidad + observación.
- [ ] **T-3.5a.7-5** Consumo de alimento del lote en sacos.
- [ ] **T-3.5a.7-6** Ficha del lote (cabezas vivas, peso promedio, última vacunación,
      enfermos, alimento del período).

### 3.5a.8 — `feature/field-app-corrections` (ADR-0017)

- [ ] **T-3.5a.8-1** `OutboxStatus` gana `cancelled`, con migración local.
- [ ] **T-3.5a.8-2** Camino A: cancelar entrada aún no sincronizada.
- [ ] **T-3.5a.8-3** Camino B: evento `Correction` con `RelatedEventId` desde `result_ref`.
- [ ] **T-3.5a.8-4** Transición `pending → cancelled` atómica; cae a camino B sin preguntar
      si ya no es `pending`.
- [ ] **T-3.5a.8-5** Pantalla "lo que registré hoy": lista del día con estado de sync y
      acción de corregir.
- [ ] **T-3.5a.8-6** Ventana: mismo día calendario para el registrador; sin límite para
      admin.

### 3.5a.9 — `feature/field-app-herd-navigation` (split A/B)

- [ ] **T-3.5a.9-B1** Navegación según el árbol de `spec.md` sec. 2.3, primer nivel =
      sujeto.
- [ ] **T-3.5a.9-B2** Búsqueda por identificador y filtro por lote en el selector.
- [ ] **T-3.5a.9-B3** "Recientes": últimos animales sobre los que este teléfono registró
      algo.
- [ ] **T-3.5a.9-A4** Ocultar Ordeño por interruptor explícito, `FarmModule { key, enabled,
      disabled_reason }`.
- [ ] **T-3.5a.9-A4a** No se borra nada (Production, MilkingScreen, endpoints, pruebas).
- [ ] **T-3.5a.9-A4b** Se oculta la entrada, jamás el camino de los datos.
- [ ] **T-3.5a.9-A4c** `Species.IsMilkable` no se elimina.
- [ ] **T-3.5a.9-A4d** Filtrado evaluado sin red (Art. 9).
- [ ] **T-3.5a.9-B5** Documentar escaneo QR como paso siguiente natural (ticket en
      `BACKLOG.md`, no trabajo de esta fase).

---

## Bloque 3.5b — Análisis y automatización

### 4.1 — `feature/livestock-health-plans` (ADR-0016, estructural)

- [ ] **T-4.1-1** `HealthPlan` + `HealthPlanItem`: ancla, desfase, ventana, filtro
      especie/categoría/sexo.
- [ ] **T-4.1-2** Asignable a lote o individuo.
- [ ] **T-4.1-3** Cumplimiento es el evento (Art. 4), sin botón "marcar como hecho".

### 4.2 — `feature/tasks-health-plan-alerts`

- [ ] **T-4.2-1** Generador `HEALTH_PLAN_ITEM_DUE` en `GenerateAlertsCommand.cs`.
- [ ] **T-4.2-2** `AlertAlreadyActiveAsync` para no duplicar.

### 4.3 — `feature/inventory-feeding-standards`

- [ ] **T-4.3-1** Tabla `{especie, etapa, peso_desde, peso_hasta, ración_kg_día}`.
- [ ] **T-4.3-2** Ración de cerda lactante con tope (`base_kg`, `por_cría_kg`, `max_kg`).
- [ ] **T-4.3-3** Salida en la app con cálculo de sacos recomendados.

### 4.4 — `feature/analytics-lot-fcr`

- [ ] **T-4.4-1** Cálculo de FCR por lote (kg alimento ÷ kg ganados).
- [ ] **T-4.4-2** Alerta de divergencia contra el estándar, umbral configurable.

### 4.5 — `feature/livestock-animal-traits` (ADR-0018, split A/B/C)

- [ ] **T-3.5b.5-A1** `AnimalTrait` + `TraitObservation`, `kind` ∈ {Conductual,
      Morfológica, Manejo}.
- [ ] **T-3.5b.5-A2** "Se observan, no se asignan": `CurrentDisposition` derivado.
- [ ] **T-3.5b.5-A3** `contexto` opcional.
- [ ] **T-3.5b.5-A4** Cuatro tipos de valor, sin unidad ni decimal libre.
- [ ] **T-3.5b.5-B5** Absorbe `SelectionCriterion`, drena `MaternalBehaviorAssessment`.
- [ ] **T-3.5b.5-C6** `visible_como_advertencia` en la ficha del animal.
- [ ] **T-3.5b.5-C7** Versionado por clonado de definiciones usadas (ADR-0018 sec. 9).

### 4.6 — `feature/breeding-maternal-index`

- [ ] **T-4.6-1** KPIs derivados de eventos contables (no almacenados).
- [ ] **T-4.6-2** `MaternalIndex` con pesos configurables, ordenable en el panel.

### 4.7 — `feature/tasks-swine-alerts`

- [ ] **T-4.7-1** Alerta destete → celo (4–7 días).
- [ ] **T-4.7-2** Retiro en carne bloqueante (`WithdrawalTarget.Meat`), rechaza no advierte.

---

## Cierre

- [ ] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md) completo (partes V y E2E).
- [ ] **TC.2** Verificar contra `docs/BACKLOG.md` sección 3.5 que la deuda declarada en
      `plan.md` sec. 3 sigue anotada allí.
