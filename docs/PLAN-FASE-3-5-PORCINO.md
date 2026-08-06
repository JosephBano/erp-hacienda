# PLAN-FASE-3-5-PORCINO.md — Plan de ejecución de la Fase 3.5 (Adaptación porcina)

> **Qué es este documento.** El mapa operativo para adaptar HATO al negocio porcino real
> del cliente del piloto, antes de abrir la Fase 4. Dice *qué* ramas existen, *en qué
> orden*, *qué entra en cada una* y *qué pruebas se exigen*. No sustituye a `ROADMAP.md`
> (el qué y el porqué) ni a `CONSTITUTION.md` (las reglas). Si algo aquí contradice la
> Constitución, gana la Constitución.
>
> **El protocolo de trabajo no se repite acá.** Los 9 pasos del ciclo de una feature, el
> nivel de exigencia en pruebas y el formato de PR están en `PLAN-FASE-3-4.md` §1 y §2, y
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
2. [Las dos decisiones que gobiernan el plan](#2-las-dos-decisiones-que-gobiernan-el-plan)
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
con la comida**. Ese "si" es medible y tiene nombre: conversión alimenticia (§4.4).

---

## 2. Las dos decisiones que gobiernan el plan

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

---

## 3. Bloque 3.5a — Captura (habilita el piloto)

### 3.5a.0 · `feature/field-app-input-guards` — **empezar por acá**

*Por qué primero:* son los defectos que el cliente reportó con el dedo puesto encima, **no
necesitan ningún cambio de backend**, y cierran en días. Poner esto en sus manos rápido es
lo que sostiene la conversación mientras se construye el resto.

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

---

### 3.5a.1 · `feature/livestock-group-events` · **estructural** (ADR-0015)

*Por qué:* sin sujeto grupal no existe el lote por conteo, y `DATA-MODEL.md` §Núcleo 2 lo
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
5. **Cierre en cascada del lote** (ADR-0015 §7). Las bajas parciales —se venden 20 de 42, que
   es como se faena de verdad— **no cierran a ningún animal**: bajan el conteo. Cuando el
   lote llega a cero, la disposición final cierra todas las membresías restantes en bloque y
   marca esos animales de baja **con alcance de lote**. Sin esto el modelo tiene una fuga:
   las filas nunca se cerrarían y un conteo de animales vivos devolvería 42 fantasmas por
   cada lote ya faenado.
6. Lectura consciente del modo: el estado individual de un animal en lote `Headcount` se
   responde como *indeterminado*, no como un booleano inventado. **En una sola función**,
   no repartida por los llamadores. Los **agregados** ("cuántos animales vivos hay") se
   resuelven por el cierre en cascada del punto 5, no por esta función.
7. Endpoints de eventos grupales + push de sync (`PushSyncCommands.cs` gana el caso
   `recordGroupEvent`).
8. Migración EF Core + reflejo en `SyncPullQueries` y en el esquema local del móvil.

Pruebas: evento sin animal ni grupo rechazado; evento con ambos rechazado; el CHECK de BD
se verifica en integración, no sólo el dominio; `LiveHeadCount` tras altas, bajas y salidas;
**baja parcial no cierra ninguna fila de `Animal`**; **al llegar a cero cabezas se cierran
todas las membresías restantes y el conteo global de animales vivos no deja fantasmas**;
push duplicado de un evento grupal → un registro (exigencia de `PLAN-FASE-3-4.md` §2.2);
migración corre desde cero.

---

### 3.5a.2 · `feature/livestock-treatment-detail` · **estructural**

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

Tareas:
1. Catálogo `administration_routes` (Art. 8): oral en agua, oral en alimento, IM, SC,
   tópica, intranasal, intrauterina. Semilla inicial, ampliable desde el panel.
2. `TreatmentReason` ∈ {`Scheduled`, `Curative`, `Preventive`}.
3. Payload de tratamiento: `route_id`, `reason`, `batch_id` (lote de inventario consumido),
   `applied_by` distinto de `recorded_by`.
4. **La dosis tiene tres formas, no una.** En porcinos se dosifica por peso mucho más que en
   bovinos —y por la misma razón por la que el cliente alimenta por peso: un cerdo enfermo
   pesa menos y le corresponde menos.

   | Forma | Ejemplo | Cómo se resuelve |
   |---|---|---|
   | Absoluta | 10 ml a este animal | El operario da el número; la unidad la pone el producto. |
   | **Por peso** | 1 ml / 10 kg | Se resuelve contra el último pesaje del animal. |
   | Por cabeza | 1 dosis × 42 cabezas | Vacunación de lote. |

5. **Se guardan la dosis calculada y la administrada, no una sola.** En un lote por conteo la
   dosis por peso se calcula contra el promedio muestral: 42 cabezas × 35 kg × 1 ml/10 kg =
   147 ml. Eso es una estimación con incertidumbre real. Lo que salió del frasco es exacto.
   **La diferencia entre ambas es información**: si el sistema sugirió 147 ml y se
   administraron 200, alguien derramó, alguien subdosificó, o el muestreo de peso está mal.
   Ninguna de las tres se puede detectar hoy, y sale de guardar dos números en vez de uno.
   Es además lo que hace que el descuento de inventario deje de ser una adivinanza.
6. **La dosis es opcional; si está, lleva unidad.** Cuando el operario genuinamente no sabe
   la cantidad ("le puse lo que quedaba en el frasco"), un campo obligatorio produce un
   número inventado — y un `5 ml` falso es peor que un texto honesto, porque nadie puede
   distinguirlo después de un `5 ml` real. El Art. 10 exige que **toda cantidad lleve
   unidad**; no exige que toda aplicación tenga cantidad. Ante la duda gana el Art. 1, que
   protege la integridad del historial.
7. **Campo de observación libre en todo tratamiento.** No es un cajón de sastre: es donde
   vive lo que ningún esquema captura — *"se aplicó en el cuello porque la pierna estaba
   lastimada"*, *"medio frasco aproximadamente, se movió mucho"*. La medida se estructura;
   la narrativa se libera.
8. **`health_plan_item_id` nullable desde ya.** El cronograma se implementa en 3.5b, pero
   si el piloto corre un mes sin este campo, esos tratamientos **no se pueden enlazar
   retroactivamente** y nadie podrá decir después si aquella vacuna fue de calendario o por
   enfermedad. Cuesta nada hoy, es irrecuperable mañana (ADR-0016).
9. `TreatmentCourse`: un tratamiento de 3 días es **una** serie con sus aplicaciones, no
   tres eventos sueltos e inconexos.
10. Vacunación como camino propio en la app, separado de tratamiento.
11. Pantalla de campo: vía y motivo en la misma pasada, sin sumar toques al caso normal.

Pruebas: dosis con valor y **sin** unidad rechazada; dosis ausente **aceptada** (con o sin
observación); dosis por peso resuelta contra el último pesaje, y rechazada si el animal no
tiene ninguno; dosis por peso sobre un lote usa el promedio muestral y queda marcada como
estimada; calculada ≠ administrada se persiste sin corregir ninguna de las dos; ruta
inexistente rechazada; serie de 3 días produce una serie con 3 aplicaciones y un solo
período de retiro correctamente fechado; el retiro sigue calculándose igual que antes (no
regresión de Art. 19).

---

### 3.5a.3 · `feature/livestock-mortality-causes`

*Por qué:* sin causa, la mortalidad es un número que no permite decidir nada. Con causa,
distingue "madre que aplasta" de "madre con mala leche" — que es el juicio que el cliente
hace hoy a ojo y que el §4.6 va a necesitar.

Tareas:
1. Catálogo `mortality_causes` (Art. 8): aplastamiento, inanición, débil al nacer, diarrea,
   hernia, desconocida. Ampliable desde el panel — la lista final la da el cliente (§7).
2. `DisposalType.Death` gana `cause_id`, y `GroupMortality` lo lleva también.
3. Registro de baja de lechón desde el móvil, con la madre resuelta automáticamente
   mientras la cría siga en su cohorte de lactancia.

Pruebas: baja individual con causa; baja grupal con causa; causa inexistente rechazada; la
mortalidad predestete por madre se agrega correctamente sobre datos sembrados.

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

*Por qué:* `DATA-MODEL.md` §Núcleo 3 anticipó el "bug del saco" y **nunca se implementó**:
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

Pruebas: valor dentro de rango pasa sin fricción; valor improbable exige confirmación
explícita; valor imposible se rechaza; **sin rangos configurados no se bloquea nada**
(fail-open acá es correcto: un rango olvidado no puede impedir registrar la realidad);
funciona sin red.

---

### 3.5a.7 · `feature/field-app-lot-registration`

*Por qué:* es la superficie de campo de todo lo construido en 3.5a.1. Sin ella, el lote por
conteo existe sólo en el backend.

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
§2.1 para React Native); el promedio calculado coincide con el enviado; la ficha refleja las
bajas.

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

