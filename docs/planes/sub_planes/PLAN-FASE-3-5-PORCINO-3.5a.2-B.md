# PLAN-FASE-3-5-PORCINO-3.5a.2-B.md — Lógica de dosis y series de tratamiento

> **Sub-plan extraído de `PLAN-FASE-3-5-PORCINO.md` sec.3.5a.2.**
> Este archivo **es ejecutable de forma independiente** del macro plan y de sus
> pares 3.5a.2-A (catálogos y payload) y 3.5a.2-C (UI de campo). El macro plan
> sigue siendo la fuente de verdad para el resto del proyecto. Esta sub-rama
> tiene una **dependencia dura con 3.5a.2-A**: el payload del tratamiento ya
> debe tener los catálogos y campos definidos.

- **Rama Git:** `feature/livestock-treatment-dose-logic`
- **ADRs que la respaldan:** ninguno nuevo (las decisiones conviven con ADR-0016
  del cronograma y ADR-0015 del sujeto grupal — ninguno se reabre acá).
- **Pares del split:**
  - [`3.5a.2-A`](./PLAN-FASE-3-5-PORCINO-3.5a.2-A.md) (rama `feature/livestock-treatment-catalog`): los catálogos `administration_routes`, `TreatmentReason`, el payload, `health_plan_item_id` nullable, y `applied_by` ≠ `recorded_by`. **Esta sub-rama requiere A mergeado.**
  - [`3.5a.2-C`](./PLAN-FASE-3-5-PORCINO-3.5a.2-C.md) (rama `feature/field-app-treatment-ui`): la pantalla de campo que consume lo definido aquí.
