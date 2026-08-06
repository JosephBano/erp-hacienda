# GLOSSARY.md — Lenguaje ubicuo del dominio

> Regla (Art. 20): el dominio se piensa en **español**, el código se escribe en **inglés**.
> Esta tabla es el puente. Un concepto nuevo entra **aquí primero**, luego al código.
> Los agentes de IA no inventan sinónimos: usan estos términos exactos.

## Núcleo — Animales e identidad

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Animal | `Animal` | Entidad viva individual de cualquier especie. Nace con UUID interno. |
| Especie | `Species` | Bovino, porcino, equino, etc. **Dato configurable**, no enum. |
| Raza | `Breed` | Raza dentro de una especie (Holstein, Brahman, Landrace…). Configurable. |
| Categoría | `AnimalCategory` | Etapa/uso del animal (ternera, vacona, vaca en producción, vaca seca, lechón, cerdo de engorde, reproductora…). Configurable por especie. |
| Identificación | `AnimalIdentifier` | ID externo adjunto al animal, con tipo, valor y vigencia (fecha asignación/retiro). |
| Código de manejo | `FarmTag` | Identificador interno de la finca (arete propio, nombre, marca). |
| Arete oficial / SIFAE | `OfficialTag` / `SifaeId` | Identificación oficial Agrocalidad. Puede no existir o llegar tarde. |
| Ficha del animal | `AnimalRecord` | Vista completa: datos + historial de eventos + genealogía. |
| Baja | `Disposal` | Salida definitiva: venta, muerte, robo, descarte. Nunca borra el registro. |

## Historial y eventos

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Evento | `AnimalEvent` | Hecho fechado e inmutable en la vida de un animal o grupo. Base de todo el historial. |
| Pesaje | `WeighingEvent` | Registro de peso en fecha. |
| Tratamiento | `TreatmentEvent` | Aplicación de medicamento: producto, dosis, vía, costo, veterinario. |
| Vacunación | `VaccinationEvent` | Aplicación de vacuna (p. ej., campaña de aftosa). |
| Período de retiro | `WithdrawalPeriod` | Días post-tratamiento en que leche/carne **no** puede venderse. Bloqueante. |
| Diagnóstico | `DiagnosisEvent` | Enfermedad o condición detectada. |
| Movimiento | `MovementEvent` | Cambio de grupo o de potrero. |
| Corrección | `CorrectionEvent` | Evento que corrige a otro (lo referencia); el original nunca se edita. |

## Reproducción y genética

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Servicio | `BreedingService` | Intento de preñar: monta natural o inseminación artificial (IA). |
| Inseminación artificial | `ArtificialInsemination` | Servicio con material genético (pajuela). |
| Pajuela | `SemenStraw` | Dosis de semen: toro de catálogo, raza, proveedor, lote. Puede ser "padre" genealógico. |
| Diagnóstico de preñez | `PregnancyCheck` | Confirmación (palpación/eco) tras el servicio. |
| Gestación | `Pregnancy` | Estado activo entre confirmación y parto. Duración parametrizada por especie. |
| Parto | `Birthing` (`Farrowing` en cerdas, `Calving` en vacas) | Evento que **crea** crías enlazadas a madre y padre. |
| Camada | `Litter` | Conjunto de crías de un parto (clave en porcinos): vivos, muertos, momias. |
| Destete | `Weaning` | Separación de la cría; cierra el ciclo de la madre. |
| Días abiertos | `DaysOpen` | Días entre parto y nueva concepción. KPI de fertilidad. |
| Intervalo entre partos | `CalvingInterval` | KPI reproductivo clave por madre. |
| Árbol genealógico | `Pedigree` | Ancestros/descendientes; el padre puede ser `Animal` o `SemenStraw`. |
| Buena madre | — (KPI compuesto) | Ranking por datos: intervalo, servicios/concepción, destetados/año, peso camada. |

