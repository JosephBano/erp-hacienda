# spec.md — Manejo móvil de porcinos con identificación individual por arete

> **Estado:** implementado en rama `feature/livestock-individual-tagging`. La carpeta tiene los cuatro
> documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en qué
> orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual).
>
> **Propósito único de la carpeta:** operar con individuos identificados conservando el
> historial y la capacidad de manejo por grupo.

- **Rama documental:** `feature/field-app-production-specs`, base `0faf483`.
- **Fecha:** 2026-09-07. **Fase:** 3.5a, adecuación al manejo real.
- **Reglas:** AGENTS.md 1, 3, 6, 7, 8 y 10; Constitución arts. 3 y 8.
- **Referencias:** [ADR-0006](../../adr/0006-doble-identificacion.md),
  [ADR-0015](../../adr/0015-lote-por-conteo.md), [GLOSSARY](../../GLOSSARY.md).
  ADR-0015 figura como «Propuesto» en su encabezado aunque hay código que lo implementa;
  este documento no cambia ni da por aprobada retroactivamente esa decisión.

## 1. Cambio de requisito

El dueño declara: «todos ellos van a tener un arete». Esto reemplaza el supuesto
operativo del engorde sin identificación para los animales que pueda reconocer
individualmente. No autoriza reconstruir genealogías de animales ya mezclados ni
convertir el arete en clave primaria.

Debe diferenciarse aretar nuevas camadas identificables de aretar un lote antiguo cuya
correspondencia física con las filas del sistema se perdió. No se recibió aún ese dato.

## 2. Investigación previa

| Evidencia en `0faf483` | Hallazgo |
|---|---|
| `docs/adr/0015-lote-por-conteo.md:152`, sección «Qué pasa el día del aretado». | El diseño ya contempla la transición y prohíbe asignar arbitrariamente filas antiguas a animales mezclados. |
| `clients/field-app/src/screens/birth/Step3Offspring.tsx:48` y `clients/field-app/src/services/birthService.ts:8`. | El parto acepta arete opcional por cría. No hace falta inventar otra entidad para el arete. |
| `clients/field-app/src/services/herdQueries.ts:47` construye etiqueta con nombre y un identificador preferido; `clients/field-app/src/screens/AnimalSubjectScreen.tsx:65` busca sobre esa etiqueta. | No equivale a buscar por todos los identificadores vigentes; uno puede quedar fuera de la etiqueta. |
| `src/Modules/Livestock/Hato.Modules.Livestock.Infrastructure/Persistence/Configurations/AnimalIdentifierConfiguration.cs:24`. | La unicidad existente es por animal/tipo vigente; no garantiza que dos animales no compartan valor de arete. |
| `src/Modules/Livestock/Hato.Modules.Livestock.Application/Animals/AssignAnimalIdentifierCommand.cs:17`. | Carga el animal y sus identificadores, pero no busca el mismo valor en otros animales. No se afirmó haber encontrado duplicados en producción. |
| `clients/field-app/src/services/birthService.ts:66` encola el parto y `:79` envía crías sin UUID propio; `src/Modules/Breeding/Hato.Modules.Breeding.Application/Birthings/RecordBirthingCommand.cs:125` las registra en servidor. | El servicio móvil no crea crías consultables localmente al registrar el parto. Hace falta resolver la continuidad de captura sobre una cría antes del primer sync. |
| `clients/field-app/src/services/herdQueries.ts:202` expone grupos activos y su `trackingMode`; `src/Modules/Livestock/Hato.Modules.Livestock.Application/AnimalGroups/UpdateAnimalGroupCommand.cs:11` no recibe modo de seguimiento. | No existe en ese comando una conversión libre de grupos históricos; este spec no la presupone. |

