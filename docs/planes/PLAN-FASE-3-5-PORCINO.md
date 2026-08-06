# PLAN-FASE-3-5-PORCINO.md — Plan de ejecución de la Fase 3.5 (Adaptación porcina)

> **Qué es este documento.** El mapa operativo para adaptar HATO al negocio porcino real
> del cliente del piloto, antes de abrir la Fase 4. Dice *qué* ramas existen, *en qué
> orden*, *qué entra en cada una* y *qué pruebas se exigen*. No sustituye a `ROADMAP.md`
> (el qué y el porqué) ni a `CONSTITUTION.md` (las reglas). Si algo aquí contradice la
> Constitución, gana la Constitución.
>
> **El protocolo de trabajo no se repite acá.** Los 9 pasos del ciclo de una feature, el
> nivel de exigencia en pruebas y el formato de PR están en `PLAN-FASE-3-4.md` sec.1 y sec.2, y
> aplican idénticos. Este documento sólo agrega lo específico de la Fase 3.5.
>
> **Estado de partida (2026-08-05):** `develop` en `2e8b5d4`. 6 módulos (Livestock,
> Production, Inventory, People, Breeding, Tasks) + `Hato.Sync.IntegrationTests`. 165
> métodos de prueba en backend (`[Fact]`/`[Theory]`; los `[Theory]` aportan más casos) y
> 104 en `field-app`. Fase 3 con todo el código cerrado y **sólo el piloto real
> pendiente**.

---

## Índice

