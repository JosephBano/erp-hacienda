# spec-3.5a.md — Bloque 3.5a: Captura (habilita el piloto)

> **Qué es este documento.** La mitad de [`spec.md`](./spec.md) que no entraba en las ~400
> líneas del archivo principal: las nueve ramas del bloque de captura, con la numeración de
> tarea (`3.5a.N task M`) que 85 citas del código usan para apuntar aquí. Las decisiones que
> gobiernan este bloque (2.1–2.3) y todo lo demás de la fase están en `spec.md`. La secuencia
> de ejecución y el estado de merge de cada rama están en [`plan.md`](./plan.md); el checklist
> ejecutable en [`tasks.md`](./tasks.md); la verificación manual en
> [`test-e2e.md`](./test-e2e.md).

---

## 3. Bloque 3.5a — Captura (habilita el piloto)

### 3.5a.0 · `feature/field-app-input-guards` — **empezar por acá**

*Por qué primero:* son los defectos que el cliente reportó con el dedo puesto encima, **no
necesitan ningún cambio de backend**, y cierran en días. Poner esto en sus manos rápido es
lo que sostiene la conversación mientras se construye el resto.

> **Esta rama es la excepción a la compuerta de `spec.md` sec. 2.3**: son correcciones
> puntuales sobre pantallas que ya existen, no navegación nueva. El árbol de actividades se
> dibuja **en paralelo** a esta rama, para que esté listo cuando llegue 3.5a.7.

Tareas:
1. `assertVolume` (`milkingService.ts:247`) acepta hoy `liters >= 0`: **0 litros pasa**.
   Corregir a `> 0` estricto, con mensaje que explique por qué.
2. `BirthScreen.tsx:33` sólo sabe agregar crías (`addCalf`) y no permite quitarlas — el
   ejemplo exacto que dio el cliente. Agregar quitar y editar por cría.
3. Pantalla de resumen antes de confirmar el parto: madre, padre, conteo por sexo, lista de
   crías. Confirmar es un toque más **sólo cuando hay algo que revisar**.
4. Fijar en el código el principio que rige todo lo demás: **tres toques para lo normal,
   cuatro para lo raro**. No castigar el error humano: confirmar lo improbable, bloquear
   sólo lo imposible.

> El techo de litros (1000 L en una vaca) **no entra acá**: necesita rangos configurables
> por especie y eso es backend. Va en 3.5a.6.

> **Sí, se arregla Ordeño aunque 3.5a.9 lo vaya a apagar.** Son dos cosas distintas: el
> módulo se oculta porque no aplica a esta finca (ADR-0019), no porque esté roto. Un módulo
> se guarda **sano**, no averiado — el día que se encienda nadie va a recordar que arrastraba
> un defecto conocido. Y el arreglo es de una línea (`liters > 0`), mientras que el trabajo
> de fondo de esta rama —quitar y editar crías, el resumen previo— es el que el cliente
> reportó y no tiene nada que ver con la leche.

---

### 3.5a.1 · `feature/livestock-group-events` · **estructural** (ADR-0015)

*Por qué:* sin sujeto grupal no existe el lote por conteo, y `DATA-MODEL.md` sec. Núcleo 2 lo
tenía dibujado desde la Fase 1 sin construir.

Tareas:
1. `AnimalGroup.TrackingMode` ∈ {`Individual`, `Headcount`}, default `Individual`.
2. `AnimalEvent`: `AnimalId` pasa a **XOR** con `GroupId`. CHECK en BD **además** del
   dominio, para que un consumidor que asuma `AnimalId` no nulo falle al escribir y no en
   silencio al leer. Hoy `AnimalEvent.cs:58` exige `animalId != Guid.Empty`.