Ver [0004](../feature-0004-field-app-sync-reliability/spec.md#21-verificación-ejecutada-y-sus-límites)
para ejecución de pruebas y límites. No hubo inventario físico ni consulta de aretes en producción.

## 3. Decisiones fijadas para la propuesta

- **D1 — Individuo con UUID y arete adjunto.** Se usan `Animal`, `AnimalIdentifier` y
  `FarmTag`, con vigencia e historial. Perder el arete no elimina al animal.
- **D2 — Nuevo manejo individual, historia grupal preservada.** Grupos `Individual`
  permiten identificar miembros; se conservan grupos/eventos `Headcount` anteriores.
- **D3 — Buscar por arete es el recorrido principal de esta operación.** Mostrar arete
  destacado, sexo y grupo para distinguir; buscar también nombre e identificadores
  vigentes aunque no sean el elegido para construir la etiqueta.
- **D4 — Sin correspondencia demostrable no hay asignación histórica.** La transición
  de animales mezclados requiere decisión documentada del dueño sobre inventario físico
  y linaje desconocido. No se ejecuta con coincidencias de orden, peso o cantidad.
- **D5 — La falta temporal de arete no bloquea registrar un hecho real.** Se muestra
  identificación pendiente; no se impone una restricción global incompatible con Art. 3.
- **D6 — La cría debe poder recibir registros sin red inmediatamente después de nacer.**
  La identidad generada en cliente debe conservarse al sincronizar. El cambio del
  contrato de parto y dependencias del outbox requiere ADR antes de implementar.

## 4. Alcance y comportamiento

Incluye selección por arete, distinción clara individuo/grupo, continuidad offline de
crías, asignación o reemplazo de arete en campo con historia y detección de ambigüedad.
La operación de identificación debe existir localmente y sincronizarse de forma
idempotente; el endpoint web actual por sí solo no cumple el flujo de campo.

Peso, tratamiento, vacuna, movimiento y baja de un individuo se atribuyen a su UUID.
Alimento continúa registrado por grupo. Un pesaje muestral no se convierte en pesos
individuales inventados y una baja por cantidad no selecciona animales al azar.

Al registrar parto offline, cada cría tiene identidad estable y estado local pendiente;
puede buscarse y recibir un pesaje o tratamiento antes de conexión. La sincronización
respeta la dependencia nacimiento → registro posterior. Si rechaza el nacimiento,
los dependientes se conservan y muestran la causa; no se envían como hechos huérfanos.

El cambio de arete conserva identificación anterior y fechas. Buscar un arete anterior
puede ofrecer historial claramente señalado, nunca presentarlo como identificación
vigente. La búsqueda distingue coincidencia exacta y parcial, conserva ceros iniciales
y no selecciona automáticamente entre valores ambiguos.

La política de duplicados vigentes entre animales exige definir alcance y normalización:
finca/tipo, reutilización temporal y mayúsculas. Hasta decidirla, el diseño mínimo
detecta y presenta ambigüedad, y no fusiona animales. Una garantía de unicidad concurrente
en BD requiere ADR, migración nueva y revisión no destructiva de datos existentes.

## 5. Preguntas abiertas y límites

- Momento del aretado: nacimiento, ingreso o lote ya mezclado; posibilidad real de
  reconocer cada animal antiguo. Determina qué transición puede implementarse.
- Tipo de arete y formato operativo: código propio, SIFAE u otro; el usuario no dijo
  que todos serán identificadores oficiales.
- Alcance de unicidad y tratamiento de aretes repetidos. No se aprueba limpiar datos.
- No entra lector RFID/QR, impresión, compras de hardware ni eliminación del modo por
  conteo. Ninguna dependencia nueva está aprobada.
- Actualizar en implementación la definición de `WeightSorting` del glosario: hoy dice
  que termina la identificación individual. Con aretes esto ya no es necesariamente
  cierto. Esta entrega conserva los documentos anteriores y registra la divergencia aquí.

La capacidad funcional depende de [0004](../feature-0004-field-app-sync-reliability/spec.md)
y [0005](../feature-0005-field-app-activity-validation/spec.md). Su experiencia visual
se integra después en el rediseño final [0010](../feature-0010-field-app-redesign/spec.md);
ese rediseño no es requisito previo para implementar la identificación offline.
Especificar esta capacidad no autoriza aún migrar físicamente lotes históricos.

## 6. Criterios de aceptación

1. Encontrar offline un animal por cualquiera de sus identificadores vigentes, con
   ceros iniciales conservados y sin confundir coincidencias parciales.
2. Registrar parto con varias crías aretadas y, sin sincronizar, registrar un peso a una
   de ellas. Tras reinicio y sync mantiene exactamente el mismo UUID y genealogía.
3. Actualizar o reemplazar arete y cambiar grupo desde un dispositivo se refleja en el
   otro sin duplicar animales ni reasignar historia.
4. Un duplicado o búsqueda ambigua exige resolver la identidad; nunca fusiona ni elige
   automáticamente. Probar también conflicto entre dos dispositivos offline.
5. El animal sin arete admite captura por identidad interna; el arete pendiente es
   visible y asignarlo después preserva todos los registros.
6. Conviven grupos individuales nuevos y grupos por conteo históricos. No se adjudican
   a individuos hechos antiguos conocidos únicamente por cantidad.
7. Alimento sigue por grupo, y tratamiento/pesaje individual queda asociado al animal
   elegido; filtros y validaciones funcionan para ambos sexos y especies configuradas.
8. Se verifica con datos ficticios y luego con una camada reconocible en campo; el
   caso de lote ya mezclado no se acepta sin resolver la pregunta de identidad física.
