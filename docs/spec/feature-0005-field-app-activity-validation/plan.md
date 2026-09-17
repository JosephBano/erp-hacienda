# plan.md — Ejecución de la rama `feature/livestock-activity-validation`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Desglose ejecutable en [`tasks.md`](./tasks.md), verificación en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

**Objetivo:** que cada actividad ofrezca sujetos válidos, explique sus restricciones y
repita las invariantes en el servidor, sin depender de que una pantalla concreta filtre bien.

**Enfoque:** una rama, **seis commits secuenciales**, más **una compuerta** previa. El punto
de no retorno es el commit 1, que sube `SCHEMA_VERSION` en el cliente (hoy `11`, en
`clients/field-app/src/database/schema.ts:16`) y amplía el contrato de pull.

**Spec:** [`spec.md`](./spec.md).

**Depende de:** [0004](../feature-0004-field-app-sync-reliability/spec.md) para recibir
estado actualizado, y de [0008](../feature-0008-people-permission-enforcement/spec.md) para
autorización. Esta rama **no** espera a que 0008 cierre: valida aptitud, no permisos.

---

## Restricciones globales

- **Filtrar por actividad, no reducir el hato histórico** (D1). Un macho o un animal dado de
  baja sigue siendo consultable en su expediente.
- **Sin `if`/`switch` por especie, producto o raza** (regla dura 3, art. 8, D3). Capacidad de
  ordeño, categorías y rangos vienen de configuración en base de datos.
- **`isDeleted` no se usa como sustituto de `DisposedAt`** (spec sec. 5). Una baja no es un
  borrado lógico.
- **La misma invariante en captura local y en servidor** (D2). Un acceso directo a la API o al
  push recibe la misma decisión de negocio.
- **No se descarta lo escrito** (spec sec. 4). Si una revalidación impide enviar, se conserva
  el borrador y se explica el impedimento.
- **No se corrigen datos históricos ya inválidos** (spec sec. 5). Se identifican para revisión;
  no se eliminan ni se reescribe su sexo para que cuadren (regla dura 1).
- **Pruebas de backend contra PostgreSQL real**, nunca InMemory (regla 5).
- **Migración local que preserve outbox y datos** (regla dura 10) en todo cambio de esquema móvil.

---

## Índice