3. Tipos nuevos: `GroupWeighing` `{sample_count, avg_kg, min_kg, max_kg}`, `GroupMortality`
   `{count, cause_id}` y `GroupDiagnosis` `{affected_count, condition, notes}` — este último
   es el *"en este lote hay uno enfermo"* que pidió el cliente, **sin identificar cuál**, que
   es exactamente lo que él quiso decir. `EventType.Vaccination` —que existe en
   `EventEnums.cs` y **nunca se emitió**— pasa a usarse de verdad.

   > **Nota de diseño (post-implementación, sin actualizar aquí hasta ahora):** el código no
   > construyó `GroupWeighing`/`GroupMortality`/`GroupDiagnosis` como tipos de evento
   > separados. `EventEnums.cs:6-19` define un único `EventType` (`Weighing`, `Treatment`,
   > `Vaccination`, `Diagnosis`, `Movement`, `Disposal`, `Correction`, más los dos tipos de
   > 3.5a.4) compartido entre animal y lote. `AnimalEvent.cs:10-14` lo documenta de forma
   > explícita: *"the subject is exactly one of `AnimalId` / `GroupId` (ADR-0015 sec.2):
   > 'vaccinated this animal' and 'vaccinated this lot' are the same `EventType` with a
   > different subject, not two event types. A group event is never materialized per member
   > — that would invent which individual it happened to."* Es decir: en vez de tipos
   > `Group*` paralelos a los individuales, el mismo `EventType.Weighing`/`Diagnosis`/etc. se
   > reutiliza y el sujeto (`AnimalId` vs `GroupId`, XOR por CHECK de BD — tarea 2) es lo que
   > distingue el caso grupal del individual. Solo la mitad final de esta tarea —
   > `EventType.Vaccination` efectivamente emitido para grupos — se completó tal como estaba
   > escrita (`GetAnimalGroupQueries.cs:194`). El código no explica por qué se tomó esta
   > decisión en vez de crear los tres tipos nuevos; no hay comentario, ADR ni commit que dé
   > la razón, así que no se puede afirmar aquí más que lo que el propio tipo unificado deja
   > ver: evita que cada evento futuro (pesaje, mortalidad, diagnóstico, y lo que venga
   > después) necesite una versión `Group*` duplicada de sí mismo.
4. `LiveHeadCount` como consulta derivada (membresías activas − bajas del lote). **Nunca un
   contador editable.**
5. **Cierre en cascada del lote** (ADR-0015 sec. 7). Las bajas parciales —se venden 20 de 42,
   que es como se faena de verdad— **no cierran a ningún animal**: bajan el conteo. Cuando el
   lote llega a cero, la disposición final cierra todas las membresías restantes en bloque y
   marca esos animales de baja **con alcance de lote**. Sin esto el modelo tiene una fuga:
   las filas nunca se cerrarían y un conteo de animales vivos devolvería 42 fantasmas por
   cada lote ya faenado.
6. Lectura consciente del modo: el estado individual de un animal en lote `Headcount` se
   responde como *indeterminado*, no como un booleano inventado. **En una sola función**,
   no repartida por los llamadores. Propuesta de nombre (a fijar al implementar):
   `ResolveIndividualState(animalId, asOf) → Alive | Indeterminate | Disposed`, alineada
   con el término `IndeterminateIndividualState` del glosario. Los **agregados**
   ("cuántos animales vivos hay") se resuelven por el cierre en cascada del punto 5, no
   por esta función.
7. Endpoints de eventos grupales + push de sync (`PushSyncCommands.cs` gana el caso
   `recordGroupEvent`).
8. Migración EF Core + reflejo en `SyncPullQueries` y en el esquema local del móvil.

---

### 3.5a.2 · `feature/livestock-treatment-detail` · **estructural — split en 3.5a.2-A, 3.5a.2-B y 3.5a.2-C**

> Se partió en tres sub-ramas ejecutables ([`3.5a.2-A`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md),
> [`3.5a.2-B`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-B.md),
> [`3.5a.2-C`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-C.md)) porque la rama original de 11
> tareas violaba la regla "un PR = un propósito". Detalle de tareas y pruebas en cada
> sub-plan; no absorbido aquí.

*Por qué:* hoy `dose` es **texto libre** (`eventService.ts`), lo que incumple el Art. 10
("toda cantidad física lleva su unidad explícita"). Y no se distingue una vacuna de
calendario de un tratamiento por enfermedad, que es el punto 1 del cliente.

**El choque con la Constitución es aparente, y la salida es medida exacta + observación.**
La trampa está en asumir que estructurar la dosis significa hacerle elegir la unidad al
operario. **La unidad no es del operario: es del producto.** Un frasco de oxitetraciclina se
dosifica en ml, siempre. Si el ítem de inventario declara su unidad, la app pide **un número
y nada más**: hoy el operario tipea `"10 ml"` en un campo de texto, mañana toca `10` en un
teclado numérico. Es *menos* trabajo, no más. El Art. 10 y la regla de los tres toques
apuntan al mismo lado; el texto libre era lo peor de ambos mundos.

---