## Grupos, potreros y alimentación

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Grupo / Lote (de animales) | `AnimalGroup` | Conjunto de manejo (vacas en ordeño, engorde marzo-2026). Membresía con historial. |
| Potrero | `Paddock` | División física de pasto. Tiene área y aforo. |
| Rotación | `GrazingRotation` | Movimiento de grupos entre potreros con fechas y descansos. |
| Aforo | `ForageAssessment` | Medición de pasto disponible en un potrero. |
| Carga animal | `StockingRate` | Animales (o UGM) por hectárea. |
| Consumo de grupo | `GroupFeedConsumption` | Alimento asignado a un grupo en un período; se prorratea a costo por animal-día. |
| Pienso / Balanceado | `Feed` / `CompoundFeed` | Alimento comprado; es un ítem de inventario con lote y vencimiento. |

## Producción, inventario y transformación

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Ordeño | `MilkingSession` | Registro de leche por sesión (vaca o grupo + fecha + litros). |
| Producción de leche | `MilkYield` | Litros por vaca/día; alimenta curvas de lactancia. |
| Lactancia | `Lactation` | Período productivo entre parto y secado; unidad de análisis lechero. |
| Calidad de leche | `MilkQualityTest` | CMT/mastitis, sólidos, células somáticas; puede afectar precio de venta. |
| Ítem de inventario | `InventoryItem` | Cualquier bien: insumo, medicamento, producto, subproducto. |
| Lote de inventario | `InventoryBatch` | Partida con cantidad, costo, vencimiento (¡distinto de `AnimalGroup`!). |
| Producto | `Product` | Bien vendible (leche cruda, queso fresco, cerdo en pie…). Configurable. |
| Subproducto | `ByProduct` | Salida secundaria de una transformación (suero, estiércol). |
| Receta / BOM | `BillOfMaterials` | Definición de transformación: insumos → productos + subproductos + merma. |
| Orden de transformación | `TransformationOrder` | Ejecución de una BOM en fecha, con costos reales distribuidos. |
| Merma | `ProcessLoss` | Pérdida esperada/real en una transformación. |

## Comercial, contabilidad y personas

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Venta | `Sale` | De productos **o de animales** (la venta de un animal genera su baja). |
| Compra | `Purchase` | Adquisición a proveedor; alimenta inventario y cuentas por pagar. |
| Proveedor / Cliente | `Supplier` / `Customer` | Terceros comerciales. |
| Asiento contable | `JournalEntry` | Registro contable emitido por los módulos hacia Contabilidad. |
| Centro de costo | `CostCenter` | Eje de análisis (lechería, porcinos, quesería, turismo…). |
| Costo por animal-día | `CostPerAnimalDay` | Resultado del prorrateo de costos de grupo. |
| Empleado | `Employee` | Persona con rol, permisos y (a futuro) datos laborales IESS. |
| Rol | `Role` | Conjunto configurable de permisos (mayordomo, ordeñador, veterinario, admin). |
| Tarea / Alerta | `Task` / `Alert` | Acción pendiente generada por reglas (vacunar, palpar, fin de retiro, vencimiento). |

## Sincronización móvil fuera de línea (Offline Sync)

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Bandeja de salida local | `SyncOutbox` | Cola local en el cliente móvil donde se registran las operaciones pendientes de sincronización. |
| Identificador de operación cliente | `ClientOperationId` | UUIDv4 generado en el móvil para garantizar la idempotencia estricta en el backend. |
| Tirón incremental | `SyncPull` | Obtención incremental de cambios desde la BD del servidor basada en cursor. |
| Empuje de lote | `SyncPush` | Envío en lote de operaciones offline desde el móvil hacia el backend (`Accepted`, `Duplicate`, `Rejected`). |
| Cursor de sincronización | `Cursor` | Marca de tiempo y UUID de desempate para sincronizar diferencialmente la información sin duplicados. |
| Borrado lógico / lápida | `Tombstone` | Fila marcada `deleted_at` (Art. 1: nunca se borra físicamente) que el pull entrega una vez con `isDeleted: true` para que cada cliente la retire de su base local. |
| Última escritura gana | `LWW` (*Last-Write-Wins*) | Estrategia de resolución para entidades editables (ADR-0008): entre dos ediciones concurrentes del mismo campo, gana la que declare el `occurredAt` más tardío — no la que llegue primero al servidor. |
| Momento de última edición | `LastEditedAt` | Marca de tiempo declarada por el dispositivo, distinta de `UpdatedAt` (tiempo de procesamiento del servidor); es la que LWW compara para decidir quién gana. |
| Bitácora de conflictos | `SyncConflict` | Registro inmutable de cada campo donde una edición se superpuso a otra ya aplicada: valor que quedó, valor que se intentó, y quién ganó. Auditable en el panel (`GET /api/v1/sync/conflicts`), nunca editable. |