1. [Por qué existe esta fase](#1-por-qué-existe-esta-fase)
2. [Las decisiones que gobiernan el plan](#2-las-decisiones-que-gobiernan-el-plan)
3. [Bloque 3.5a — Captura (habilita el piloto)](#3-bloque-35a--captura-habilita-el-piloto)
4. [Bloque 3.5b — Análisis y automatización](#4-bloque-35b--análisis-y-automatización)
5. [ADRs de esta fase](#5-adrs-de-esta-fase)
6. [Riesgos y frenos de emergencia](#6-riesgos-y-frenos-de-emergencia)
7. [Lo que sólo el cliente puede responder](#7-lo-que-sólo-el-cliente-puede-responder)

---

## 1. Por qué existe esta fase

El cliente del piloto **no es una lechería: es una granja porcina**. El levantamiento del
2026-08-05 expuso ocho frentes donde el sistema no representa su realidad, y dos de ellos
chocan con supuestos que atraviesan todo el modelo.

La Fase 4 (ventas, compras, contabilidad) necesita volumen de datos cargados que hoy no
existe. Adaptar el dominio **antes** de la Fase 4 es lo que permite que esa carga ocurra.
Ese es exactamente el orden que pide el Art. 11: una fase cierra cuando alguien la usa de
verdad, y hoy nadie puede usar el sistema para lo que esta finca hace.

**Cómo maneja el cliente su operación** (su descripción textual, que es la especificación
de la que sale todo lo demás):

1. Las camadas nacen en días consecutivos. Hay **3 cerdas madres**, identificadas con su ID
   legal.
2. Apenas nacen, **los lechones se pesan uno por uno**. Ese dato decide futuras madres: una
   hembra de ≥1 kg promete, una de <0.7 kg probablemente no crezca.
3. Durante ~24 días siguen con su madre. **El período se cuenta hasta que la última camada
   los cumple**, no camada por camada.
4. Cumplidos los 24 días **se mezclan y se reclasifican por peso** (pequeños con pequeños,
   grandes con grandes; o sólo dos grupos, según cuántos haya). **Desde ahí nadie sabe cuál
   cerdo es cuál.** No hay aretes.
5. El engorde no controla el consumo por animal —"que coman lo que tengan que comer"— pero
   **sí cuántos sacos de 20 o 40 kg entran y cada cuánto se consumen**.
6. La ración se decide **por peso, no por edad**: un cerdo enfermo pesa menos y debe comer
   en proporción a su peso.

El cliente declaró además que **implementará el aretado si el sistema demuestra resultados
con la comida**. Ese "si" es medible y tiene nombre: conversión alimenticia (sec.4.4).

---

## 2. Las decisiones que gobiernan el plan

### 2.1 El lote sabe cuántos, no cuáles (ADR-0015)

Durante los 24 días de lactancia cada lechón es un `Animal` real: está físicamente separado
por camada, así que su peso al nacer y su muerte con causa son **datos individuales
verdaderos**. Al mezclarse, el lote pasa a modo `Headcount` y los eventos posteriores se
registran **al lote**.

El sistema declara *"entraron 90 y murieron 3, no sé cuáles 3"* en vez de elegir 3 filas al
azar. La alternativa —dejar que el sistema asigne— produce datos sintéticos
indistinguibles de los reales, y el costo no se paga hoy sino en Fase 6, cuando ya nadie
puede saber qué dato era cierto.

**El día del aretado nada de esto se tira.** Es la pregunta que conviene tener contestada
antes de escribir la primera línea, y el ADR-0015 le dedica una sección entera. En corto:

- **No se elimina código.** Los eventos grupales ya registrados son historia inmutable
  (Art. 1), así que el código que los lee no puede borrarse mientras existan — es decir,
  nunca. Además `Headcount` no es una capacidad porcina: sirve para pollos, para lotes
  comprados sin identificación y para animales cuyo arete se cayó.
- **La transición no es de código sino de práctica**: aretar al nacer adosa el identificador
  a la **misma fila** que ya se crea hoy, y el período anónimo simplemente no ocurre para esa
  camada. Cero migración.
- **La trampa a no pisar**: aretar las filas viejas de un lote ya mezclado sería elegir
  arbitrariamente qué fila es qué cerdo — el mismo dato sintético que todo esto evita,
  resucitado en la transición y con aspecto de progreso. Los lotes ya mezclados terminan sin
  aretar.

### 2.2 Captura primero, análisis después

La fase se parte en dos bloques y **el piloto arranca al cerrar 3.5a**:

- **3.5a — captura.** Todo lo que se *registra*. Sin esto, el campo no puede cargar la
  realidad porcina.
- **3.5b — análisis.** Todo lo que se *calcula* sobre esos datos, construido **mientras el
  campo ya está cargando**.

El motivo no es de esfuerzo sino de validez: el FCR, el índice de madres y las ventanas del
cronograma **no se pueden calibrar contra una tabla en papel**. Necesitan semanas de datos
reales. Diseñarlos antes es exactamente el error que la Fase 3 ya pagó una vez —cerrarse en
falso sin que nadie la usara— y que su retrospectiva dejó escrito en el `ROADMAP.md`.

El cronograma de vacunación, mientras tanto, se lleva en papel unas semanas. Que es como se
lleva hoy.

### 2.3 El árbol de actividades: entregable **previo** a cualquier pantalla

**Ninguna rama de este plan que toque el móvil empieza a escribir pantallas antes de que
exista el árbol de actividades y estén contados los toques sobre papel.** Es una compuerta,
no una recomendación.

*Por qué:* hoy la app tiene cuatro caminos (ordeño, eventos, parto, sincronización). Este
plan agrega pesaje muestral, mortalidad de lote, vacunación de lote, diagnóstico grupal,
consumo de alimento en sacos, observación de características, clasificación por peso,
corrección de registros y "lo que registré hoy". **Pasa de 4 a más de 15 actividades.** Si
eso se resuelve agregando botones a la pantalla de inicio, la promesa de los tres toques se
muere sin que nadie tome la decisión de matarla.

Este plan enumera ramas **por módulo del backend**, que es como se construye. El árbol
enumera **por lo que la persona hace parada en el corral**, que es como se usa. Los dos hacen
falta y no son el mismo documento.

**El primer nivel del árbol es el sujeto**, y eso no es una preferencia de UX: es el mismo
XOR animal/grupo que el ADR-0015 metió en `animal_events`. Cuando la navegación y el modelo
se ramifican igual, es señal de que el modelo está bien.

```
INICIO
├── Un animal          (las 3 madres, el verraco)
│   ├── tratamiento / vacuna
│   ├── pesaje
│   ├── observar característica        (ADR-0018)
│   ├── mover de lote
│   └── baja con causa
├── Un lote            (engorde, por conteo)
│   ├── alimento (sacos)               ← el más frecuente
│   ├── pesaje muestral
│   ├── vacunar / tratar el lote
│   ├── "hay uno enfermo"              (diagnóstico grupal)
│   ├── baja(s) con causa
│   └── clasificar por peso            (episódico, alto impacto)
├── Un parto
│   ├── registrar camada (sexo + peso por cría)
│   ├── observar conducta de la madre  (ADR-0018)
│   └── destete
├── Lo que registré hoy
│   └── revisar / corregir             (ADR-0017)
└── Pendientes de hoy                  (3.5b, cuando exista el cronograma)
```

Reglas para construirlo:

1. **Se ordena por frecuencia real, no por importancia conceptual.** El alimento se registra
   a diario y el parto es episódico, así que el alimento va más cerca aunque el parto "suene"
   más importante. La frecuencia la dice el cliente, no nosotros.
2. **Se cuenta cada actividad en toques sobre papel, antes de escribir una línea.** El
   estándar ya fijado en 3.5a.0 aplica: **tres toques para lo normal, cuatro para lo raro**.
   Si "el lote comió 3 sacos" da seis toques, se ve en el árbol y no en el piloto.
3. **Se construye con el cliente, no para él.** Es la conversación más barata de todo el
   proyecto y la que más retrabajo evita.
4. **Lo que el árbol deja vacío es información.** Hoy Ordeño ocupa el lugar más visible de la
   app y para esta finca está muerto (por eso 3.5a.9 lo esconde), mientras que "un lote" —el
   sujeto de casi todo el trabajo diario— **no existe como rama**. Eso no se ve leyendo el
   backlog; se ve dibujando el árbol.
5. **Filtrado, no ramificado por especie.** Qué ramas se muestran sale de una conjunción:
   **módulo encendido** (ADR-0019) ∧ capacidades de la finca (especies ordeñables, lotes por
   conteo) ∧ permisos del usuario. Es filtrado sobre un árbol único — jamás un árbol por
   especie, que sería el `if (especie == 'cerdo')` mudándose a la navegación (Art. 8).
   El interruptor de módulo manda: si está apagado, se oculta y no se evalúa nada más.

---

## 3. Bloque 3.5a — Captura (habilita el piloto)

### 3.5a.0 · `feature/field-app-input-guards` — **empezar por acá**

*Por qué primero:* son los defectos que el cliente reportó con el dedo puesto encima, **no
necesitan ningún cambio de backend**, y cierran en días. Poner esto en sus manos rápido es
lo que sostiene la conversación mientras se construye el resto.

> **Esta rama es la excepción a la compuerta de sec.2.3**: son correcciones puntuales sobre
> pantallas que ya existen, no navegación nueva. El árbol de actividades se dibuja **en
> paralelo** a esta rama, para que esté listo cuando llegue 3.5a.7.

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

Pruebas: 0 litros rechazado; quitar la cría #3 de 5 deja las 4 correctas y no descoloca los
sexos; cancelar tras quitar no deja estado sucio; el resumen refleja exactamente lo que se
envía.

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

*Por qué:* sin sujeto grupal no existe el lote por conteo, y `DATA-MODEL.md` sec.Núcleo 2 lo
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
4. `LiveHeadCount` como consulta derivada (membresías activas − bajas del lote). **Nunca un
   contador editable.**
5. **Cierre en cascada del lote** (ADR-0015 sec.7). Las bajas parciales —se venden 20 de 42, que
   es como se faena de verdad— **no cierran a ningún animal**: bajan el conteo. Cuando el
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

Pruebas: evento sin animal ni grupo rechazado; evento con ambos rechazado; el CHECK de BD
se verifica en integración, no sólo el dominio; `LiveHeadCount` tras altas, bajas y salidas;
**baja parcial no cierra ninguna fila de `Animal`**; **al llegar a cero cabezas se cierran
todas las membresías restantes y el conteo global de animales vivos no deja fantasmas**;
push duplicado de un evento grupal → un registro (exigencia de `PLAN-FASE-3-4.md` sec.2.2);
migración corre desde cero.

---

### 3.5a.2 · `feature/livestock-treatment-detail` · **estructural — split en 3.5a.2-A, 3.5a.2-B y 3.5a.2-C**

> **Esta sección se partió en tres sub-ramas ejecutables** porque la rama original
> de 11 tareas violaba la regla "un PR = un propósito" del
> [`PLAN-FASE-3-4.md` sec.1.3](../planes/PLAN-FASE-3-4.md). El contexto y la motivación
> comunes se conservan acá; cada sub-rama vive en su propio archivo y puede ser
> implementada por personas distintas.

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

#### Sub-ramas

| Sub-rama | Alcance principal | Depende de | Rama Git |
|---|---|---|---|
| [`3.5a.2-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md) | Catálogos (`administration_routes`, `TreatmentReason`), payload estructurado, `applied_by` ≠ `recorded_by`, `health_plan_item_id` nullable | — | `feature/livestock-treatment-catalog` |
| [`3.5a.2-B`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-B.md) | Las tres formas de dosis (`Absolute`, `PerWeight`, `PerHead`), `CalculatedDose` vs `AdministeredDose`, dosis opcional, observación libre, `TreatmentCourse` | A | `feature/livestock-treatment-dose-logic` |
| [`3.5a.2-C`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-C.md) | UI: vacunación como camino separado (3 toques) y tratamiento como camino curativo (4 toques), con plausibilidad local | A y B | `feature/field-app-treatment-ui` |

#### Resumen de tareas distribuidas

| # | Tarea | Sub-rama |
|---|---|---|
| 1 | Catálogo `administration_routes` (Art. 8), semilla inicial, ampliable desde el panel | A |
| 2 | `TreatmentReason` ∈ {`Scheduled`, `Curative`, `Preventive`} | A |
| 3 | Payload de tratamiento: `route_id`, `reason`, `batch_id`, `applied_by` ≠ `recorded_by` | A |
| 4 | Las tres formas de dosis (Absoluta, Por peso, Por cabeza) | B |
| 5 | Dosis calculada y administrada se guardan las dos; la diferencia es información | B |
| 6 | La dosis es opcional; si está, lleva unidad (Art. 10) | B |
| 7 | Campo de observación libre (`TreatmentNotes`) | B |
| 8 | `health_plan_item_id` nullable desde ya (forward-compat con 3.5b.1, ADR-0016) | A |
| 9 | `TreatmentCourse`: tratamiento de varios días como una serie con aplicaciones y un único retiro | B |
| 10 | Vacunación como camino propio en la app, separado de tratamiento | C |
| 11 | Pantalla de campo: vía y motivo en la misma pasada, sin sumar toques al caso normal | C |

#### Resumen de pruebas distribuidas

- **Catálogos configurables desde el panel** → [A](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md).
- **`applied_by` ≠ `recorded_by`** → A.
- **`health_plan_item_id` nullable ahora** → A.
- **Dosis con valor y sin unidad rechazada** → [B](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-B.md).
- **Dosis ausente aceptada** (con o sin observación) → B.
- **Dosis por peso resuelta contra el último pesaje** → B.
- **Dosis por peso rechazada si el animal no tiene pesaje** → B.
- **Dosis por peso sobre lote usa promedio muestral y queda `is_estimated`** → B.
- **Calculada ≠ administrada persiste sin corregir ninguna** → B.
- **Ruta inexistente o `is_active = false` rechazada** → A y B (B valida la FK; A garantiza que el catálogo es la fuente).
- **Serie de 3 días produce 3 aplicaciones y un solo retiro** → B.
- **No regresión del cálculo de retiro (Art. 19)** → B.
- **Tres toques para vacunación, cuatro para tratamiento** → [C](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-C.md).
- **Cancelar no deja estado sucio** → C.

---

### 3.5a.3 · `feature/livestock-mortality-causes`

*Por qué:* sin causa, la mortalidad es un número que no permite decidir nada. Con causa,
distingue "madre que aplasta" de "madre con mala leche" — que es el juicio que el cliente
hace hoy a ojo y que el sec.4.6 va a necesitar.

Tareas:
1. Catálogo `mortality_causes` (Art. 8): aplastamiento, inanición, débil al nacer, diarrea,
   hernia, desconocida. Ampliable desde el panel — la lista final la da el cliente (sec.7).
2. `DisposalType.Death` gana `cause_id`, y `GroupMortality` lo lleva también.
3. Registro de baja de lechón desde el móvil, con la madre resuelta automáticamente
   mientras la cría siga en su cohorte de lactancia.

Pruebas: baja individual con causa; baja grupal con causa; causa inexistente rechazada; la
mortalidad predestete por madre se agrega correctamente sobre datos sembrados.

> **Nota de tamaño.** Esta rama tiene sólo 3 tareas y podría fusionarse con `3.5a.2-A`
> (catálogos de tratamiento) si al planificar la implementación se prefiere un solo PR.
> La separación actual favorece títulos más legibles y diffs acotados; la decisión puede
> revertirse sin costo cuando llegue el momento. Si se decide combinar, mover aquí las
> tareas 1–3 de `3.5a.2-A` consume ambas ramas en un solo PR.

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

Pruebas: cohorte con 3 camadas de días distintos deteta por la última; peso al nacer
persiste y sincroniza; clasificación reparte N cabezas en M lotes sin perder ni duplicar
cabezas; los animales conservan `mother_id` tras la clasificación (el linaje **no** se
pierde).

---

### 3.5a.5 · `feature/inventory-unit-conversions`

*Por qué:* `DATA-MODEL.md` sec.Núcleo 3 anticipó el "bug del saco" y **nunca se implementó**:
`InventoryItem` tiene una sola `Unit` y no existe tabla de conversiones. El engorde lo
vuelve bloqueante — se compra en sacos y se consume en kilos.

Tareas:
1. `unit_conversions` `{item_id, from_unit, to_unit, factor}`. `saco40kg` → 40 `kg`.
2. `GroupFeedConsumption` guarda **ambas**: lo registrado (3 sacos de 40 kg) y la base
   (120 kg). Lo que el operario tecleó no se pierde al normalizar.
3. `feed_stage` en ítems de categoría `Feed`: preiniciador, iniciador, crecimiento, engorde,
   gestación, lactancia.
4. Registro de consumo por lote desde el móvil, en sacos.

Pruebas: conversión ida y vuelta sin pérdida de precisión (`decimal`, Art. 10); consumo en
sacos descuenta los kilos correctos del batch; factor 0 o negativo rechazado; item sin
conversión definida consumido en su unidad base sigue funcionando.

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

Pruebas: valor dentro de rango pasa sin fricción; valor improbable exige confirmación
explícita; valor imposible se rechaza; **sin rangos configurados no se bloquea nada**
(fail-open acá es correcto: un rango olvidado no puede impedir registrar la realidad);
funciona sin red.

---

### 3.5a.7 · `feature/field-app-lot-registration`

*Por qué:* es la superficie de campo de todo lo construido en 3.5a.1. Sin ella, el lote por
conteo existe sólo en el backend.

> **Compuerta (sec.2.3): esta rama no arranca sin el árbol de actividades cerrado con el
> cliente y los toques contados.** Es la rama que introduce el sujeto "lote", que hoy no
> existe en la navegación — el lugar exacto donde una decisión apurada condena la app a ser
> un menú de botones.

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

Pruebas: una prueba de "registro sin red" por pantalla (exigencia de `PLAN-FASE-3-4.md`
sec.2.1 para React Native); el promedio calculado coincide con el enviado; la ficha refleja las
bajas; **cada actividad del árbol se resuelve en los toques que se contaron en sec.2.3** —si la
implementación excede lo dibujado, se corrige el flujo, no se relaja el número.

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

Pruebas: **forzar la carrera** —cancelar mientras un push está en vuelo— y verificar que no
queda registro fantasma; va en `Hato.Sync.IntegrationTests`, que ya existe para esto y ya
encontró un bug real del cliente con los cortes a mitad de lote. Más: cancelación no borra
la fila; corrección de evento sincronizado deja original **y** corrección; corregir fuera de
ventana se rechaza en el móvil.

---

### 3.5a.9 · `feature/field-app-herd-navigation` — **split en 3.5a.9-A y 3.5a.9-B**

> **Esta sección se partió en dos sub-planes independientes** durante la revisión
> del macro plan (#43). El bloque completo sigue acá para conservar el contexto de
> *por qué* la navegación necesita rehacerse, pero el alcance ejecutable vive en los
> sub-planes. Cada sub-rama tiene su compuerta, sus pruebas y su propio criterio de
> salida.

*Por qué:* `EventsScreen.tsx:113-124` pinta **un `BigButton` por animal**. Con 3 vacas
funciona; con 90 cerdos es un scroll infinito. **Esto bloquea el piloto**, aparte y antes
del QR. Y peor que el scroll: el día que la app sume las 15+ actividades del pivote
porcino (pesaje muestral, mortalidad de lote, vacunación de lote, diagnóstico grupal,
consumo en sacos, observación de características, clasificación por peso, corrección,
etc.), la lista plana deja de tener jerarquía razonable y la promesa de los tres toques
se muere sin que nadie tome la decisión de matarla.

#### Sub-ramas

| Sub-rama | Alcance principal | ADR | Compuerta |
|---|---|---|---|
| [`3.5a.9-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md) (rama `feature/field-app-module-visibility`) | Visibilidad de módulos por interruptor explícito (`FarmModule`) — apagar Ordeño para esta finca, sin borrar nada | [ADR-0019](../adr/0019-visibilidad-de-modulos.md) | Ninguna. Es transversal y testeable en aislamiento. |
| [`3.5a.9-B`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-B.md) (rama `feature/field-app-activity-tree`) | Árbol de actividades con sujeto como primer nivel + selector con búsqueda, filtro por lote y "recientes" | — (se apoya en sec.2.3 y en ADR-0019) | **[Bloqueada por sec.2.3 y sec.7-C](#)** del macro plan: el árbol se dibuja y los toques se cuentan con el cliente **antes** de escribir pantallas. |

#### Decisiones que aplican a las dos sub-ramas

- **Filtrado, no ramificación por especie** (Art. 8). El árbol es único y se filtra
  por la conjunción `interruptor ∧ capacidades ∧ permisos`.
- **Evaluación sin red** (Art. 9). Las capacidades y el flag de módulo vienen del
  pull y se evalúan localmente.
- **El orden de las actividades va por frecuencia declarada por el cliente**, no por
  importancia conceptual. Sin la respuesta a sec.7-C, 3.5a.9-B no arranca; 3.5a.9-A sí.

#### Resumen de tareas (distribuidas en los sub-planes)

| # | Tarea | Sub-rama |
|---|---|---|
| 1 | Navegación según el árbol de sec.2.3, primer nivel = sujeto | 3.5a.9-B |
| 2 | Búsqueda por identificador y filtro por lote en el selector de animales | 3.5a.9-B |
| 3 | "Recientes": últimos animales sobre los que este teléfono registró algo | 3.5a.9-B |
| 4 | Ocultar Ordeño por interruptor explícito (ADR-0019), `FarmModule { key, enabled, disabled_reason }` | 3.5a.9-A |
| 4a | No se borra nada (Production, MilkingScreen, milkingService, quick-milking, endpoints, pruebas siguen) | 3.5a.9-A |
| 4b | Se oculta la entrada, jamás el camino de los datos (outbox sigue empujando) | 3.5a.9-A |
| 4c | `Species.IsMilkable` no se elimina: verdad de dominio y filtro cuando el módulo esté encendido | 3.5a.9-A |
| 4d | Es una entrada más del filtrado del punto 1, evaluada sin red (Art. 9) | 3.5a.9-A |
| 5 | Documentar el escaneo QR como paso siguiente natural del aretado (no es trabajo de esta fase) | 3.5a.9-B (tarea derivada, ticket en `BACKLOG.md`) |

> **Bug histórico resuelto:** la lista original tenía dos puntos numerados `4`
> (uno el interruptor, otro el QR). El segundo es el punto 5 de esta tabla; el
> error tipográfico quedó en el merge de #41 y se corrige al mergear el split
> porque esta sección se reemplaza por la tabla de arriba.

#### Resumen de pruebas (distribuidas en los sub-planes)

- **Búsqueda con 200 animales sembrados devuelve el correcto** → [3.5a.9-B](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-B.md).
- **Filtro por lote** → 3.5a.9-B.
- **Módulo apagado: pantalla de ordeño no alcanzable desde ninguna ruta** → [3.5a.9-A](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md).
- **Módulo apagado: ordeño en outbox sigue sincronizando** (regla del ADR-0019 sec.4, única pérdida silenciosa posible) → 3.5a.9-A.
- **Encender el módulo lo devuelve sin tocar código** → 3.5a.9-A.
- **Pantalla que administra los módulos no puede ocultarse a sí misma** → 3.5a.9-A.
- **La navegación resuelve el interruptor sin red** → ambos (A provee el flag, B lo consume).

---

### Criterio de salida de 3.5a

> Una camada real nacida, pesada y seguida dentro del sistema hasta su clasificación por
> peso a los 24 días, con sus tratamientos registrados con vía y motivo, y **al menos una
> corrección hecha desde el teléfono por un error de dedo real**.

Lo último no es decorativo: es la prueba de que el camino de corrección funciona en manos
de quien comete el error, no en manos de quien lo programó.

---

## 4. Bloque 3.5b — Análisis y automatización

> Se construye **mientras el campo ya carga datos de 3.5a**. Cada ítem se calibra contra
> datos reales, no contra la tabla en papel.

### 4.1 · `feature/livestock-health-plans` · **estructural** (ADR-0016)

`HealthPlan` + `HealthPlanItem` con ancla (`Nacimiento` | `InicioDeLote` | `Parto` |
`Destete`), desfase en días, ventana de cumplimiento y filtro por especie/categoría/**sexo**.
Asignable a un lote o a un individuo.

El filtro por sexo es lo que colapsa tres pedidos del cliente en un solo motor: la
castración de machos y la preselección de futuras madres **no son "otro cronograma"**, son
ítems con `applies_to_sex`. El cumplimiento **es el evento** (Art. 4): no hay botón de
"marcar como hecho" separado del registro del hecho.

Pruebas: resolución de fecha teórica por cada ancla; ítem cumplido por un evento que lo
referencia; ítem vencido fuera de ventana; plan de sexo macho no genera pendientes en
hembras.

### 4.2 · `feature/tasks-health-plan-alerts`

Quinto generador `HEALTH_PLAN_ITEM_DUE` en `GenerateAlertsCommand.cs`, copiando el patrón de
los cuatro existentes: leer por contrato público (Art. 6), `AlertAlreadyActiveAsync` para no
duplicar, crear. Cero SQL cruzado entre esquemas.

Pruebas: no duplica alerta activa; se genera al entrar en ventana; no se genera para un ítem
ya cumplido.

### 4.3 · `feature/inventory-feeding-standards`

La tabla peso/día del cliente como filas configurables `{especie, etapa, peso_desde,
peso_hasta, ración_kg_día}`. La ración de la cerda lactante es
`{base_kg: 2, por_cría_kg: 0.5, max_kg: 9}` — **tres números en una fila, jamás un `if`**
(Art. 8).

Salida útil en la app: *"este lote pesa ~35 kg promedio × 42 cabezas → 75.6 kg/día ≈ 2 sacos
de 40 kg"*.

Pruebas: rangos de peso solapados rechazados; la ración de la cerda topa en 9 kg con 15
crías; lote sin pesaje reciente no inventa una recomendación.

### 4.4 · `feature/analytics-lot-fcr`

**Conversión alimenticia por lote**: kg de alimento ÷ kg ganados. Sale gratis de lo que
3.5a ya captura (consumo por lote + pesajes muestrales).

**Es el número que decide el punto 6 del cliente.** Dijo que invertiría en el aretado si el
sistema demuestra resultados con la comida; el FCR *es* esa demostración. En **kg, no en
dinero** — la plata es Fase 4, y el FCR en kg está completo sin contabilidad.

Añadir: alerta cuando el consumo real del lote **diverge del estándar**, que es señal
temprana de enfermedad y el motivo real por el que el cliente cuenta los sacos.

Pruebas: FCR sobre un lote sembrado con valores conocidos, a mano en el test; lote sin
pesaje inicial no produce un FCR falso; la divergencia dispara sobre umbral configurable.

### 4.5 · `feature/livestock-animal-traits` · **estructural** (ADR-0018) — **split en 3.5b.5-A, 3.5b.5-B y 3.5b.5-C**

> **Esta sección se partió en tres sub-ramas ejecutables** porque las 7 tareas originales
> mezclaban cambios de naturaleza muy distinta en un mismo PR: (a) la introducción del núcleo
> del mecanismo, (b) una migración de datos con pérdida potencial si algo falla, y (c) la
> UI donde el valor del mecanismo se vuelve tangible. Cada uno merece su propio diff.

*Por qué:* la evaluación de futuras madres y la calificación materna eran dos subsistemas
distintos, y el segundo (`MaternalBehaviorAssessment`) era **un `if (especie == 'cerdo')`
disfrazado de tabla**. Ambos son el mismo mecanismo: un juicio tipado, fechado y firmado
sobre un animal. **Dos ramas planificadas se vuelven una, y más chica.**

#### Sub-ramas

| Sub-rama | Alcance principal | Depende de | Rama Git |
|---|---|---|---|
| [`3.5b.5-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-A.md) | Núcleo: tablas `animal_traits`, `trait_kinds`, `trait_value_types`, `trait_observations`. Cuatro tipos de valor y nada más. `CurrentDisposition` derivado. | — | `feature/livestock-animal-traits-core` |
| [`3.5b.5-B`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-B.md) | Absorción de `SelectionCriterion` y drenaje de `MaternalBehaviorAssessment`. Semilla de las morfológicas porcinas (tetas, aplomos, hernia, temperamento, etc.). | A | `feature/livestock-selection-criterion-deprecation` |
| [`3.5b.5-C`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) | Advertencias visibles en la ficha del animal + versionado por clonado de las definiciones usadas. | A (recomendada B) | `feature/livestock-trait-alerts-and-versioning` |

#### Resumen de tareas distribuidas

| # | Tarea | Sub-rama |
|---|---|---|
| 1 | `AnimalTrait` (definición) + `TraitObservation` (registro fechado y firmado); `kind` ∈ {Conductual, Morfológica, Manejo}; `especie` nula = global | A |
| 2 | "Se observan, no se asignan": nunca columna editable en `Animal`; `CurrentDisposition` derivado | A |
| 3 | `contexto` opcional apuntando al hecho durante el cual se observó | A |
| 4 | Cuatro tipos de valor y nada más (Booleano, EscalaOrdinal, ConteoAcotado, TextoLibre). Sin unidad y sin decimal libre (guardarraíl estructural) | A |
| 5 | Absorbe `SelectionCriterion` y drena `MaternalBehaviorAssessment`; semillas del sec.7-A (tetas, aplomos, hernia, temperamento, etc.) | B |
| 6 | `visible_como_advertencia` muestra la característica en la ficha del animal en el móvil antes de que alguien lo toque | C |
| 7 | "Una definición usada se versiona, no se edita" (ADR-0018 sec.9): si la escala cambia, las observaciones viejas se interpretan con la versión que tenían al observarse | C |

#### Resumen de pruebas distribuidas

- **Guardarraíl estructural: característica no puede declarar unidad ni aceptar decimal libre**
  (test de **arquitectura**) → [A](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-A.md).
- **`TraitValueType` tiene exactamente 4 filas** → A.
- **`EscalaOrdinal` rechaza valores fuera del conjunto / fuera del string** → A.
- **`ConteoAcotado` rechaza fuera de rango; sin decimal** → A.
- **`CurrentDisposition` derivado y refleja la última observación** → A (lo completa C con el
  versionado).
- **Drenaje exacto e idempotente de `SelectionCriterion`** → [B](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-B.md).
- **`MaternalBehaviorAssessment` queda vacía y se elimina** → B.
- **Sesión → `gilt_evaluation` context preservado** → B.
- **Regresión del `MaternalIndex` (sec.4.6): lee lo drenado, no se pisa** → B.
- **Versionado por clonado (transaccional, no edición)** → [C](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-C.md).
- **Atomicidad y concurrencia del versionado** → C.
- **Interpretación preservada tras versionado** (la prueba más importante del ADR-0018) → C.
- **Alertas visibles en la ficha, sin red** → C.
- **Toggle de alerta en el panel, sin tocar código** → C.

### 4.6 · `feature/breeding-maternal-index`

Ahora **consumidor** de 4.5, no dueño de su propia tabla de conductas.

- **Derivada de eventos contables** (calculada, **no almacenada**, coherente con
  `DATA-MODEL.md` sec.Núcleo 4): nacidos vivos/muertos/momias, peso promedio de camada al nacer,
  **mortalidad predestete 0–24 d por madre**, destetados por parto, intervalo destete–celo.
- **Características conductuales** de 4.5, sólo las genuinamente subjetivas.

> **El criterio del ADR-0018 sec.3 corrigió este diseño.** El aplastamiento de crías estaba
> planificado como ítem de calificación conductual, pero dos personas **sí** coinciden en
> cuántos lechones aparecieron aplastados: es una **medición**, o sea un evento de mortalidad
> con causa, que ya se captura en 3.5a.3. Y "esta cerda es torpe con las crías" ni siquiera
> hace falta como característica — **se deriva** contando esos eventos. Sólo lo
> irreductiblemente subjetivo (¿deja mamar?, ¿es agresiva al manejo?) pasa por
> características. El índice queda **más objetivo** que en el diseño anterior, no menos.

Encima, `MaternalIndex`: puntaje compuesto con **pesos configurables** sobre ambas mitades,
ordenable en el panel. Que los pesos sean configurables es lo que permite calibrar conceptos
ambiguos **usándolos**, que es la única forma de calibrarlos bien.

Pruebas: KPIs derivados contra datos sembrados con resultados escritos a mano; cambiar los
pesos reordena el ranking; una madre sin partos no aparece con índice 0 (aparece sin índice);
el índice no lee ninguna característica que duplique un evento contable.

### 4.7 · `feature/tasks-swine-alerts`

1. **Destete → celo (4–7 días).** El ciclo porcino es corto e intenso. El cliente no lo
   pidió; es de los avisos más rentables que el módulo Breeding ya construido permite.
2. **Retiro en carne bloqueante.** Art. 19 hoy bloquea leche. En engorde lo que importa es
   que un lote tratado **no pueda ir a faena** antes de X días. `WithdrawalTarget.Meat` ya
   existe en `EventEnums.cs` y nadie lo usa. Es requisito legal, no comodidad.

Pruebas: alerta de celo en la ventana correcta tras el destete; **intento de disposición de
un lote en retiro de carne se rechaza**, no se advierte (mismo estándar que
`PLAN-FASE-3-4.md` sec.2.3 exige para leche).

---

## 5. ADRs de esta fase

| ADR | Qué decide | Cuándo se implementa |
|---|---|---|
| [0015](../adr/0015-lote-por-conteo.md) | Lote por conteo, evento grupal XOR, reversibilidad al aretar | 3.5a.1 |
| [0016](../adr/0016-plan-sanitario-configurable.md) | Un solo motor de cronograma por ancla + desfase + filtro | **escrito ahora**, implementado en 3.5b.1 |
| [0017](../adr/0017-correccion-de-registros-de-campo.md) | Dos caminos de corrección según dónde esté el registro | 3.5a.8 |
| [0018](../adr/0018-caracteristicas-observables-del-animal.md) | Un solo mecanismo para los juicios sobre un animal, con guardarraíl estructural | [`3.5b.5-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-A.md), [`3.5b.5-B`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-B.md), [`3.5b.5-C`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) |
| [0019](../adr/0019-visibilidad-de-modulos.md) | Los módulos se ocultan por interruptor explícito y **nunca se borran** | [`3.5a.9-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md) |

Los cinco se mergean **antes** que su código (Art. 14).

Dos son **transversales** y no pertenecen al pivote porcino aunque hayan nacido de él:

- **0018** — "este caballo patea" y "esta vaca se escapa del corral" usan el mismo mecanismo
  que la calificación de madres. Esa fue exactamente la observación que lo hizo existir.
- **0019** — apagar Ordeño para esta finca es el primer uso, pero el mecanismo sirve para
  cualquier módulo y cualquier cliente. Es lo que permite que un mismo producto sirva a
  fincas distintas sin una rama de código por finca.

---

## 6. Riesgos y frenos de emergencia

| Riesgo | Señal temprana | Freno |
|---|---|---|
| 3.5a se estira y el piloto no arranca | 3.5a.0–3.5a.4 sin cerrar a las 3 semanas | Recortar 3.5a.7 y 3.5a.9 al mínimo y arrancar el piloto con lo que haya. Regla anti-estancamiento #1: **recortar alcance, no extender plazo**. |
| El evento grupal rompe consumidores del historial | Build rojo en módulos que leen `AnimalEvent` | Es el resultado buscado: el CHECK XOR y el tipo hacen que falle al compilar y no en el potrero. |
| Avalancha de alertas falsas del cronograma | El cliente deja de mirar el panel | Ventana de cumplimiento por ítem + `AlertAlreadyActiveAsync`. Medir cumplidas/vencidas durante el piloto (condición de reversa del ADR-0016). |
| Las validaciones de plausibilidad estorban | El operario pide "quitá eso" | Los rangos son configurables por el cliente: se ensanchan, no se eliminan. Nada bloquea si no hay rango configurado. |
| Se cuela alcance de Fase 4 | Aparece "costo" o "precio" en un ticket de 3.5 | El FCR y todo lo demás va **en kg**. La plata es Fase 4. |
| El cliente cambia de opinión sobre el aretado | — | No es riesgo: ADR-0015 hace que el aretado sea un cambio de bandera en cualquier momento. |
| Las características se vuelven el vertedero de datos que debían estar tipados | Aparece una característica con unidad, o alguien pide "un número libre" | El guardarraíl es estructural: cuatro tipos de valor, sin unidad ni decimal libre, con un test que fija la invariante. La petición misma es la señal de que ese dato va al esquema (ADR-0018 sec.4). |
| El catálogo de características se llena y nadie observa nada | Definiciones sin observaciones al cerrar la fase | Reducir a lo que demostró valor —probablemente sólo las advertencias visibles— y calcular el índice materno con KPIs derivados de eventos (condición de reversa del ADR-0018). |
| La app se vuelve un menú de botones y muere la promesa de los 3 toques | Una actividad nueva se resuelve "agregando un botón al inicio" | La compuerta de sec.2.3: el árbol se dibuja y los toques se cuentan **antes** de escribir pantallas. De 4 actividades a más de 15 no se sobrevive improvisando la navegación. |
| **Ordeño se pudre mientras está oculto** | Nadie lo ejercita a mano; un defecto no cubierto por pruebas vive ahí meses | Su suite sigue corriendo en CI igual que antes y en rojo bloquea el merge (Art. 12, ADR-0019 sec.7). Al reencenderlo se trata como feature que vuelve a producción —revisión y prueba manual—, no como un interruptor inocuo. |
| Se pierden ordeños pendientes al apagar el módulo | Un teléfono con registros de leche sin sincronizar | ADR-0019 sec.4: se oculta la entrada, **nunca el camino de los datos**. Los endpoints siguen aceptando y el motor de sync siguen empujando lo que ya se registró. Cubierto por prueba en [`3.5a.9-A`](./sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.9-A.md). |

---

## 7. Lo que sólo el cliente puede responder

Cuatro cosas quedan **deliberadamente incompletas** porque su contenido es conocimiento de
la finca, no decisión de diseño. Conviene llevarlas impresas a la próxima visita.

> **A y B no bloquean nada**: son filas de catálogo (ADR-0018 y `mortality_causes`), así que
> el sistema se construye sin ellas y se llenan cuando él las dé. Preguntarlas temprano sirve
> para que la semilla inicial no sea inventada.
>
> **C sí bloquea**: sin las frecuencias no se cierra el árbol de actividades, y sin el árbol
> no arranca 3.5a.7 (compuerta de sec.2.3).

**A · Criterios de selección de futuras madres.** En la conversación mencionó el número de
tetas y la postura de las patas, y dijo que había más que no quedaron anotados. La propuesta
del sec.4.5 es un punto de partida, no una lista cerrada:

- ¿Cuántas tetas funcionales exige como mínimo? ¿Descarta por asimetría?
- ¿Qué mira exactamente en los aplomos, y cómo lo puntúa hoy (bien/regular/mal, o más fino)?
- ¿A qué edad o peso hace la selección?
- ¿Qué descarta de inmediato, sin discusión?
- ¿Mira el tamaño de la camada de la que salió la hembra?

**B · Causas de muerte de lechón.** La lista del sec.3.5a.3 es la estándar; la suya puede
diferir y es la que importa:

- ¿Qué causas distingue en la práctica cuando muere un lechón?
- ¿Separa "aplastamiento" de "débil que no llegó a mamar"? Esa distinción es exactamente la
  que hace útil el índice de madres del sec.4.6.

**C · Frecuencia real de cada actividad**, que es lo que ordena el árbol de sec.2.3. Esta sí
conviene resolverla temprano, porque es la única de las tres que **bloquea** una rama
(3.5a.7 no arranca sin el árbol cerrado):

- ¿Cuántas veces por semana registra alimento? ¿Y pesaje del lote?
- ¿Qué hace **todos los días** sin falta, y qué hace una vez al mes?
- Si tuviera que llegar a una sola cosa en un toque desde que abre la app, ¿cuál sería?

La respuesta reordena el árbol. Lo más frecuente va más cerca, aunque conceptualmente "suene"
menos importante que un parto.

**D · Dos preguntas de manejo que quedaron abiertas:**

- Los 24 días, ¿son siempre 24 o varían según cómo venga la camada? (El modelo lo trata como
  parámetro configurable por especie, no como constante.)
- Cuando clasifica por peso, ¿son siempre tres grupos (pequeños/medianos/grandes) o dos según
  la cantidad? (El modelo admite N lotes; la pregunta es qué ofrecer por defecto en la app.)
