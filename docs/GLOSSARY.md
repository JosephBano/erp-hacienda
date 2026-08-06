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

### Selección y calificación de madres

| Término (ES) | Código (EN) | Definición |
|---|---|---|
| Evaluación de futura madre | `GiltEvaluation` | Examen morfológico y productivo para decidir si una hembra pasa a reproductora. |
| Criterio de selección | `SelectionCriterion` | Cada aspecto evaluado (tetas funcionales, aplomos, desarrollo vulvar, condición corporal…), con su tipo: conteo, escala 1–5 o booleano. **Catálogo configurable**: agregar un criterio es un INSERT. |
| Teta funcional | `FunctionalTeat` | Pezón apto para amamantar. Los invertidos o ciegos no cuentan, y por eso el dato es un conteo evaluado, no el número de pezones visibles. |
| Calificación materna | `MaternalBehaviorAssessment` | Valoración conductual de una madre **en un parto concreto** (aplastamiento, agresividad, si deja mamar, nerviosismo al manejo). Por parto y no global, para ver tendencia en vez de una etiqueta fija. |
| Mortalidad predestete | `PreWeaningMortality` | Crías muertas entre el parto y el destete, atribuibles a la madre. KPI **derivado**, no almacenado. |
| Índice de madre | `MaternalIndex` | Puntaje compuesto y ordenable que combina KPIs derivados y calificación conductual, con **pesos configurables**. |

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