- **Fuente original:** [`PLAN-FASE-3-5-PORCINO.md` sec.3.5a.2](../PLAN-FASE-3-5-PORCINO.md#35a2--featurelivestock-treatment-detail--estructural)

---

## Por qué existe esta sub-rama

Esta sub-rama existe porque el modelo de dosis del plan original tenía **tres
problemas independientes** que el mismo PR intentaba resolver al mismo tiempo,
y eso (a) violaba la regla "un PR = un propósito" del
`PLAN-FASE-3-4.md` sec.1.3, y (b) mezclaba tres órdenes de magnitud de cambio:

1. **Conceptual:** cómo se *expresa* una dosis en el nuevo modelo. Responder a
   "¿se dosifica por peso, por cabeza, o absoluto?" introduce un nuevo tipo en
   el dominio.
2. **De cálculo:** cómo se resuelve esa expresión contra datos reales
   (último pesaje del animal, promedio muestral del lote) y qué se guarda cuando
   lo que el operario escribió no coincide con el cálculo.
3. **De agrupación:** cómo se modela un tratamiento de varios días como **una**
   serie con aplicaciones y un único período de retiro, en vez de N eventos
   sueltos.

Cada uno de los tres merece su propia sub-rama; este archivo los contiene los
tres juntos porque comparten la **entidad** `Treatment` y la pantalla los
muestra juntos, pero podrían partirse más si la implementación revela que
escapan del ~1 semana de trabajo.

## Decisiones tomadas en el macro plan y que aplican a esta sub-rama

- sec.3.5a.2 puntos 4–7 (las tres formas, calculada vs. administrada, dosis
  opcional, observación libre): todo el contenido está aquí.
- sec.3.5a.2 punto 9 (`TreatmentCourse`): también.
- sec.3.5a.2 nota de tamaño: si la implementación de las tres formas cabe en un
  PR, esta sub-rama se ejecuta tal cual; si no, partir en B-1 (las tres formas)
  y B-2 (`TreatmentCourse`) antes de mergear, anotándolo en `docs/BACKLOG.md`.
- **Calculada ≠ administrada se persiste sin corregir ninguna.** Si el sistema
  sugirió 147 ml y se administraron 200, alguien derramó, alguien subdosificó,
  o el muestreo está mal — y el sistema debe mostrarlo, no decidir. El Art. 1
  (registro inmutable) gana sobre cualquier "smart default" tentador.

## Tareas

1. **Tipo `DoseKind` ∈ {`Absolute`, `PerWeight`, `PerHead`}.** Tabla, no enum
   (Art. 8). Para `PerWeight` se almacena la unidad *base* (`ml/kg`,
   `mg/kg`, etc.) que el operario selecciona de las unidades disponibles del
   producto. Si `route_id` no tiene una unidad esperada, se rechaza la forma.
2. **`CalculatedDose` y `AdministeredDose` como columnas separadas** (no
   nullable; ambas son `decimal` con unidad explícita, Art. 10):
   - `CalculatedDose`: lo que el sistema sugirió. Si `DoseKind = PerWeight`,
     se calcula multiplicando factor por el último pesaje del animal (o el
     promedio muestral del lote). Va con un flag `is_estimated: bool` que es
     `true` cuando se calculó contra un promedio muestral.
   - `AdministeredDose`: lo que el operario dice que salió del frasco. Si
     difiere de `CalculatedDose`, **se guarda el gap** como dato explotable
     más tarde (análisis de derrame, subdosificación, muestreo).
3. **Dosis opcional, no obligatoria.** Art. 10 exige que *toda cantidad lleve
   unidad*, no que *toda aplicación tenga cantidad*. Si `AdministeredDose` es
   `NULL`, se acepta el evento — pero siempre con `TreatmentNotes`
   (siguiente tarea) explicando la omisión. Un valor obligatorio produce un
   `5 ml` falso, que es peor que un texto honesto (Art. 1).
4. **`TreatmentNotes` (text) libre.** No es cajón de sastre: vive donde ningún
   esquema captura. *"se aplicó en el cuello porque la pierna estaba
   lastimada"*. La medida se estructura; la narrativa se libera. **Nunca**
   viaja por `dose` ni por ninguno de los otros campos tipados.
5. **`TreatmentCourse` como agregado nuevo.** Un tratamiento de 3 días es
   **una serie** con sus aplicaciones, no 3 eventos sueltos:
   ```
   TreatmentCourse
     id, animal_or_group_id, starts_at, ends_at?, route_id, reason,
     product_id, dose_kind, dose_factor (con unidad), notes?,
     ── applications (1..N)
          application_no, applied_at, calculated_dose?, administered_dose?, notes?,
   ```
   El período de retiro se calcula a partir de `applications[last].applied_at`,
   no por aplicación suelta.
6. **Resolución del cálculo contra el último pesaje:**
   - Si `DoseKind = PerWeight` y el animal tiene pesaje: se calcula.
   - Si `DoseKind = PerWeight` y el animal no tiene pesaje: **se rechaza** el
     envío, con un Problem Details (RFC 7807) explicando la causa. Sin
     pesaje no se puede dosificar por peso — el operario debe elegir `Absolute`
     o hacer un pesaje primero.
   - Si `DoseKind = PerWeight` y el sujeto es un grupo: se usa el último
     `GroupWeighing` y la dosis se marca `is_estimated = true`. La estimación
     se registra con una nota automática: *"calculado sobre muestreo de N
     cabezas"*.
7. **Migración:** nueva tabla `treatment_courses`, deprecación (sin borrado)
   del campo `dose` libre en `animal_events` para el evento `treatment` — el
   campo se queda mientras haya datos legacy; nuevos eventos sólo usan
   `TreatmentCourse`.
8. **Tests de dominio e integración** (detallados más abajo).
9. **Backward compat:** durante la transición, un `animal_events` con
   `treatment` y `dose` libre se acepta y se migra a un `TreatmentCourse`
   sintético de una sola aplicación al primer push que lo toque. La migración
   corre en una suite de idempotencia explícita.

## Pruebas

Las pruebas mínimas obligatorias:

1. **Dosis con valor y sin unidad rechazada.** `AdministeredDose = 10` sin
   unidad produce error de validación. `AdministeredDose = 10` con
   `unit = "ml"` se acepta.
2. **Dosis ausente es válida.** `AdministeredDose = NULL` con `notes`
   cualquiera se acepta. `AdministeredDose = NULL` sin `notes` se acepta
   también (Art. 1), pero la prueba capta la ruta.
3. **`PerWeight` con pesaje.** Sembrar animal con último pesaje de 200 kg,
   `DoseKind = PerWeight`, `factor = 1 ml/10 kg`. La `CalculatedDose`
   resultante es `20 ml` y `is_estimated = false`.
4. **`PerWeight` sin pesaje es rechazado.** Mismo escenario sin pesaje:
   Problem Details, código de error tipado. El operario tiene que cambiar a
   `Absolute` o hacer un pesaje.
5. **`PerWeight` sobre grupo usa promedio y marca estimado.** Sembrar un grupo
   con `GroupWeighing { sample_count: 10, avg_kg: 35 }`, total 42 cabezas.
   `DoseKind = PerWeight`, `factor = 1 ml/10 kg`. La `CalculatedDose`
   resultante es `147 ml` (42 × 35 ÷ 10), `is_estimated = true`, y la nota
   automática cita el muestreo.
6. **Calculada ≠ administrada se persiste sin corregir.** Sembrar
   `CalculatedDose = 147 ml`, `AdministeredDose = 200 ml`. La fila persiste
   con los dos valores y el gap queda como dato consultable.
7. **Ruta inexistente rechazada.** Un `route_id` que no está en
   `administration_routes` o que está `is_active = false` se rechaza con
   Problem Details.
8. **`TreatmentCourse` de 3 días.** Crear serie con 3 aplicaciones separadas
   por 24 h. El sistema produce:
   - Una fila en `treatment_courses` con `ends_at` apuntando a la última
     aplicación.
   - Tres filas en `treatment_course_applications`.
   - Un único período de retiro, fechado a partir de la última aplicación.
9. **No regresión de Art. 19.** El retiro sigue calculándose igual que antes
   para todos los tratamientos pre-existentes. La prueba toma una camada de
   registros legacy (evento con `dose` libre + `withdrawal_period_days`) y
   verifica que su retiro sigue activo donde corresponde.
10. **Backward-compat del push.** Un dispositivo con un evento `treatment` con
    `dose` libre en el `SyncOutbox` se sincroniza, crea un
    `TreatmentCourse` sintético de una aplicación, y la fila original queda
    marcada con `migrated_to_course_id`. Reintentarlo no duplica (idempotencia
    explícita en la migración).

Adicional recomendado:

- Tabla con 20+ combinaciones de `(DoseKind, has_weighing, animal_or_group)`
  para cubrir el espacio.
- Tiempo de cálculo bajo (un PR con tiempos inaceptables aquí se rechaza).

## Lo que NO incluye (queda para 3.5a.2-C)

- La **UI de campo** y la separación tratamiento/vacunación en la app → va en
  [`3.5a.2-C`](./PLAN-FASE-3-5-PORCINO-3.5a.2-C.md).
- Análisis del gap calculada vs. administrada — eso es la alerta de cobertura
  del FCR (sec.3.5b.4 del macro plan, mucho más adelante). Acá **sólo** se guarda
  el dato; nadie lo lee todavía.

## Cómo probarlo

```bash
git fetch origin
git switch feature/livestock-treatment-dose-logic
dotnet test --configuration Release
dotnet ef database update --project src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure --startup-project src/Hato.Api
```

Para el flujo manual:

```bash
# Sembrar pesaje
psql -d hato -c "INSERT INTO weighings (animal_id, kg, occurred_at) VALUES (...), (...);"
# Crear tratamiento con PerWeight vía API
curl -X POST http://localhost:5000/api/v1/animals/.../events \
  -H 'Content-Type: application/json' \
  -d '{"type":"treatment","kind":"PerWeight","factor":0.1,"unit":"ml","route_id":"...","reason":"curative"}'
# Verificar la fila creada y el flag is_estimated.
```

## Riesgos específicos de esta sub-rama

| Riesgo | Mitigación |
|---|---|
| El cálculo por peso produce cifras absurdas (decimal mal redondeado) | Suite de cálculo con tabla de pares pesaje × factor y comparación manual contra una hoja de cálculo. |
| Se rechazan demasiados eventos por falta de pesaje | La app ofrece un fallback "Absolute" explícito antes de bloquear; documentado en 3.5a.2-C. |
| `TreatmentCourse` rompe consumidores que asumen evento plano | Ver sec.3.5a.2-A punto 3 — `applied_by` ≠ `recorded_by` se modeló para no encadenar los cambios. Los consumidores que hoy leen `AnimalEvent` siguen funcionando; el `TreatmentCourse` expone una vista derivada. |
| Migración de legacy duplica filas | Test #10 explícito + idempotencia única en la columna `migrated_to_course_id`. |
