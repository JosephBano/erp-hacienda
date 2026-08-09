# ADR-0023 — Eventos de clasificación por peso: una huella individual, una huella grupal

- **Estado:** Aceptado
- **Fecha:** 2026-08-07
- **Aceptado en rama:** `feature/breeding-weight-classification` (3.5a.4 task 4) — pendiente de merge
- **Fase del roadmap:** Fase 3.5 — Adaptación porcina — bloque 3.5a
- **Decisiones que respeta:** ADR-0015 (lote por conteo, evento grupal XOR), Constitución Art. 1 (inmutabilidad), Constitución Art. 4 (todo cambio es un evento)

## Contexto

La tarea 4 de 3.5a.4 (`PLAN-FASE-3-5-PORCINO.md` sec.3.5a.4) define la **clasificación por peso** como la operación que reparte una cohorte de lactancia destetada en N lotes de engorde en modo `Headcount`. Es el momento en que **termina la identificación individual** de los animales — el último instante en que el sistema puede afirmar con certeza "este animal específico entró a este lote específico".

Tres tensiones concretas que este ADR tiene que resolver:

1. **Animal vs. grupo (XOR de ADR-0015 sec.2).** `AnimalEvent` exige exactamente uno de los dos como `SubjectId`. La clasificación es por definición *sobre la frontera*: por última vez sabemos el sujeto individual, pero al mismo tiempo estamos creando el grupo `Headcount` al que se asigna. Emitir **solo** eventos individuales pierde el hecho "se mezclaron N cabezas en el lote X"; emitir **solo** eventos grupales pierde la genealogía fina del último instante.

2. **El linaje individual se preserva solo** (decisión ya tomada en 3.5a.1 + 3.5a.4 tasks 1–3): `Animal.MotherId`, `Animal.FatherAnimalId`, `Animal.FatherStrawId`, `Animal.BirthingId` **no se tocan**. La pregunta que este ADR responde no es si se preservan — se preservan — sino **cómo se registra el evento** de la transición.

3. **El contador global de "cabezas vivas"** ya se resuelve por la combinación de membresías activas del grupo `Headcount` menos las bajas (`ADR-0015 sec.7`, `NursingCohort` y `AnimalGroup.CloseAllActiveMemberships` ya implementados). La operación de clasificación no necesita inventar un contador nuevo: **mueve crías de "sueltas en su cohorte" a "miembros de un grupo `Headcount`"** y emite la huella del momento.

4. **El sync móvil** debe poder registrar el hecho desde el campo si el operario lo tipea en el teléfono el día de la mezcla. Hoy el push ya soporta `recordAnimalEvent` (individual) y `recordGroupEvent` (grupal). La rama de 3.5a.4 task 4 cubre el path del servidor (panel / móvil con señal); la integración con el outbox queda para 3.5a.7.

## Decisión

### 1. Dos nuevos `EventType`, no uno solo

`EventType` (en `src/Modules/Livestock/Hato.Modules.Livestock.Domain/EventEnums.cs`) gana **dos valores**, persistidos como string (`HasConversion<string>`, `MaxLength(30)` ya existente):

| Nuevo valor | Subject | Payload mínimo | Semántica |
|---|---|---|---|
| `WeightSorted` | `AnimalId` | `{ target_group_id, weight_kg }` | Última huella individual del animal — el sistema sabe exactamente qué cría va a qué lote y con qué peso. |
| `GroupWeightSorting` | `GroupId` | `{ source_cohort_id, head_count, avg_weight_kg, min_weight_kg, max_weight_kg }` | Huella grupal del momento en que el lote recibe N cabezas mezcladas. Anonimiza desde acá. |

**Regla del XOR se respeta por construcción**: cada evento individual tiene `AnimalId` no nulo y `GroupId` nulo; cada evento grupal tiene `GroupId` no nulo y `AnimalId` nulo. El check constraint `CK_AnimalEvent_AnimalXorGroup` (ya existente en BD, `AnimalEventConfiguration.cs:13-16`) lo garantiza.

### 2. La operación emite N+1 eventos, no 2N ni 2

Para una clasificación que reparte 90 crías en 3 lotes (A=30, B=35, C=25):

- **90 eventos `WeightSorted`** — uno por cría, con `target_group_id` y `weight_kg`.
- **3 eventos `GroupWeightSorting`** — uno por lote destino, con `head_count`, pesos agregados del subconjunto.

