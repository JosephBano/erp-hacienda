# spec.md — Fase 3.5: Adaptación porcina

> **Qué es este documento.** Decide *qué* se construye en la Fase 3.5 y *por qué*, y fija
> las decisiones que gobiernan toda la fase. El bloque de captura (3.5a) — sus nueve ramas,
> con su numeración de tarea citada 85 veces desde el código — vive en
> [`spec-3.5a.md`](./spec-3.5a.md) porque juntos no entraban en ~400 líneas. El *cómo* y el
> *cuándo* de cada rama van en [`plan.md`](./plan.md), el checklist ejecutable en
> [`tasks.md`](./tasks.md), la verificación manual en [`test-e2e.md`](./test-e2e.md).
>
> **Por qué esta subcarpeta.** El trabajo de esta fase no es una sola rama: son ~17 ramas de
> feature (3.5a.0 a 3.5a.9, 3.5b.1 a 3.5b.7) que comparten un mismo objetivo, un mismo
> conjunto de decisiones (sec. 2) y un mismo criterio de salida. Repartir eso en documentos
> sueltos de `docs/planes/` los deja huérfanos entre sí — de ahí la carpeta.
>
> **Este documento reemplaza a `PLAN-FASE-3-5-PORCINO.md`** como fuente citable desde el
> código. El original permanece en `docs/planes/` sin editarse: es la fuente de este traslado
> y el commit 11 de esta rama reapunta las 85 citas del código hacia esta carpeta.

- **Fase del ROADMAP:** Fase 3.5 — Adaptación porcina (`docs/ROADMAP.md:146-217`), **en
  curso**, insertada 2026-08-05.
- **ADRs vigentes que respalda:** ADR-0015, ADR-0016, ADR-0017, ADR-0018, ADR-0019,
  ADR-0021, ADR-0024, ADR-0026 (ver sec. 5).
- **Reglas duras que gobiernan este trabajo:** Art. 1 (historia inmutable, corrección con
  evento nuevo), Art. 4 (el cumplimiento es el evento), Art. 6 (contrato público entre
  módulos, cero SQL cruzado), Art. 8 (filtrado por capacidad, nunca `if (especie == ...)`),
  Art. 9 (evaluación local sin red), Art. 10 (toda cantidad física lleva su unidad), Art. 11
  (una fase cierra cuando alguien la usa de verdad), Art. 12 (CI en rojo bloquea merge),
  Art. 14 (los ADRs mergean antes que su código), Art. 19 (retiro bloqueante, no advertencia).

---

## Índice