## Capacidades configurables por especie (Art. 8)

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Especie ordeñable | `is_milkable` (`Species.IsMilkable`) | Bandera booleana por especie que indica si el field-app permite registrar ordeños para sus animales. **Configuración, no código**: agregar una especie nueva no requiere tocar el código de dominio. Default `false` (fail-closed) — una especie recién registrada no es ordeñable hasta que un operador la habilita explícitamente desde el panel. Ver `SpeciesConfiguration` (backend) y `services/herdQueries.loadHerd` (field-app). |
| Especie con retiro de leche bloqueante | `WithdrawalTarget.Milk` / `WithdrawalTarget.Both` | La leche de un animal bajo período de retiro (medicamento o) no es vendible. Aplica al `MilkingSession` sin importar si la especie es ordeñable. |

## Adaptación porcina (Fase 3.5)

> Términos que entran con el pivote a porcinos. **Ninguno está implementado todavía**:
> esta sección existe porque el Art. 20 exige que el término entre al glosario antes que
> al código. Ver `PLAN-FASE-3-5-PORCINO.md` y los ADR-0015/0016/0017.

### Lote, conteo y trazabilidad

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Modo de seguimiento | `TrackingMode` | Cómo un `AnimalGroup` conoce a sus miembros: `Individual` (cada cabeza es un animal identificable) o `Headcount` (el lote sabe *cuántos* hay, no *cuáles*). Configuración del grupo, no de la especie. |
| Lote por conteo | `HeadcountLot` | `AnimalGroup` en modo `Headcount`. Los eventos se registran **al lote**, no al animal. Es el modelo del engorde porcino antes del aretado (ADR-0015). |
| Cabezas vivas | `LiveHeadCount` | Miembros activos del lote menos las bajas registradas al lote. Cantidad **derivada**, nunca un contador que se edita a mano. |
| Estado individual indeterminado | `IndeterminateIndividualState` | Condición de un animal cuyo lote actual está en modo `Headcount`: el sistema sabe que entró al lote y no sabe si sigue vivo. Se declara explícitamente en las consultas; no se disimula. |
| Cohorte de lactancia | `NursingCohort` | Conjunto de camadas nacidas en días consecutivos que se manejan juntas con sus madres. Su destete se calcula desde la **última** camada: `max(fecha_parto) + días_de_lactancia`. |
| Clasificación por peso | `WeightSorting` | Reparto de una cohorte destetada en lotes de engorde por tamaño (pequeños / medianos / grandes). Es el momento en que termina la identificación individual. |
| Pesaje muestral | `SampleWeighing` | Pesaje de una muestra del lote, no del total. Payload `{sample_count, avg_kg, min_kg, max_kg}`. Un promedio de 10 sobre 42 cabezas es un dato honesto; inventar 42 pesos no lo es. |
| Diagnóstico grupal | `GroupDiagnosis` | *"En este lote hay uno enfermo."* Cantidad de cabezas afectadas y condición, **sin identificar cuál** — que es exactamente lo que el encargado sabe y lo que quiso decir. |
| Disolución de lote | `LotDissolution` | Cierre en bloque de las membresías restantes cuando un lote por conteo llega a cero cabezas. Las bajas parciales no cierran a nadie; sólo bajan el conteo. |
| Baja con alcance de lote | `LotScopedDisposal` | Baja de un animal cuyo destino individual nadie verificó: salió como parte de la disposición de su lote. Se muestra como tal en la ficha, nunca como una venta común. |
| Aretado masivo | `BulkTagging` | Asignación de identificadores a los animales de un lote. Sobre animales ya mezclados **está prohibido**: elegir qué fila es qué cerdo es el dato sintético que el ADR-0015 existe para evitar. |
| Cerda | `Sow` | Hembra porcina reproductora en producción. |
| Futura madre / Cerda de reemplazo | `Gilt` | Hembra seleccionada como reproductora que aún no ha parido. |
| Lechón | `Piglet` | Cría porcina hasta el destete. |
| Cerdo de engorde | `Grower` / `Finisher` | Porcino en fase de crecimiento/terminación, destinado a faena. |

