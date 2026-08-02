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