### 3.5a.3 · `feature/livestock-mortality-causes`

*Por qué:* sin causa, la mortalidad es un número que no permite decidir nada. Con causa,
distingue "madre que aplasta" de "madre con mala leche" — que es el juicio que el cliente
hace hoy a ojo y que el sec. 4.6 va a necesitar.

Tareas:
1. Catálogo `mortality_causes` (Art. 8): aplastamiento, inanición, débil al nacer, diarrea,
   hernia, desconocida. Ampliable desde el panel — la lista final la da el cliente
   (`spec.md` sec. 7-B).
2. `DisposalType.Death` gana `cause_id`, y `GroupMortality` lo lleva también.
3. Registro de baja de lechón desde el móvil, con la madre resuelta automáticamente
   mientras la cría siga en su cohorte de lactancia.

> **Nota de tamaño.** Esta rama tiene sólo 3 tareas y podría fusionarse con `3.5a.2-A`
> (catálogos de tratamiento) si al planificar la implementación se prefiere un solo PR.
> La separación actual favorece títulos más legibles y diffs acotados; la decisión puede
> revertirse sin costo cuando llegue el momento.

---

### 3.5a.4 · `feature/breeding-nursing-cohort` · **estructural**

*Por qué:* los 24 días se cuentan **hasta que la última camada los cumple**. Es una regla de
manejo real que ningún modelo por camada individual puede expresar.

Tareas:
1. `NursingCohort`: agrupa las camadas nacidas en días consecutivos que se manejan juntas.
2. `weaning_date` derivada: `max(birth_date de sus camadas) + días_de_lactancia`, con los
   días como **parámetro configurable por especie**, no la constante 24.
3. **Peso al nacer por lechón** en el flujo de parto — el dato de analítica que el cliente
   pidió explícitamente. `BirthScreen` pasa de "toque por sexo" a "sexo + peso por cría",
   sin perder la velocidad para quien no quiera pesar.
4. Operación **clasificación por peso**: reparte la cohorte destetada en lotes de engorde en
   modo `Headcount`. Es el momento en que termina la identificación individual, y se
   registra como tal.
5. `Birthing.RecordWeaning` ya existe y valida `weanedCount <= BornAlive`: se integra con la
   cohorte en vez de duplicarse.

---

### 3.5a.5 · `feature/inventory-unit-conversions`

*Por qué:* `DATA-MODEL.md` sec. Núcleo 3 anticipó el "bug del saco" y **nunca se implementó**:
`InventoryItem` tiene una sola `Unit` y no existe tabla de conversiones. El engorde lo
vuelve bloqueante — se compra en sacos y se consume en kilos.

Tareas:
1. `unit_conversions` `{item_id, from_unit, to_unit, factor}`. `saco40kg` → 40 `kg`.
2. `GroupFeedConsumption` guarda **ambas**: lo registrado (3 sacos de 40 kg) y la base
   (120 kg). Lo que el operario tecleó no se pierde al normalizar.
3. `feed_stage` en ítems de categoría `Feed`: preiniciador, iniciador, crecimiento, engorde,
   gestación, lactancia.
4. Registro de consumo por lote desde el móvil, en sacos.

---

### 3.5a.6 · `feature/livestock-plausibility-ranges`

*Por qué:* el cliente reportó que la app acepta **1000 litros** de una vaca. El techo no
puede ser una constante: un lechón al nacer no pesa lo que un cerdo de engorde.

Tareas:
1. Rangos configurables (Art. 8) por especie y categoría: `plausible_min`/`plausible_max`
   (→ **confirmación**) y `absolute_min`/`absolute_max` (→ **bloqueo**), para peso y para
   litros.
2. Se sincronizan al móvil y se evalúan **localmente**: un control que sólo funciona con
   señal no es un control (Art. 9), exactamente como ya se resolvió el retiro en
   `milkingService`.
3. UI de confirmación: *"1000 L es mucho más de lo normal para este animal. ¿Es correcto?"*
   El dato improbable se puede registrar; el imposible no.
