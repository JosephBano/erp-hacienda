# ADR-0027 — Identidad de la cría generada en cliente en registro de parto offline

- **Estado:** Aceptado
- **Fecha:** 2026-09-08
- **Fase del roadmap:** 3.5a (adecuación al manejo real)

## Contexto

En el diseño original del registro de partos en la aplicación de campo (`BirthService` en `clients/field-app`), las crías nacidas se enviaban en el payload de `recordBirth` sin un identificador único generado por el dispositivo y no se guardaban en las tablas locales (`animals`, `animal_identifiers`). Era el servidor, al procesar `RecordBirthingCommand` vía `IAnimalRegistrationService`, quien generaba un nuevo UUID para cada cría.

Esto genera una ruptura operativa en campo (D6, Art. 3 y Art. 10 de la Constitución):
1. Inmediatamente después del parto, el operario necesita poder pesar a una cría, aplicarle un tratamiento preventivo o moverla sin necesidad de contar con señal celular o sincronizar.
2. Si la cría no tiene una identidad soberana en el dispositivo, no existe en la base local (WatermelonDB) y las pantallas de selección de animales no pueden mostrarla.
3. Si el registro de parto se sincroniza y es rechazado por el servidor, cualquier registro posterior realizado contra esa cría no debe enviarse como un hecho huérfano contra un UUID inexistente en el servidor.

El spec de la feature 0007 (`docs/spec/feature-0007-field-app-individual-tagged-livestock/spec.md`, Compuerta 1 y D6) exige explícitamente aprobar un ADR antes de alterar el contrato de parto y las dependencias del outbox.

## Decisión

Se decide:

1. **Identidad soberana en cliente (D6, Art. 3):** Toda cría viva ingresada en el paso de crías del parto (`Step3Offspring`) recibe un UUID v4 generado en el dispositivo móvil al momento de capturar el registro.
2. **Persistencia local inmediata:** `BirthService.recordBirth` persiste localmente en WatermelonDB cada cría en la tabla `animals` (con sexo, fecha de nacimiento, especie y madre) y, si se le asignó arete, en `animal_identifiers` (como `FarmTag` activo). Así, la cría queda consultable y elegible de inmediato para pesajes o tratamientos antes del primer sync.
3. **Ampliación del contrato de parto (`recordBirth`):** Cada elemento del arreglo `offspring` en el payload de `recordBirth` transporta su identificador bajo el campo `childId` (UUID).
4. **Compatibilidad retroactiva en el backend:** En `RecordBirthingCommand`, el DTO de cría (`OffspringBirthInfo` / `OffspringDto`) trata `ChildId` como opcional. Si el payload contiene un `childId` válido (no vacío), `AnimalRegistrationService` utiliza ese UUID exacto mediante `Animal.Register(..., id: childId)`. Si un cliente antiguo envía crías sin `childId`, el servidor genera un UUID nuevo, preservando la compatibilidad de versiones anteriores sin exigir reinstalaciones forzadas.
5. **Causalidad y manejo de dependencias en el outbox:**
   - La cola de salida (outbox) respeta el orden temporal: el parto se envía antes que cualquier evento posterior de la cría.
   - Si la operación de parto (`recordBirth`) es rechazada por el servidor, el motor de sincronización (`SyncEngine`) detecta las operaciones pendientes dependientes de los UUIDs de dichas crías y las retiene/marca como rechazadas con causa clara ("Depende del parto rechazado: <motivo>"), evitando llamadas huérfanas al backend.

## Alternativas consideradas

1. **ID temporal en cliente y remapeo tras sincronización:** Generar un identificador efímero (e.g. `tmp-1`) que se reemplace cuando el servidor responda con el UUID definitivo. Descartado: viola la Constitución (Art. 3, el UUID es soberano), obliga a complejas cascadas de reescritura de claves foráneas en SQLite local si se registran varios eventos offline, y falla si el dispositivo pasa días sin señal.
2. **Separar el parto en múltiples operaciones `createAnimal`:** Enviar cada cría como un `createAnimal` individual. Descartado: rompe la integridad transaccional del parto, pierde la relación genealógica con la madre y el servicio reproductivo en una sola operación, y no garantiza la creación atómica de la camada.

## Consecuencias

### Positivas
- Permite la continuidad operativa total offline de las crías desde el minuto cero de su nacimiento.
- La identidad se preserva intacta antes y después del sync, manteniendo la coherencia de todos los registros asociados (pesajes, tratamientos, traslados).
- Los dispositivos existentes con la versión previa del cliente pueden seguir enviando partos sin errores de deserialización.

### Negativas / A vigilar
- Si un parto pendiente es cancelado por el usuario en el dispositivo antes de sincronizar, las filas locales de las crías deben ser eliminadas o marcadas como canceladas para no dejar animales fantasma.
- El motor de sincronización añade una validación de causalidad para evitar enviar eventos de crías cuyo parto falló.

### Condición de reversa
- Evidencia de colisión de UUIDs generados en cliente o defectos en motores SQLite embebidos que impidan indexar eficientemente los registros creados antes del sync.