### Sanidad, tratamientos y cronograma

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Vía de administración | `AdministrationRoute` | Cómo se aplicó el producto: oral en agua, oral en alimento, intramuscular, subcutánea, tópica, intranasal, intrauterina. **Catálogo configurable** (Art. 8), no enum. |
| Motivo del tratamiento | `TreatmentReason` | Por qué se aplicó: `Scheduled` (tocaba por cronograma), `Curative` (el animal está enfermo), `Preventive` (profilaxis fuera de cronograma). Distinguirlos es lo que separa "vacuna de calendario" de "vacuna porque se enfermó". |
| Serie de tratamiento | `TreatmentCourse` | Tratamiento de varios días como **una** unidad con sus aplicaciones, no como N eventos sueltos e inconexos. |
| Forma de la dosis | `DoseKind` | `Absolute` (10 ml), `PerWeight` (1 ml / 10 kg, resuelta contra el último pesaje) o `PerHead` (1 dosis × 42 cabezas). En porcinos la dosis por peso es la norma, por la misma razón que la ración: un animal enfermo pesa menos. |
| Dosis calculada vs. administrada | `CalculatedDose` / `AdministeredDose` | La que el sistema sugiere (estimada, si sale de un promedio muestral) y la que realmente salió del frasco. **Se guardan las dos**: su diferencia delata derrame, subdosificación o un muestreo de peso equivocado. |
| Observación del tratamiento | `TreatmentNotes` | Texto libre donde vive lo que ningún esquema captura ("se aplicó en el cuello porque la pierna estaba lastimada"). La medida se estructura; la narrativa se libera. |
| Plan sanitario / de manejo | `HealthPlan` | Cronograma configurable de vacunas, tratamientos y procedimientos, aplicable a un lote o a un individuo (ADR-0016). |
| Ítem de plan | `HealthPlanItem` | Una línea del plan: qué se hace, anclado a qué (`nacimiento` \| `inicio de lote` \| `parto` \| `destete`), a cuántos días, con qué ventana de cumplimiento, y para qué especie/categoría/sexo. |
| Ancla del plan | `PlanAnchor` | El hecho desde el cual se cuentan los días de un ítem. Que sea dato y no código es lo que permite que castración y preselección de madres vivan en el mismo motor que las vacunas. |
| Ventana de cumplimiento | `ComplianceWindow` | Días de tolerancia alrededor de la fecha teórica antes de que el ítem cuente como vencido. |
| Causa de muerte | `MortalityCause` | Catálogo configurable (aplastamiento, inanición, débil al nacer, diarrea, hernia, desconocida). Sin causa la mortalidad es un número que no permite decidir nada. |
| Retiro en carne | `WithdrawalTarget.Meat` | Días post-tratamiento en que el animal **no puede ir a faena**. Ya existe en el enum y nunca se usó; en engorde porcino es el retiro que importa (Art. 19). |

### Alimentación

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Etapa de alimento | `FeedStage` | Preiniciador, iniciador, crecimiento, engorde, gestación, lactancia. Clasifica un ítem de inventario de categoría `Feed`. |
| Presentación | `PackagePresentation` | Cómo se compra un ítem (saco de 20 kg, saco de 40 kg). Distinta de la unidad base en que se consume. |
| Conversión de unidad | `UnitConversion` | Factor entre presentación y unidad base (`saco40kg` → 40 `kg`). Evita el "bug del saco": comprar en sacos y consumir en kilos sin que los números mientan. |
| Estándar de alimentación | `FeedingStandard` | Ración diaria esperada según peso y etapa: `{especie, etapa, peso_desde, peso_hasta, ración_kg_día}`. Es **dato configurable**, jamás un `if` por especie (Art. 8). |
| Ración de cerda lactante | `LactatingSowRation` | Caso particular del estándar, expresado como `{base_kg, por_cría_kg, max_kg}` — p. ej. 2 kg + 0.5 kg por lechón, tope 9 kg. Tres números en una fila, no una fórmula compilada. |
| Conversión alimenticia | `FeedConversionRatio` (FCR) | Kg de alimento consumido ÷ kg de peso ganado por el lote. **El indicador que decide si el engorde va bien.** Se calcula en kg; el costo en dinero es Fase 4. |