4. Semilla con valores razonables para porcino y bovino, ajustables por el cliente.
5. **Fail-open por diseño, no por descuido.** Una especie recién registrada o una
   categoría nueva quedan sin rangos y **el sistema no bloquea nada**: registrar
   un dato sin rango configurado es legal. El contraste con el resto del sistema
   es deliberado: `Species.IsMilkable` default `false` (fail-closed, default
   seguro porque una especie desconocida *no debe* ordeñarse) y los permisos
   (ADR-0007) tienen "no-permitido por defecto". Acá el razonamiento es inverso:
   un rango olvidado no puede impedir registrar la realidad del campo, porque el
   daño de perder un registro real es peor que el ruido de aceptar un valor
   sospechoso que el operario acaba de tipear. **Consecuencia operativa**: la
   semilla del punto 4 debe existir desde el primer día de la rama, no semanas
   después — sin semilla, la rama no detecta nada, que es exactamente el modo
   fail-open.

---

### 3.5a.7 · `feature/field-app-lot-registration`

*Por qué:* es la superficie de campo de todo lo construido en 3.5a.1. Sin ella, el lote por
conteo existe sólo en el backend.

> **Compuerta (`spec.md` sec. 2.3): esta rama no arranca sin el árbol de actividades cerrado
> con el cliente y los toques contados.** Es la rama que introduce el sujeto "lote", que hoy
> no existe en la navegación — el lugar exacto donde una decisión apurada condena la app a
> ser un menú de botones.

Tareas:
1. Pesaje muestral del lote: cuántos se pesaron y los pesos; el promedio lo calcula la app.
2. Baja del lote con causa y cantidad.
3. Vacunación/tratamiento de lote completo, con las cabezas tratadas.
4. **"En este lote hay uno enfermo"**: diagnóstico grupal con cantidad de cabezas afectadas y
   observación, sin identificar cuál. Es literalmente lo que el cliente describió, y si
   después se trata, es un tratamiento de lote sobre 1 cabeza.
5. Consumo de alimento del lote en sacos.
6. Ficha del lote: cabezas vivas, peso promedio del último muestreo, última vacunación,
   cabezas marcadas como enfermas, alimento del período.

---

### 3.5a.8 · `feature/field-app-corrections` (ADR-0017)

*Por qué:* Art. 1 exige corregir con un evento nuevo, y **la app no tiene ningún camino de
corrección**, pese a que `AnimalEvent.RelatedEventId`, `EventType.Correction` y
`sync_outbox.result_ref` existen sin uso desde la Fase 1.

