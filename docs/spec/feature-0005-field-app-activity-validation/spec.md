# spec.md — Validaciones de animales por actividad, offline y en servidor

> **Estado:** propuesta investigada, **no implementada**. La carpeta tiene los cuatro
> documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en qué
> orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual). Ningún criterio de producción
> queda cerrado por existir estos documentos.
>
> **Propósito único de la carpeta:** impedir registros biológicamente imposibles o contra un
> sujeto no apto para la actividad.

- **Rama documental:** `feature/field-app-production-specs`, base `0faf483`.
- **Fecha:** 2026-09-07. **Fase:** 3 / 3.5, estabilización de captura.
- **Reglas:** AGENTS.md 1, 3, 5, 6, 7, 8 y 10.
- **ADRs:** [0005](../../adr/0005-offline-first-movil.md),
  [0019](../../adr/0019-visibilidad-de-modulos.md),
  [0022](../../adr/0022-rangos-plausibilidad.md).

## 1. Problema

El dueño reporta machos visibles en ordeño y falta de bloqueos en la app. La revisión
confirma un caso concreto, pero no permite afirmar que todas las validaciones faltan.
El objetivo es que cada actividad ofrezca sujetos válidos, explique las restricciones
y repita las invariantes en el servidor sin depender de una pantalla concreta.

## 2. Hallazgos verificados por lectura

Rutas relativas a raíz y líneas en `0faf483`:

| Evidencia | Resultado |
|---|---|
| `clients/field-app/src/App.tsx:335` pasa `candidates={herd}`; `clients/field-app/src/screens/MilkingScreen.tsx:225` muestra todos y deshabilita solo por `speciesIsMilkable`. | Un macho de especie ordeñable puede seleccionarse. |
| `clients/field-app/src/services/milkingService.ts:84` comprueba volumen, especie y retiro; `:248` consulta la especie, no el sexo. | El servicio local no impide ese caso al evitar la UI. |
| `src/Modules/Production/Hato.Modules.Production.Application/Milking/RecordMilkingSessionCommand.cs:22` valida fecha/autor/total; `:60` revisa retiros al registrar rendimientos. | El manejador no verifica sexo ni capacidad de ordeño de la especie. |
| `src/Modules/Livestock/Hato.Modules.Livestock.Domain/Animal.cs:57` conserva `DisposedAt`, separado del borrado lógico. `src/Hato.Api/Sync/SyncPullQueries.cs:50` y `clients/field-app/src/database/models.ts:13` no lo transportan/modelan. | El móvil no dispone de ese estado en su espejo de animales. |
| `clients/field-app/src/services/herdQueries.ts:93` excluye borrados, pero no bajas; `:125` inicia la consulta de madres preñadas. | El hato general y los selectores de actividad tienen necesidades diferentes. |
| `clients/field-app/src/services/herdQueries.ts:127` y `clients/field-app/src/screens/birth/BirthScreen.tsx` ya usan preñeces activas. | El filtro reproductivo existe; no se especifica rehacer el asistente desde cero. |
| `clients/field-app/src/screens/MilkingScreen.tsx:125` evalúa rangos; `clients/field-app/src/services/milkingService.ts:92` bloquea retiro. | Hay validaciones existentes que se deben conservar y probar. |