1. [Por qué existe esta fase](#1-por-qué-existe-esta-fase)
2. [Las decisiones que gobiernan el plan](#2-las-decisiones-que-gobiernan-el-plan)
3. [Bloque 3.5a — Captura](./spec-3.5a.md) (documento aparte)
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
con la comida**. Ese "si" es medible y tiene nombre: conversión alimenticia (spec-3.5a.md
sec. 4.4 del plan original, sección 4.4 de este bloque más abajo).

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

La fase se parte en dos bloques. **El piloto real puede arrancar con un sub-conjunto de
3.5a mergeado a develop** (sub-criterio "Para abrir el piloto real", en `spec-3.5a.md`),
siempre que la deuda restante quede documentada como tal — ver
[ADR-0024](../../adr/0024-pilot-decoupling-from-3-5a.md). El desacople es deliberado: no
hay valor en dejar al cliente sin sistema mientras la UI del sujeto "lote" (3.5a.7.1–5)
termina de implementarse, y la conversación de frecuencias con el cliente (sec. 7-C de
este documento) puede ocurrir en paralelo al uso real:

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

### 2.9 Adelantos de Fase 4 que viven aquí

Cinco piezas de Fase 4 se construyeron dentro de la 3.5 porque el piloto no podía esperar a
Purchasing. Cada una tiene su vida útil declarada en el propio código, no sólo en este
documento — el día que se abra Purchasing, ésta es la lista de qué jubilar.

| Adelanto | Dónde vive | Contrato de caducidad declarado |
|---|---|---|
| Recepción de inventario pre-Purchasing (ADR-0026, PR #94) | `InventoryBatch.SupplierLabel`, `InvoiceReference`, `ReceivedAt` en `src/Modules/Inventory/Hato.Modules.Inventory.Domain/InventoryItem.cs:186-234` | "Deprecado cuando llegue Purchasing (Fase 4)" — comentario junto a `ReceivedAt` (línea 99 y adyacentes) |
| Aviso en la UI de admin-web | `clients/admin-web/src/app/components/inventory-batches-section/inventory-batches-section.component.ts:25` | El banner de la pantalla le dice al usuario: *"Cuando llegue Purchasing (Fase 4), este flujo se reemplazará por 'Recibir orden de compra'"* |
| Evento de recepción sin outbox | `src/Modules/Inventory/Hato.Modules.Inventory.Domain/Events/InventoryReceptionRecorded.cs:10` | "Published in-process via MediatR ...; no outbox — reopens if Fase 4 (Purchasing) introduces a durable downstream consumer" |
| FK cross-schema diferida (`AnimalEvent.BatchId`, hacia `inventory_batches`; y hacia `people.users`) | `src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalEvent.cs:78` (comentario del campo) y `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Configurations/AnimalEventConfiguration.cs:51` | "Las FKs estrictas llegan en Fase 4 cuando la arquitectura decida cómo conectar módulos" |
| Subida de fotos en tratamientos | `clients/field-app/src/services/eventService.ts:163` | "Uploading needs the attachments module (Fase 4). Saying so explicitly keeps the app from implying a picture is safely on the server when it is not" |

Las cinco comparten el mismo patrón: el dato correcto ya se captura hoy (proveedor, factura,
fecha, foto local, la relación entre evento y lote), pero el mecanismo que lo vuelve
consistente a nivel de plataforma (orden de compra real, outbox durable, FK estricta entre
esquemas, almacenamiento remoto de adjuntos) es explícitamente de Fase 4. Ninguna de las
cinco bloquea el piloto; todas quedan documentadas para no perderse cuando Purchasing abra.

---

## 4. Bloque 3.5b — Análisis y automatización

> Se construye **mientras el campo ya carga datos de 3.5a**. Cada ítem se calibra contra
> datos reales, no contra la tabla en papel. Ningún número de esta sección está citado desde
> el código a la fecha de este traslado (2026-08-16); por eso vive junto al resto de la
> narrativa en este archivo y no en un documento aparte.

### 4.1 · `feature/livestock-health-plans` · **estructural** (ADR-0016)

`HealthPlan` + `HealthPlanItem` con ancla (`Nacimiento` | `InicioDeLote` | `Parto` |
`Destete`), desfase en días, ventana de cumplimiento y filtro por especie/categoría/**sexo**.
Asignable a un lote o a un individuo.

El filtro por sexo es lo que colapsa tres pedidos del cliente en un solo motor: la
castración de machos y la preselección de futuras madres **no son "otro cronograma"**, son
ítems con `applies_to_sex`. El cumplimiento **es el evento** (Art. 4): no hay botón de
"marcar como hecho" separado del registro del hecho.

Tareas: 1) tabla `HealthPlan`/`HealthPlanItem` con ancla, desfase y ventana; 2) filtro por
especie/categoría/sexo; 3) asignación a lote o a individuo; 4) resolución de fecha teórica
por ancla; 5) cumplimiento derivado del evento que lo referencia, sin botón separado.

### 4.2 · `feature/tasks-health-plan-alerts`

Quinto generador `HEALTH_PLAN_ITEM_DUE` en `GenerateAlertsCommand.cs`, copiando el patrón de
los cuatro existentes: leer por contrato público (Art. 6), `AlertAlreadyActiveAsync` para no
duplicar, crear. Cero SQL cruzado entre esquemas.

Tareas: 1) generador `HEALTH_PLAN_ITEM_DUE`; 2) `AlertAlreadyActiveAsync` contra este tipo;
3) creación al entrar en ventana de cumplimiento.

### 4.3 · `feature/inventory-feeding-standards`

La tabla peso/día del cliente como filas configurables `{especie, etapa, peso_desde,
peso_hasta, ración_kg_día}`. La ración de la cerda lactante es
`{base_kg: 2, por_cría_kg: 0.5, max_kg: 9}` — **tres números en una fila, jamás un `if`**
(Art. 8).

Salida útil en la app: *"este lote pesa ~35 kg promedio × 42 cabezas → 75.6 kg/día ≈ 2 sacos
de 40 kg"*.

Tareas: 1) tabla `feeding_standards` configurable; 2) ración de cerda lactante con tope;
3) salida en la app con el cálculo de sacos.

### 4.4 · `feature/analytics-lot-fcr`

**Conversión alimenticia por lote**: kg de alimento ÷ kg ganados. Sale gratis de lo que
3.5a ya captura (consumo por lote + pesajes muestrales).

**Es el número que decide el punto 6 del cliente.** Dijo que invertiría en el aretado si el
sistema demuestra resultados con la comida; el FCR *es* esa demostración. En **kg, no en
dinero** — la plata es Fase 4, y el FCR en kg está completo sin contabilidad.

Añadir: alerta cuando el consumo real del lote **diverge del estándar**, que es señal
temprana de enfermedad y el motivo real por el que el cliente cuenta los sacos.

Tareas: 1) cálculo de FCR por lote a partir de consumo y pesajes ya capturados; 2) alerta de
divergencia contra el estándar de 4.3, con umbral configurable.

### 4.5 · `feature/livestock-animal-traits` · **estructural** (ADR-0018) — **split en 3.5b.5-A, 3.5b.5-B y 3.5b.5-C**

*Por qué:* la evaluación de futuras madres y la calificación materna eran dos subsistemas
distintos, y el segundo (`MaternalBehaviorAssessment`) era **un `if (especie == 'cerdo')`
disfrazado de tabla**. Ambos son el mismo mecanismo: un juicio tipado, fechado y firmado
sobre un animal. Dos ramas planificadas se vuelven una, y más chica.

| Sub-rama | Alcance principal | Depende de | Rama Git |
|---|---|---|---|
| [`3.5b.5-A`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-A.md) | Núcleo: `animal_traits`, `trait_kinds`, `trait_value_types`, `trait_observations`. Cuatro tipos de valor y nada más. `CurrentDisposition` derivado. | — | `feature/livestock-animal-traits-core` |
| [`3.5b.5-B`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-B.md) | Absorción de `SelectionCriterion` y drenaje de `MaternalBehaviorAssessment`. Semilla de las morfológicas porcinas. | A | `feature/livestock-selection-criterion-deprecation` |
| [`3.5b.5-C`](../sub_planes/PLAN-FASE-3-5-PORCINO-3.5b.5-C.md) | Advertencias visibles en la ficha del animal + versionado por clonado de las definiciones usadas. | A (recomendada B) | `feature/livestock-trait-alerts-and-versioning` |

Tareas resumidas: 1) `AnimalTrait` + `TraitObservation`, `kind` ∈ {Conductual, Morfológica,
Manejo}; 2) "se observan, no se asignan" — `CurrentDisposition` derivado, nunca columna
editable; 3) `contexto` opcional; 4) cuatro tipos de valor y nada más, sin unidad ni decimal
libre (guardarraíl estructural); 5) absorbe `SelectionCriterion`, drena
`MaternalBehaviorAssessment`; 6) `visible_como_advertencia` en la ficha móvil; 7) una
definición usada se versiona, no se edita (ADR-0018 sec. 9).

### 4.6 · `feature/breeding-maternal-index`

Ahora **consumidor** de 4.5, no dueño de su propia tabla de conductas.

- **Derivada de eventos contables** (calculada, **no almacenada**): nacidos vivos/muertos/
  momias, peso promedio de camada al nacer, **mortalidad predestete 0–24 d por madre**,
  destetados por parto, intervalo destete–celo.
- **Características conductuales** de 4.5, sólo las genuinamente subjetivas.

El criterio del ADR-0018 sec. 3 corrigió este diseño: el aplastamiento de crías es una
**medición** (evento de mortalidad con causa, 3.5a.3), no una calificación conductual —
"esta cerda es torpe con las crías" **se deriva** contando esos eventos. Sólo lo
irreductiblemente subjetivo (¿deja mamar?, ¿es agresiva al manejo?) pasa por
características. El índice queda **más objetivo** que en el diseño anterior, no menos.

`MaternalIndex`: puntaje compuesto con **pesos configurables** sobre ambas mitades,
ordenable en el panel. Que los pesos sean configurables es lo que permite calibrar conceptos
ambiguos **usándolos**, que es la única forma de calibrarlos bien.

### 4.7 · `feature/tasks-swine-alerts`

1. **Destete → celo (4–7 días).** El ciclo porcino es corto e intenso. El cliente no lo
   pidió; es de los avisos más rentables que el módulo Breeding ya construido permite.
2. **Retiro en carne bloqueante.** Art. 19 hoy bloquea leche. En engorde lo que importa es
   que un lote tratado **no pueda ir a faena** antes de X días. `WithdrawalTarget.Meat` ya
   existe en `EventEnums.cs` y nadie lo usa. Es requisito legal, no comodidad.

---

## 5. ADRs de esta fase

| ADR | Qué decide | Cuándo se implementa |
|---|---|---|
| [0015](../../adr/0015-lote-por-conteo.md) | Lote por conteo, evento grupal XOR, reversibilidad al aretar | 3.5a.1 |
| [0016](../../adr/0016-plan-sanitario-configurable.md) | Un solo motor de cronograma por ancla + desfase + filtro | implementado en 3.5b.1 |
| [0017](../../adr/0017-correccion-de-registros-de-campo.md) | Dos caminos de corrección según dónde esté el registro | 3.5a.8 |
| [0018](../../adr/0018-caracteristicas-observables-del-animal.md) | Un solo mecanismo para los juicios sobre un animal, con guardarraíl estructural | 3.5b.5-A/B/C |
| [0019](../../adr/0019-visibilidad-de-modulos.md) | Los módulos se ocultan por interruptor explícito y **nunca se borran** | 3.5a.9-A |
| [0021](../../adr/0021-cierre-retroactivo-compuerta-3-5a-9-B.md) | Cierre retroactivo parcial de la compuerta sec. 2.3 para 3.5a.9-B | 3.5a.9-B |
| [0024](../../adr/0024-pilot-decoupling-from-3-5a.md) | Desacople del inicio del piloto real del cierre completo de 3.5a | gobierna sec. 2.2 |
| [0026](../../adr/0026-recepcion-inventario-minima-pre-purchasing.md) | Recepción de inventario mínima, pre-Purchasing | mergeada, PR #94 (ver sec. 2.9) |

Los ADRs se mergean **antes** que su código (Art. 14).

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
| Las características se vuelven el vertedero de datos que debían estar tipados | Aparece una característica con unidad, o alguien pide "un número libre" | El guardarraíl es estructural: cuatro tipos de valor, sin unidad ni decimal libre, con un test que fija la invariante. La petición misma es la señal de que ese dato va al esquema (ADR-0018 sec. 4). |
| El catálogo de características se llena y nadie observa nada | Definiciones sin observaciones al cerrar la fase | Reducir a lo que demostró valor —probablemente sólo las advertencias visibles— y calcular el índice materno con KPIs derivados de eventos (condición de reversa del ADR-0018). |
| La app se vuelve un menú de botones y muere la promesa de los 3 toques | Una actividad nueva se resuelve "agregando un botón al inicio" | La compuerta de sec. 2.3: el árbol se dibuja y los toques se cuentan **antes** de escribir pantallas. De 4 actividades a más de 15 no se sobrevive improvisando la navegación. |
| **Ordeño se pudre mientras está oculto** | Nadie lo ejercita a mano; un defecto no cubierto por pruebas vive ahí meses | Su suite sigue corriendo en CI igual que antes y en rojo bloquea el merge (Art. 12, ADR-0019 sec. 7). Al reencenderlo se trata como feature que vuelve a producción, no como un interruptor inocuo. |
| Se pierden ordeños pendientes al apagar el módulo | Un teléfono con registros de leche sin sincronizar | ADR-0019 sec. 4: se oculta la entrada, **nunca el camino de los datos**. Los endpoints siguen aceptando y el motor de sync sigue empujando lo que ya se registró. Cubierto por prueba en 3.5a.9-A. |

---

## 7. Lo que sólo el cliente puede responder

Cuatro cosas quedan **deliberadamente incompletas** porque su contenido es conocimiento de
la finca, no decisión de diseño. Conviene llevarlas impresas a la próxima visita.

> **A y B no bloquean nada**: son filas de catálogo (ADR-0018 y `mortality_causes`), así que
> el sistema se construye sin ellas y se llenan cuando él las dé. Preguntarlas temprano sirve
> para que la semilla inicial no sea inventada.
>
> **C sí bloquea**: sin las frecuencias no se cierra el árbol de actividades, y sin el árbol
> no arranca 3.5a.7 (compuerta de sec. 2.3).

**A · Criterios de selección de futuras madres.** En la conversación mencionó el número de
tetas y la postura de las patas, y dijo que había más que no quedaron anotados. La propuesta
del sec. 4.5 es un punto de partida, no una lista cerrada:

- ¿Cuántas tetas funcionales exige como mínimo? ¿Descarta por asimetría?
- ¿Qué mira exactamente en los aplomos, y cómo lo puntúa hoy (bien/regular/mal, o más fino)?
- ¿A qué edad o peso hace la selección?
- ¿Qué descarta de inmediato, sin discusión?
- ¿Mira el tamaño de la camada de la que salió la hembra?

**B · Causas de muerte de lechón.** La lista de 3.5a.3 (`spec-3.5a.md`) es la estándar; la
suya puede diferir y es la que importa:

- ¿Qué causas distingue en la práctica cuando muere un lechón?
- ¿Separa "aplastamiento" de "débil que no llegó a mamar"? Esa distinción es exactamente la
  que hace útil el índice de madres del sec. 4.6.

**C · Frecuencia real de cada actividad**, que es lo que ordena el árbol de sec. 2.3. Esta sí
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