**Total: 93 filas de evento**, no 180 ni 6. La cantidad es deliberada y testeada: si una cría falta, hay una fila de menos en `WeightSorted`; si un grupo no recibe la huella grupal, falta una fila en `GroupWeightSorting`. Cada omisión es detectable.

**Por qué no un solo evento grupal que diga "90 cabezas al lote A, 35 al B, 25 al C"**: porque perderíamos la huella individual. El sistema no podría responder tres meses después "esta cerda de reemplazo, ¿de qué camada vino y qué lote la recibió?". Esa respuesta es la base del índice de madres que 3.5b.6 va a calcular.

### 3. La operación la dispara el servidor desde `ClassifyCohortByWeightCommand`

El handler de la nueva operación en Breeding (`ClassifyCohortByWeightCommand`):
- Recibe `(NursingCohortId, SortingDate, Assignments: List<{AnimalId, TargetGroupId, WeightKg}>)`.
- Valida que la cohorte esté destetada (`NursingCohort.WeanedAt != null`), que no esté ya clasificada (`NursingCohort.SortedAt == null`), que cada `AnimalId` sea cría de la cohorte (`Animal.BirthingId → Birthing.NursingCohortId`), que cada `TargetGroupId` exista y esté en `TrackingMode.Headcount`, y que `Σ 1 por Assignment == Σ Birthing.WeanedCount` de la cohorte.
- Emite los 90+3 eventos vía `AnimalEvent.CreateIndividual(...)` y `AnimalEvent.CreateForGroup(...)` (factories ya existentes).
- Crea las membresías (`GroupMembership`) con `joinedAt = SortingDate`, `isActive = true`.
- Marca `NursingCohort.MarkSorted(SortingDate)` — método nuevo en el dominio con invariante "post-weaning" + "idempotente".

### 4. La transición a `IndeterminateIndividualState` se infiere, no se marca

`ResolveIndividualState(animalId, asOf)` (de 3.5a.1 sec.6, ya implementado en `Animals/ResolveIndividualStateQuery.cs` y expuesto en `GET /api/v1/animals/{id}/individual-state`) ya responde `Indeterminate` para un animal cuyo `AnimalGroup.TrackingMode == Headcount` lo incluye como miembro activo. **No agregamos columna nueva a `Animal`**: la transición se deduce de la membresía activa al grupo `Headcount`. Si la operación no crea esas membresías, la lectura sigue diciendo `Alive` para siempre. Por eso la sección 3 insiste en que las membresías se crean en la misma transacción que los eventos.

### 5. No se elimina la identificación individual de las crías tras clasificar

`Animal` sigue siendo la entidad soberana de cada cerdo (UUID, genealogía, arete si llega tarde). Lo que cambia es **cómo el sistema responde** sobre ella después de clasificar: `GET /api/v1/animals/{id}/individual-state` devuelve `Indeterminate` mientras esté en un lote `Headcount`. El día que se arete (cuando el cliente decida), un `MovementEvent` o un nuevo arete en `animal_identifiers` la saca de `Indeterminate` y vuelve a `Alive` — sin tocar la fila `Animal`. Esto es exactamente la propiedad que el ADR-0015 sec.2 llama *"la trampa a no pisar: aretar las filas viejas de un lote ya mezclado sería elegir arbitrariamente qué fila es qué cerdo"*.

### 6. Tests obligatorios

Regresión:
- Una cohorte de 90 crías clasificada en 3 lotes genera **93** filas de evento (90 individuales + 3 grupales). La cuenta es exacta.
- `GET /api/v1/animals/{criaId}/individual-state` antes de clasificar = `Alive`; después = `Indeterminate`.
- El linaje se preserva: `GET /api/v1/animals/{criaId}` sigue mostrando `motherId`, `birthWeightKg`, `farmTag`.

Cobertura nueva:
- Cohorte sin destetar → comando rechazado.
- Cohorte ya clasificada → comando rechazado (`SortedAt != null`).
- `Σ Assignment < Σ WeanedCount` → comando rechazado con Problem Details.
- `Σ Assignment > Σ WeanedCount` → comando rechazado.
- `TargetGroupId` con `TrackingMode == Individual` → comando rechazado.
- `AnimalId` que no es cría de la cohorte → comando rechazado.
- Sync push de `recordGroupEvent` con `EventType == GroupWeightSorting` es aceptado por el servidor (extiende `EventType` string).

## Alternativas consideradas