### 3.5a.9 · `feature/field-app-herd-navigation`

*Por qué:* `EventsScreen.tsx:113-124` pinta **un `BigButton` por animal**. Con 3 vacas
funciona; con 90 cerdos es un scroll infinito. **Esto bloquea el piloto**, aparte y antes
del QR.

Tareas:
1. Búsqueda por identificador y filtro por lote en el selector de animales.
2. "Recientes": los últimos animales sobre los que este teléfono registró algo.
3. **Ocultar Ordeño** cuando ninguna especie tiene `IsMilkable`. La bandera ya existe
   (`Species.IsMilkable`); falta que la navegación la respete. Es la diferencia entre
   software a medida y software de vacas con cerdos encima.
4. Documentar el escaneo QR como el paso siguiente natural **cuando llegue el aretado** —no
   es trabajo de esta fase, y `AnimalIdentifier` ya lo soporta con tipo `RFID` (ADR-0006).

Pruebas: búsqueda con 200 animales sembrados devuelve el correcto; filtro por lote; sin
especies ordeñables la pantalla de ordeño no es alcanzable desde ninguna ruta.

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

### 4.5 · `feature/breeding-gilt-selection`

`GiltEvaluation` con criterios **configurables** (`SelectionCriterion`: conteo / escala 1–5
/ booleano), precisamente porque el cliente no recordaba todos.