### Características observables del animal (ADR-0018 — transversal a todas las especies)

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Característica | `AnimalTrait` | Definición configurable de un **juicio** sobre un animal: "es mansa", "patea", "se escapa del corral", "tetas funcionales". Catálogo por especie o global; agregar una es un INSERT (Art. 8). |
| Observación de característica | `TraitObservation` | Registro fechado y **firmado** del valor de una característica en un animal. Las características **se observan, no se asignan**: nunca son una columna editable sobre `Animal`. |
| Tipo de característica | `TraitKind` | `Conductual` (patea, deja mamar), `Morfológica` (tetas funcionales, aplomos), `Manejo` (abre el pestillo, no entra a la manga). Determina qué la consume, no cómo se guarda. |
| Tipo de valor | `TraitValueType` | Los **cuatro únicos** admitidos: `Booleano`, `EscalaOrdinal` (conjunto cerrado etiquetado), `ConteoAcotado` (mín/máx declarados), `TextoLibre`. **Sin unidad y sin decimal libre** — ver Guardarraíl. |
| Guardarraíl de las características | — | Regla que impide que el mecanismo se vuelva el vertedero de datos que debían estar tipados: **¿dos personas competentes, con el animal delante, obtendrían el mismo número?** Sí → medición → esquema y eventos. No → juicio → característica. Se impone estructuralmente: sin campo de unidad ni decimal libre, `peso = 35.4 kg` es **inexpresable**. |
| Disposición actual | `CurrentDisposition` | Resumen **derivado** de la serie de observaciones ("esta yegua es mansa"). Nunca almacenado, para que un cambio de conducta sea visible en vez de sobrescrito. |
| Advertencia de campo | `TraitAlert` | Característica marcada como visible, que aparece en la ficha del animal en el móvil antes de que alguien lo toque (*"PATEA"*). Transfiere el conocimiento del empleado veterano al que recién entra. |
| Contexto de la observación | `ObservationContext` | Referencia opcional al hecho durante el cual se observó (un parto, una jornada de manejo). Es lo que conserva el "en **este** parto" al generalizar. |

### Selección y calificación de madres

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Evaluación de futura madre | `GiltEvaluation` | Sesión en la que se capturan las características **morfológicas** de una hembra y se registra la decisión de pasarla a reproductora. No tiene catálogo propio: usa `AnimalTrait` (ADR-0018). |
| Teta funcional | `FunctionalTeat` | Pezón apto para amamantar. Los invertidos o ciegos no cuentan, y por eso es un **juicio** —dos personas discrepan sobre cuáles cuentan— y no una medición: va como característica morfológica de tipo `ConteoAcotado`. |
| Mortalidad predestete | `PreWeaningMortality` | Crías muertas entre el parto y el destete, atribuibles a la madre. KPI **derivado**, no almacenado. |
| Índice de madre | `MaternalIndex` | Puntaje compuesto y ordenable con **pesos configurables**, que combina KPIs derivados de eventos contables (mortalidad por causa, destetados, peso de camada) con las características conductuales genuinamente subjetivas. El aplastamiento **no** entra como característica: se cuenta desde los eventos de mortalidad con causa, que es objetivo. |

### Navegación de campo

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Árbol de actividades | `ActivityTree` | Mapa de lo que una persona hace **parada en el corral**, con el **sujeto** (animal / lote / parto) como primer nivel — el mismo XOR que `animal_events`. Distinto del plan de ramas, que enumera por módulo del backend: uno es cómo se usa, el otro cómo se construye. Se dibuja **antes** de escribir pantallas. |
| Conteo de toques | `TapBudget` | Cantidad de toques que cuesta cada actividad del árbol, contada **sobre papel** antes de implementar. Estándar: **tres para lo normal, cuatro para lo raro**. Si la implementación excede lo dibujado, se corrige el flujo, no el número. |
| Filtrado de actividades | `ActivityFiltering` | Qué ramas del árbol se muestran: **módulo encendido** ∧ capacidades de la finca (especies ordeñables, lotes por conteo) ∧ permisos del usuario. **Filtrado sobre un árbol único**, nunca un árbol por especie — eso sería el `if (especie == 'cerdo')` mudándose a la navegación (Art. 8). |