- **Un solo `EventType.GroupWeightSorting` con N crías referenciadas en el payload** (p. ej. `affected_count = 90` y un array `animal_ids`). Descartado: contradice el check constraint de `AffectedCount > 0` que ya existe (sin identidad individual) **y** pierde la huella del último instante individual. El argumento que lo justifica ("es anónimo") es el mismo argumento que el cliente reportó para evitar: el sistema termina diciendo "estas 90 cabezas anónimas se mezclaron" sin poder reconstruir qué cerda específica vino de qué camada.

- **Un solo `EventType.WeightSorted` por cría, sin huella grupal**. Descartado: pierde la historia del lote `Headcount` que recibe las N cabezas. Cuando tres meses después alguien pregunte "¿cuándo se formó este lote de engorde?", no hay evento que lo diga — la membresía existe pero su origen temporal sólo lo tiene el grupo, no el historial.

- **Una columna nueva en `Animal` (`LastMixedIntoHeadcountAt`)** como marca explícita de transición a `Indeterminate`. Descartada por ADR-0015 sec.6 y por Art. 8: introduce un flag cuyo significado se solapa con `ResolveIndividualState`, fuerza un UPDATE sobre la fila `Animal` (lo que Art. 1 quiere evitar siempre que se pueda expresar como evento), y exige migraciones de datos hacia atrás en el futuro cuando se areten los ya mezclados.

- **Implementar la clasificación como una operación de `Animal` individual N veces** (un command por cría). Descartado por la regla "un PR = un propósito" de `PLAN-FASE-3-4.md` sec.1.3 y porque arrastra el riesgo de "cabras y cabezas": si la red se corta a mitad, la cohorte queda parcialmente clasificada sin que haya un cursor claro para retomar. La unidad de la operación es la cohorte, no la cría.

- **`TreatmentCourse`-style grouping** (`PLAN-FASE-3-5-PORCINO.md` sec.3.5a.2-B) para agrupar los 93 eventos en una "operación de clasificación". No corresponde a esta rama: `TreatmentCourse` es del sub-dominio de tratamiento, no del de manejo/cohorte. Si más adelante se quiere agrupar los eventos de clasificación (3.5b) como una unidad lógica superior, se hace con un `CohortClassificationId` agregado al payload — sin tocar este ADR.

## Consecuencias

### Positivas

- **Art. 1 y Art. 4 cumplidos sin invenciones**: la transición "individual → mezclado" queda registrada como dos secuencias de eventos inmutables. El pasado no se edita y el cambio relevante es un evento fechado.
- **Genalogía fina del último instante**: si en 3.5b.6 (Índice de madres) se quiere correlacionar camada de origen con lote de engorde, hay una fila por cría que lo dice.
- **Sincronización limpia**: la nueva operación es del servidor, no del cliente móvil — el operario la hace desde el panel con la lista de pesos ya pesados. Pero si la operación se invoca desde el sync (futuro), los dos `EventType` se empujan como `recordAnimalEvent` / `recordGroupEvent` que el push ya soporta (extensión por string del enum).
- **El contador global de "cabezas vivas" no se inventa**: sigue siendo la suma de membresías activas a grupos `Headcount` menos las bajas, como ya estaba.

### Negativas / costos

- **93 filas de evento para 90 crías** parece verboso, pero el costo es de BD (índices y storage), no de usuario. `animal_events` ya tiene índices por `(animal_id, occurred_at)` y `(group_id, occurred_at)`, así que las consultas siguen rápidas. Es el precio de no inventar datos sintéticos.
- **El servidor confía en que las membresías se crean junto con los eventos** (sección 4). Si un bug rompiera la transacción, habría eventos sin membresía — un animal con `IndividualState = Indeterminate` pero sin lote donde buscarlo. La cobertura de la prueba de regresión `Σ Memberships == Σ WeightSorted events` lo detecta.
- **`EventType` crece en dos valores**. No rompe la conversión a string ya existente, pero cualquier consumidor que asuma exhaustividad (switch en C# sin default) tendría que actualizarse. Esto se mitiga con el test de arquitectura que ya existe para los enums convertidos como string.

### Condición de reversa

Si durante el piloto real la traza de 93 eventos por camada resulta operativa y materialmente insostenible (el cliente reporta "tarda mucho en cargar la ficha del lote"), se evalúa reducir a un solo evento grupal con un array JSONB de asignaciones `{animal_id, weight_kg}` en el payload. La condición de reversa es concreta: **latencia de carga de la ficha del lote > 2 segundos sostenidamente, atribuible al conteo de eventos** — no atribuible a la conexión ni al frontend. La señal se mide durante el primer mes del piloto.