Semilla propuesta, **a confirmar con él** (§7): tetas funcionales y simetría —los pezones
invertidos o ciegos no cuentan, por eso es un conteo evaluado y no el número visible—,
aplomos y calidad de pezuña, desarrollo vulvar, condición corporal, peso y edad a la
selección, temperamento, ausencia de hernias. Más dos que el sistema ya tendrá solo: **su
propio peso al nacer y el tamaño de la camada de la que salió**.

Pruebas: criterio nuevo por INSERT aparece en la evaluación sin tocar código; evaluación
incompleta no decide; el histórico de criterios de una evaluación pasada no cambia si el
catálogo cambia después.

### 4.6 · `feature/breeding-maternal-index`

Dos mitades que **no se mezclan**:

- **Conductual**: `MaternalBehaviorAssessment` **por parto**, no global (aplastamiento,
  agresividad, si deja mamar, nerviosismo al manejo). Por parto se ve tendencia; global se
  ve una etiqueta fija que nadie revisa.
- **Derivada** (calculada, **no almacenada**, coherente con `DATA-MODEL.md` §Núcleo 4):
  nacidos vivos/muertos/momias, peso promedio de camada al nacer, **mortalidad predestete
  0–24 d por madre**, destetados por parto, intervalo destete–celo.

Encima, `MaternalIndex`: puntaje compuesto con **pesos configurables**, ordenable en el
panel.

Pruebas: KPIs derivados contra datos sembrados con resultados escritos a mano; cambiar los
pesos reordena el ranking; una madre sin partos no aparece con índice 0 (aparece sin índice).

### 4.7 · `feature/tasks-swine-alerts`

1. **Destete → celo (4–7 días).** El ciclo porcino es corto e intenso. El cliente no lo
   pidió; es de los avisos más rentables que el módulo Breeding ya construido permite.
2. **Retiro en carne bloqueante.** Art. 19 hoy bloquea leche. En engorde lo que importa es
   que un lote tratado **no pueda ir a faena** antes de X días. `WithdrawalTarget.Meat` ya
   existe en `EventEnums.cs` y nadie lo usa. Es requisito legal, no comodidad.

Pruebas: alerta de celo en la ventana correcta tras el destete; **intento de disposición de
un lote en retiro de carne se rechaza**, no se advierte (mismo estándar que
`PLAN-FASE-3-4.md` §2.3 exige para leche).

---

## 5. ADRs de esta fase

| ADR | Qué decide | Cuándo se implementa |
|---|---|---|
| [0015](adr/0015-lote-por-conteo.md) | Lote por conteo, evento grupal XOR, reversibilidad al aretar | 3.5a.1 |
| [0016](adr/0016-plan-sanitario-configurable.md) | Un solo motor de cronograma por ancla + desfase + filtro | **escrito ahora**, implementado en 3.5b.1 |
| [0017](adr/0017-correccion-de-registros-de-campo.md) | Dos caminos de corrección según dónde esté el registro | 3.5a.8 |

Los tres se mergean **antes** que su código (Art. 14).

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

---

## 7. Lo que sólo el cliente puede responder

Dos catálogos quedan **deliberadamente incompletos** porque su contenido es conocimiento de
la finca, no decisión de diseño. Conviene llevarlos impresos a la próxima visita.

**A · Criterios de selección de futuras madres.** En la conversación mencionó el número de
tetas y la postura de las patas, y dijo que había más que no quedaron anotados. La propuesta
del §4.5 es un punto de partida, no una lista cerrada:

- ¿Cuántas tetas funcionales exige como mínimo? ¿Descarta por asimetría?
- ¿Qué mira exactamente en los aplomos, y cómo lo puntúa hoy (bien/regular/mal, o más fino)?
- ¿A qué edad o peso hace la selección?
- ¿Qué descarta de inmediato, sin discusión?
- ¿Mira el tamaño de la camada de la que salió la hembra?

**B · Causas de muerte de lechón.** La lista del §3.5a.3 es la estándar; la suya puede
diferir y es la que importa:

- ¿Qué causas distingue en la práctica cuando muere un lechón?
- ¿Separa "aplastamiento" de "débil que no llegó a mamar"? Esa distinción es exactamente la
  que hace útil el índice de madres del §4.6.

**C · Dos preguntas de manejo que quedaron abiertas:**

- Los 24 días, ¿son siempre 24 o varían según cómo venga la camada? (El modelo lo trata como
  parámetro configurable por especie, no como constante.)
- Cuando clasifica por peso, ¿son siempre tres grupos (pequeños/medianos/grandes) o dos según
  la cantidad? (El modelo admite N lotes; la pregunta es qué ofrecer por defecto en la app.)