Tareas:
1. **`OutboxStatus` gana `cancelled`.** Hoy es exactamente `'pending' | 'synced' |
   'rejected'` (`outbox.ts:6`): el estado **no existe y hay que crearlo**, con su migración
   local (`migrations.ts`) — un teléfono en el campo no se puede reinstalar sin perder la
   cola. La entrada **no se borra**: `outbox.ts:31` ya fijó la regla ("nothing is ever
   removed") y coincide con el Art. 1.
2. Camino A (sigue en el teléfono): cancelar la entrada. El servidor nunca supo de esto.
3. Camino B (ya sincronizó): evento `Correction` con `RelatedEventId`, resuelto desde
   `result_ref`.
4. **La transición `pending → cancelled` es una escritura condicional atómica** dentro de la
   misma transacción que verifica el estado. Si al escribir ya no es `pending`, cae al
   camino B **sin preguntarle nada al usuario**: él tocó "corregir" y quedó corregido.
5. Pantalla "lo que registré hoy": lista del día con estado de sync y acción de corregir. Es
   lo que hace alcanzable todo lo anterior, y generaliza lo que el resumen de leche ya hace.
6. Ventana: mismo día calendario para el registrador; sin límite para el admin desde el
   panel. La corrección hereda el permiso del registro que corrige.

---

### 3.5a.9 · `feature/field-app-herd-navigation` — **split en 3.5a.9-A y 3.5a.9-B**

> Partida en dos sub-planes independientes durante la revisión del macro plan (#43). El
> alcance ejecutable vive en [`3.5a.9-A`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md) y
> [`3.5a.9-B`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-B.md). Cada sub-rama tiene su
> compuerta, sus pruebas y su propio criterio de salida; no absorbidas aquí.

*Por qué:* `EventsScreen.tsx:113-124` pinta **un `BigButton` por animal**. Con 3 vacas
funciona; con 90 cerdos es un scroll infinito. **Esto bloquea el piloto**, aparte y antes
del QR. Y peor que el scroll: el día que la app sume las 15+ actividades del pivote
porcino, la lista plana deja de tener jerarquía razonable y la promesa de los tres toques
se muere sin que nadie tome la decisión de matarla.

| Sub-rama | Alcance principal | ADR | Compuerta |
|---|---|---|---|
| `3.5a.9-A` (rama `feature/field-app-module-visibility`) | Visibilidad de módulos por interruptor explícito (`FarmModule`) — apagar Ordeño para esta finca, sin borrar nada | [ADR-0019](../../adr/0019-visibilidad-de-modulos.md) | Ninguna. Es transversal y testeable en aislamiento. |
| `3.5a.9-B` (rama `feature/field-app-activity-tree`) | Árbol de actividades con sujeto como primer nivel + selector con búsqueda, filtro por lote y "recientes" | — (se apoya en `spec.md` sec. 2.3 y en ADR-0019) | **Compuerta sec. 2.3 cerrada parcialmente por [ADR-0021](../../adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md) (2026-08-07):** el primer nivel (cuatro sujetos) ya mergea vía PR #51/#53; el segundo nivel (actividades del sujeto "lote") sigue gated por 3.5a.7 tareas 1–5 y por `TapBudget` validado contra el operador. |

Decisiones que aplican a las dos sub-ramas: filtrado (no ramificación por especie, Art. 8);
evaluación sin red (Art. 9); orden por frecuencia declarada por el cliente (sin la respuesta
a `spec.md` sec. 7-C, 3.5a.9-B no arranca; 3.5a.9-A sí). **El primer nivel del árbol mergea
con un orden por defecto pineado por test** (ver ADR-0021); el reorden cuando el cliente
responda sec. 7-C será un commit deliberado, no una regresión.

> **Bug histórico resuelto:** la lista original tenía dos puntos numerados `4` (uno el
> interruptor, otro el QR). El error tipográfico quedó en el merge de #41 y se corrigió al
> introducir la tabla de sub-ramas de arriba.

---

### Criterio de salida de 3.5a

#### Para abrir el piloto real (sub-criterio de inicio, ADR-0024)

> Fija **cuándo se abre el piloto real** con el cliente. Es condición **necesaria** para
> abrir el piloto, **no suficiente** para cerrar 3.5a como bloque — el cierre del bloque
> sigue siendo el criterio completo de más abajo.

El piloto real puede abrir cuando estén **mergeadas a develop** las siguientes piezas
(en cualquier orden). Lo que aquí no aparece queda como deuda rastreable en
[`BACKLOG.md`](../../../BACKLOG.md), sección 3.5.

| Pieza | Por qué es requisito para abrir |
|---|---|
| 3.5a.0 input guards | El cliente reportó defectos de dedo que no se arreglan solos. |
| 3.5a.1 group events (ADR-0015) | El sujeto "lote" no existe en el modelo sin esto. La UI llega después; el modelo no puede esperar. |
| 3.5a.2-A y 3.5a.2-B (catálogos + payload) | El camino de tratamiento (lo más frecuente en porcinos) sin esto se queda en texto libre, violando Art. 10. |
| 3.5a.2-C (UI de tratamiento) | Sin pantallas, lo anterior existe sólo en backend. |
| 3.5a.5 inventory unit conversions | El alimento se compra en sacos y se consume en kilos — sin conversión, los números mienten. |
| 3.5a.6 plausibility ranges | El cliente reportó que la app acepta 1000 L; no se abre el piloto con esa puerta abierta. |
| 3.5a.9-A module visibility (ADR-0019) | Apagar Ordeño para esta finca sin tocar código. |
| 3.5a.9-B primer nivel del árbol (ADR-0021) | La navegación de primer nivel (animal, lote como stub, parto, hoy) mergeada. |

No exige 3.5a.7.1–5 (UI del sujeto "lote") ni 3.5a.8 (corrección de registros desde el
teléfono) — esas piezas viven como deuda rastreable con disparador explícito en
`BACKLOG.md`. Mientras esa deuda no se pague, el piloto funciona con el flujo viejo:
`recordAnimalEvent` con `GroupId` directo sobre eventos grupales (sin UI específica del
sujeto "lote"), y corrección de eventos por re-registro manual. Esto está **documentado
como deuda**, no oculto.

#### Criterio completo (cierre del bloque 3.5a)

> Una camada real nacida, pesada y seguida dentro del sistema hasta su clasificación por
> peso a los 24 días, con sus tratamientos registrados con vía y motivo, y **al menos una
> corrección hecha desde el teléfono por un error de dedo real**.

Lo último no es decorativo: es la prueba de que el camino de corrección funciona en manos
de quien comete el error, no en manos de quien lo programó.