1. [Compuerta 0 — Representación del estado de baja](#compuerta-0--representación-del-estado-de-baja)
2. [Commit 1 — El estado de baja llega al teléfono](#commit-1--el-estado-de-baja-llega-al-teléfono)
3. [Commit 2 — Selectores por actividad](#commit-2--selectores-por-actividad)
4. [Commit 3 — Ordeño: aptitud en el cliente](#commit-3--ordeño-aptitud-en-el-cliente)
5. [Commit 4 — Ordeño: invariante en el servidor](#commit-4--ordeño-invariante-en-el-servidor)
6. [Commit 5 — Las demás actividades](#commit-5--las-demás-actividades)
7. [Commit 6 — Revalidación al confirmar](#commit-6--revalidación-al-confirmar)
8. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
9. [Cómo se prueba](#cómo-se-prueba)
10. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Representación del estado de baja

**Nada se escribe hasta cerrar esto.** `Animal.cs:57` tiene `DisposedAt`, distinto de
`DeletedAt`. Ni `SyncPullQueries.cs:50` ni `clients/field-app/src/database/models.ts:13` lo
transportan o modelan.

Decidir y registrar:

1. **Qué se transporta**: la fecha de baja, su causa, o solo un indicador. D5 exige distinguir
   cronología («una baja posterior no invalida un pesaje real anterior»), así que un booleano
   sin fecha no alcanza.
2. **Si el cambio del contrato de pull es estructural** al punto de exigir ADR (spec sec. 5:
   «se documentarán por ADR si cambian contratos estructurales»).

- **No exige ADR** → el commit 1 procede.
- **Sí lo exige** → **detenerse**, escribir el ADR y aprobarlo antes de tocar el contrato.

## Commit 1 — El estado de baja llega al teléfono

`feat(sync): carry the animal disposal state to the field app`

**Por qué primero:** todo lo demás necesita el dato. Sin él, el filtro de bajas no puede
existir offline y el criterio 3 del spec es inverificable.

**Punto de no retorno:** sube `SCHEMA_VERSION`. La migración local debe preservar `sync_outbox`
y los datos existentes; una vez distribuida, revertir exige otra migración.

**Archivos:**
- `src/Hato.Api/Sync/SyncPullQueries.cs:50` — incluir el estado de baja en la proyección del
  animal sincronizado, con lo que decidió la Compuerta 0.
- `clients/field-app/src/database/models.ts:13` y `schema.ts` — modelar el campo; subir
  `SCHEMA_VERSION` de `11` al siguiente.
- Migración local de WatermelonDB que preserve outbox y datos.

**Verificación:** un animal dado de baja en el servidor llega al teléfono con su estado y su
fecha. `sync_outbox` sobrevive la migración con su contenido intacto — sin esta prueba el
commit no entra (regla dura 10).

## Commit 2 — Selectores por actividad

`feat(field-app): query candidates per activity instead of the whole herd`

**Por qué acá:** consume el dato del commit 1 y da la base a los commits 3 y 5.

**Archivos:**
- `clients/field-app/src/services/herdQueries.ts:93` — hoy excluye borrados pero no bajas.
  El hato general y los selectores de actividad pasan a tener consultas distintas (D1): el
  expediente sigue mostrando a todos, la captura ofrece solo a los aptos.
- Se conserva `herdQueries.ts:127` y el flujo de preñeces activas que ya usa `BirthScreen`
  (spec sec. 2): **no se rehace el asistente de parto**.

**Verificación:** el hato histórico sigue devolviendo machos y bajas; el selector de ordeño
no. Cubierto por prueba, no por inspección de pantalla.

## Commit 3 — Ordeño: aptitud en el cliente

`fix(field-app): stop offering males as milking candidates`

**Archivos:**
- `clients/field-app/src/services/milkingService.ts:248` — hoy consulta la especie, no el
  sexo. Añadir sexo hembra y ausencia de baja efectiva anterior al hecho, conservando las
  validaciones existentes de volumen (`:84`) y retiro (`:92`), que **no se tocan**.
- `clients/field-app/src/screens/MilkingScreen.tsx:225` — deja de mostrar a todos con el
  botón deshabilitado; ofrece candidatos aptos.
- `clients/field-app/src/App.tsx:335` — `candidates={herd}` pasa a recibir el selector de
  actividad del commit 2.

**Verificación:** un macho de especie ordeñable no aparece como candidato, y una llamada
directa a `milkingService` lo rechaza con motivo legible. Lo segundo importa más que lo
primero: es la defensa que sobrevive a una UI vieja en caché (D2).

**No se exige `Lactation` activa** (spec sec. 4): su ciclo de vida no está implementado.

## Commit 4 — Ordeño: invariante en el servidor

`feat(production): reject milking for animals that cannot be milked`

**Por qué acá:** D2. Un acceso directo a la API o al push debe recibir la misma decisión.

**Archivos:**
- `src/Modules/Production/.../Milking/RecordMilkingSessionCommand.cs:22` — el manejador hoy
  valida fecha, autor y total, y revisa retiros en `:60`, pero no sexo ni capacidad de ordeño.
  Consultar Livestock **por contratos**, como ya hace con `IWithdrawalPeriodsReader`.
- Ampliar el contrato de Livestock si hace falta exponer sexo y capacidad de la especie.

**Verificación:** `POST` directo a la ruta REST con un macho → rechazo con Problem Details y
motivo legible. Push equivalente → operación `Rejected` con el mismo motivo. Contra
PostgreSQL real.

## Commit 5 — Las demás actividades

`feat(livestock): validate subject fitness for weighing, treatment and movement`

**Archivos:** los servicios móviles y manejadores de cada actividad, según la tabla de
`spec.md` sec. 4.

- **Parto:** madre hembra coherente con la preñez elegida; padre animal **o** material
  genético, nunca ambos; no ofrecer machos como madres. Respetar el flujo actual.
- **Pesaje, vacunación, tratamiento:** sujeto existente, actividad atribuible en la fecha,
  unidades válidas. **Ambos sexos participan** — no aplicar reglas de ordeño aquí.
- **Movimiento individual:** destino válido, distinto del origen, sin membresías activas
  contradictorias en el mismo tipo de agrupación.
- **Baja individual:** no duplicar una baja ya efectiva; causa y fecha coherentes.
- **Actividad grupal:** grupo activo en la fecha, modo de seguimiento respetado, cantidades
  positivas. Una cantidad no identifica individuos.

**Verificación:** casos negativos por actividad, más casos válidos de **ambos sexos**. Los
rangos de plausibilidad y la política de ADR-0022 se conservan: falta de rango consultivo no
se convierte en bloqueo (D4).

## Commit 6 — Revalidación al confirmar

`fix(field-app): revalidate the selected subject before enqueuing the record`

**Archivos:**
- Los formularios de actividad: al confirmar, revalidar la selección aunque una
  sincronización haya cambiado al animal durante el llenado.
- **Se conserva lo escrito** y se explica el impedimento. Nunca se descarta en silencio.
- Captura actual: una baja conocida no es seleccionable. Registro retrospectivo: se evalúa la
  fecha declarada (D5).
- Si falta información autoritativa offline, se informa la limitación y el registro queda
  pendiente de validación; **no se inventa un estado favorable**.

**Verificación:** cambiar sexo, grupo o dar de baja al animal desde otro dispositivo mientras
el formulario está abierto no produce envío obsoleto ni pérdida del borrador.

## Orden, dependencias y puntos de no retorno

```
Compuerta 0 (representación del estado de baja; ¿ADR?)
  └─ Commit 1 (contrato de pull + SCHEMA_VERSION)  ◄── PUNTO DE NO RETORNO
       └─ Commit 2 (selectores)
            ├─ Commit 3 (ordeño cliente)
            │    └─ Commit 4 (ordeño servidor)
            └─ Commit 5 (demás actividades)
                 └─ Commit 6 (revalidación al confirmar)
```

- **Commit 1 es el punto de no retorno**: cambia contrato de pull y esquema local distribuido.
- **Commits 3 y 5 son paralelizables** una vez cerrado el 2.
- **El commit 4 no puede adelantarse al 3**: si el servidor rechaza antes de que el cliente
  filtre, el empleado descubre el bloqueo después de escribir todo el formulario.

## Cómo se prueba

1. `dotnet test` completo contra PostgreSQL real (regla 5).
2. `npm test` completo en `clients/field-app`.
3. [`test-e2e.md`](./test-e2e.md) sobre SQLite nativo en dispositivo real.
4. Entrada móvil offline, API y push: los tres caminos, no solo el que se tocó (spec, criterio 7).

## Descripción del PR

**Título:** `feat(livestock): validate animal fitness per activity, offline and on the server`

**Cuerpo:**

- **Qué:** transporta el estado de baja al teléfono, separa el hato histórico de los
  candidatos por actividad, impide ordeñar machos y especies no habilitadas en cliente y
  servidor, valida las demás actividades y revalida la selección al confirmar.
- **Por qué:** el dueño reportó machos visibles en ordeño y falta de bloqueos. La revisión
  confirmó ese caso concreto ([`spec.md` sec. 2](./spec.md#2-hallazgos-verificados-por-lectura)).
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas-para-la-propuesta), D1–D5.
- **Qué NO incluye:** corrección masiva de datos históricos, registro automático de lactancias,
  ventas, contabilidad, permisos (0008), ni distinción entre ordeño descartado y vendible —
  esa política necesita especificación propia.
- **Riesgo declarado:** el commit 1 sube `SCHEMA_VERSION`; la preservación del outbox se
  verifica, no se asume.
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