### Visibilidad de módulos (ADR-0019 — transversal)

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Módulo de la finca | `FarmModule` | Interruptor **explícito** por módulo (`key`, `enabled`, `disabled_reason`), editable desde el panel sin deploy. Lo decide el dueño, no el catálogo de datos. |
| Módulo oculto | — | Módulo apagado: su entrada desaparece de la navegación. **Nada se borra** — código, pruebas, endpoints y datos siguen intactos y en verde. Ocultar es decisión de producto, no permiso para dejar de mantener. |
| Entrada vs. camino de los datos | — | Se oculta **la puerta de entrada**, jamás **la salida de lo ya registrado**: un teléfono con ordeños sin sincronizar debe poder subirlos aunque el módulo esté apagado. Es el único punto donde esta decisión puede perder datos en silencio. |
| Capacidad de especie vs. visibilidad | `Species.IsMilkable` vs. `FarmModule` | Dos ejes distintos que estaban confundidos: `IsMilkable` es **verdad de dominio** (un cerdo no se ordeña nunca); la visibilidad es **decisión de producto** (esta finca no usa el módulo todavía). Se puede tener vacas ordeñables y el módulo apagado. |

### Corrección de registros

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Corrección de campo | `FieldCorrection` | Arreglo de un error de dedo hecho desde el móvil. Dos caminos según dónde esté el registro (ADR-0017). |
| Cancelación en bandeja | `OutboxCancellation` | La operación nunca salió del teléfono: se descarta la entrada del `SyncOutbox` y no hay nada que corregir, porque para el servidor nunca ocurrió. |
| Evento de corrección | `CorrectionEvent` | El registro ya sincronizó: se emite un evento nuevo que referencia al original vía `RelatedEventId`. El pasado no se edita (Art. 1). |
| Evento grupal | `GroupEvent` | `AnimalEvent` asociado a un `AnimalGroup` en vez de a un `Animal`. XOR: exactamente uno de los dos. Previsto en `DATA-MODEL.md` desde la Fase 1 y aún sin implementar. |

## Plataforma móvil — términos técnicos del field-app

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Tabla espejo | `Mirror table` | Tabla WatermelonDB que solo recibe datos vía pull (animales, especies, productos, retiros). Nunca la fuente primaria de verdad — esa vive en el server. |
| Tabla local | `Local table` | Tabla WatermelonDB que el device crea y sincroniza (outbox, milk_yields, sync_meta). El server confirma o rechaza. |
| Cleartext por Tailscale | `network_security_config.xml` (Android) | Configuración que permite HTTP plano **solo** para `100.101.240.44` (IP Tailscale del backend de la laptop del desarrollador). Cualquier otro host sigue forzado a HTTPS. Existe porque Android 9+ bloquea cleartext por default; si el backend pasa a público, este archivo se elimina. |
| Orden de plugins de Babel | `babel.config.js` | WatermelonDB usa `declare` en los modelos y requiere `@babel/plugin-transform-typescript` (con `allowDeclareFields: true`) **antes** de `@babel/plugin-proposal-decorators` y `@babel/plugin-transform-class-properties`. Si el orden está mal, sale con "Decorating class property failed". |

## Legal Ecuador (referencias)

| Término | Qué es |
|---|---|
| Agrocalidad | Agencia de regulación fito/zoosanitaria. Registro de predio, identificación animal, campañas. |
| SIFAE | Sistema de Información Bovina del Ecuador (identificación y trazabilidad oficial). |
| Guía de movilización | Permiso zoosanitario para transportar animales. Adjuntable a un `MovementEvent`/venta. |
| ARCSA | Autoridad sanitaria para alimentos procesados (aplica al hacer queso/cárnicos). |
| SRI | Administración tributaria; facturación electrónica obligatoria. |
| IESS | Seguridad social; afiliación obligatoria de empleados desde el día uno. |
| LOPDP | Ley Orgánica de Protección de Datos Personales; aplica a datos de empleados/clientes. |