La suite y sus límites se registran en [0004, sec. 2.1](../feature-0004-field-app-sync-reliability/spec.md#21-verificación-ejecutada-y-sus-límites).
No se reprodujo la selección de un macho sobre la APK instalada.

## 3. Decisiones fijadas para la propuesta

- **D1 — Filtrar por actividad, no reducir el hato histórico.** Un macho o una baja
  sigue siendo consultable en su expediente aunque no sea candidato a ordeño.
- **D2 — Invariantes en captura local y servidor.** Un acceso directo a API o push debe
  recibir la misma decisión de negocio; el servidor consulta Livestock por contratos.
- **D3 — Reglas por datos.** Capacidad de ordeño, categorías y rangos provienen de
  configuración. No habrá ramas por «cerdo», «vaca», raza o nombre de producto.
- **D4 — Bloquear lo imposible y confirmar lo improbable.** Falta de un rango consultivo
  conserva la política de ADR-0022. Un macho no es un valor raro que se pueda confirmar.
- **D5 — Fecha del hecho, no solo estado de hoy.** Una baja posterior no invalida por sí
  sola un pesaje real anterior registrado offline. El servidor distingue cronología y
  muestra contradicciones sin borrar hechos pendientes.

## 4. Alcance y reglas funcionales

| Actividad | Condiciones mínimas |
|---|---|
| Ordeño individual | Animal identificado internamente, sexo hembra conocido, especie habilitada para ordeño y sin baja efectiva anterior al hecho. Mantener validación de volumen y política vigente de retiros. No exigir una `Lactation` activa: su ciclo de vida no está implementado. |
| Parto | Madre hembra válida y coherente con la preñez seleccionada; respetar flujo actual de preñez activa. Padre animal o material genético, nunca ambos. No ofrecer machos como madres. |
| Pesaje, vacunación, tratamiento | Sujeto existente, actividad atribuible a él en la fecha y unidades válidas. Ambos sexos pueden participar. Aplicar rangos y catálogo adecuados, no reglas de ordeño. |
| Movimiento individual | Destino válido, evitar destino igual al origen y membresías activas contradictorias en el mismo tipo de agrupación. Un grupo por conteo no acredita identidad individual por sí solo. |
| Baja individual | No duplicar una baja ya efectiva; causa y fecha coherentes. Conservar registro histórico. |
| Actividad grupal | Grupo activo en la fecha, modo de seguimiento respetado y cantidades positivas cuando correspondan. Una cantidad no identifica individuos concretos. |

Las reglas de reproducción que excedan el flujo de parto existente requieren precisar
su caso de uso antes de ampliarse. Este spec no inventa edades mínimas ni calendarios
biológicos constantes. Tampoco rediseña producción para distinguir ordeño descartado
de vendible: cualquier cambio de esa política necesita especificación propia.

La selección se revalida al confirmar, incluso si una sincronización cambió el animal
durante el formulario. Se conserva lo escrito y se explica el impedimento. Para captura
actual, una baja conocida no es seleccionable; para registro retrospectivo se evalúa la
fecha declarada. Si falta información autoritativa offline, se informa la limitación y
se conserva el registro pendiente de validación, sin inventar un estado favorable.

## 5. Datos, dependencias y límites

El contrato de sync debe incorporar el estado de baja y su procedencia de forma
compatible; la ampliación de SQLite exige migración local que preserve outbox y datos.
No se usa `isDeleted` como sustituto de `DisposedAt`. La representación concreta y las
reglas temporales compartidas se documentarán por ADR si cambian contratos estructurales.

No se agregan dependencias, tablas genéricas de reglas ni eventos nuevos en esta entrega.
Los términos existentes son `Animal`, `Sex`, `Species`, `Pregnancy`, `AnimalGroup`,
`GroupMembership` y baja con alcance de lote, según GLOSSARY. Un eventual concepto nuevo
se incorpora al glosario antes de escribir su código.

Quedan fuera corrección masiva de datos históricos, registro automático de lactancias,
ventas y contabilidad. Los casos ya inválidos se identifican para revisión; no se
eliminan ni se reescribe su sexo para que cuadren. Depende de [0004](../feature-0004-field-app-sync-reliability/spec.md)
para recibir estado actualizado y de [0008](../feature-0008-people-permission-enforcement/spec.md)
para autorización.

## 6. Criterios de aceptación

1. Macho de especie ordeñable: no aparece como candidato y una llamada directa al
   servicio local o al servidor rechaza el ordeño con motivo legible.
2. Hembra válida de especie ordeñable: puede registrar offline; especie no habilitada
   no permite ordeño. El catálogo nuevo funciona sin recompilar por nombre de especie.
3. Animales dados de baja dejan de ofrecerse para captura actual tras sync sin perder
   expediente; un registro anterior a la baja recibe evaluación temporal correcta.
4. Un cambio de selección, sexo, grupo o baja durante el formulario no produce envío
   obsoleto ni pérdida silenciosa del borrador.
5. La madre del parto coincide con su preñez; preñez completada no vuelve a ofrecerse.
6. Duplicados de baja, movimientos incoherentes, números imposibles y unidades inválidas
   se cubren con casos negativos, además de los registros válidos de ambos sexos.
7. Las pruebas cubren entrada móvil offline, API y push contra PostgreSQL real. Los
   rechazos permanecen consultables en el teléfono, conforme a 0004.

