# plan.md — Ejecución de la rama `feature/people-permission-enforcement`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Desglose ejecutable en [`tasks.md`](./tasks.md), verificación en [`test-e2e.md`](./test-e2e.md).
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

**Objetivo:** que tener sesión iniciada deje de equivaler a tener permiso, con la misma
decisión de autorización en REST y en sincronización.

**Enfoque:** una rama, **cinco commits secuenciales**, más **una compuerta** que bloquea dos
de ellos. El punto de no retorno es el commit 5, que siembra permisos y asignaciones.

**Spec:** [`spec.md`](./spec.md).

**Transversal.** Es el suelo de Fase 3 y desbloquea la fila de consumo de
[0009](../feature-0009-inventory-consumption-batch-attribution/spec.md).

---

## Restricciones globales

- **Una decisión de permiso equivalente para REST y push** (D1). Usar la infraestructura
  existente. **Sin roles ni especies hardcodeados nuevos** (regla dura 3) y sin permisos
  basados solo en visibilidad.
- **El trabajo offline se conserva** (D2, regla dura 10). Si el servidor deniega, el payload
  local se conserva y el rechazo se explica. No se descarta trabajo ni se finge éxito.
- **No ampliar privilegios para que el piloto funcione** (D4). Las asignaciones conservan
  intención y mínimo alcance.
- **El filtro de pull existente se conserva.** `SyncPullQueries.cs:366` ya calcula colecciones
  visibles por permisos: este control **no se toca** salvo para reforzarlo.
- **Una denegación no produce efectos de dominio** ni reserva engañosa (spec sec. 4).
- **Las operaciones pendientes de versiones anteriores no se borran** (spec sec. 5). Quedan
  revisables y atribuibles.
- **Pruebas de backend contra PostgreSQL real**, nunca InMemory (regla 5).
- **Los comentarios de seguridad describen el código real** (criterio 6). Si un comentario
  afirma una protección que no existe, se corrige el comentario o el código, no se deja.

---

## Índice

1. [Compuerta 0 — Permiso de consumo y revocación](#compuerta-0--permiso-de-consumo-y-revocación)
2. [Commit 1 — Mapa de permisos del despachador de push](#commit-1--mapa-de-permisos-del-despachador-de-push)
3. [Commit 2 — Huecos en las rutas REST](#commit-2--huecos-en-las-rutas-rest)
4. [Commit 3 — Alcance propio en operaciones de sync](#commit-3--alcance-propio-en-operaciones-de-sync)
5. [Commit 4 — El cliente conserva lo denegado](#commit-4--el-cliente-conserva-lo-denegado)
6. [Commit 5 — Permiso de consumo: definición y semilla](#commit-5--permiso-de-consumo-definición-y-semilla)
7. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
8. [Cómo se prueba](#cómo-se-prueba)
9. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Permiso de consumo y revocación

**Bloquea los commits 4 y 5, no la rama entera.** Dos decisiones pendientes del spec sec. 4.

**Decisión A — código del permiso de consumo de alimento.** `SystemPermissions` no declara
uno propio. **No se reutiliza automáticamente `inventory.items.manage`** si eso obliga a dar
administración de catálogo a quien solo alimenta animales (spec sec. 4). Definir código,
descripción y a qué roles se asigna.

**Decisión B — semántica de revocación.** `PermissionAuthorization.cs:27,35` concede por
rol/claim antes de consultar la base. Definir:

1. Desde qué momento deja de aceptar nuevas escrituras online tras revocar un permiso.
2. Cómo se actualiza la copia de permisos del dispositivo.

- **No se promete revocación instantánea en un teléfono sin red** (spec sec. 4).
- **Si la decisión modifica la política JWT actual** → **detenerse** y escribir ADR. No se
  resuelve implícitamente ocultando botones.

## Commit 1 — Mapa de permisos del despachador de push

`feat(sync): require the same permission on push that the REST route requires`

**Por qué primero:** es el hueco de mayor consecuencia. `PushSyncCommands.cs:92` verifica
usuario y `:206` despacha por tipo de operación, **sin mapa de permisos**. Añadir políticas
solo a REST no protegería el push (spec sec. 2), así que el push va antes.

**Archivos:**
- `src/Hato.Api/Sync/PushSyncCommands.cs` — mapa explícito de tipo de operación a permiso
  requerido, aplicado antes de ejecutar el comando.
- **Todas** las operaciones de push tienen correspondencia explícita, **incluidas las que
  producen varios comandos internos** (spec sec. 4). Una operación sin entrada en el mapa se
  rechaza; no pasa por omisión.

**Verificación:** empleado sin permiso → la operación se rechaza con motivo, **sin efectos de
dominio**. Un lote de push autenticado conserva **respuesta por operación**: las autorizadas
se procesan y la denegada se rechaza sin impedir las otras válidas.

## Commit 2 — Huecos en las rutas REST

`fix(api): apply the fine-grained permission checks the endpoints already intended`

**Archivos:**
- `src/Hato.Api/Endpoints/AnimalsEndpoints.cs:14,32` — alta e identificadores carecen del
  control `livestock.animals.write` que sí tienen borrado y edición en `:43` y `:61`.
- `MilkingEndpoints.cs:10`, `BreedingEndpoints.cs`, `TasksEndpoints.cs` — exigen autenticación
  del grupo de rutas pero no los permisos específicos previstos en `SystemPermissions`.
- `AnimalEventsEndpoints.cs`, `PlausibilityRangesEndpoints.cs` e `InventoryEndpoints.cs` en
  `/feed-consumptions` — escrituras sin el control fino aplicado en otras rutas del módulo.
- Rangos de plausibilidad: `livestock.animals.write`, alineado con la intención ya documentada
  del endpoint.

**Verificación:** la matriz normativa de `spec.md` sec. 4, fila por fila. Sin autenticación →
401. Sin permiso → 403. Contra PostgreSQL real.

**Excepción:** la fila de consumo de alimento espera al commit 5.

## Commit 3 — Alcance propio en operaciones de sync

`fix(sync): scope the operations listing to its own user`

**Archivos:**
- `src/Hato.Api/Endpoints/SyncEndpoints.cs:26` y `SyncPullQueries.cs:632` —
  `/sync/operations` consulta las últimas operaciones **sin filtro de usuario**, mientras que
  `/sync/conflicts` sí exige `people.users.manage`.
- Aplicar D3: consulta personal para el empleado; `people.users.manage` permite supervisión
  global, coherente con conflictos.
- Las pantallas actuales que llaman `/sync/operations` deben **tolerar el alcance propio**
  (spec sec. 5).

**Verificación:** un empleado ve solo sus operaciones; un supervisor autorizado ve las
globales. Ninguna pantalla se rompe por recibir menos filas de las que recibía.

## Commit 4 — El cliente conserva lo denegado

`fix(field-app): keep a denied operation and explain why it was refused`

**Bloqueado por:** Compuerta 0, decisión B — el cliente necesita saber cómo se comporta ante
una revocación.

**Archivos:**
- El cliente móvil: una denegación conserva **payload, contenido y autoría**, y muestra el
  motivo. No se convierte en aceptada al reintentar ni al refrescar permisos (criterio 5).
- Coherente con lo que [0004](../feature-0004-field-app-sync-reliability/spec.md) hace con los
  rechazos: los replays respetan el estado real.

**Verificación:** operación denegada, reintentada y con permisos refrescados sigue mostrándose
como denegada, con su contenido intacto.

## Commit 5 — Permiso de consumo: definición y semilla

`feat(people): add the feed consumption permission and seed its role assignments`

**Bloqueado por:** Compuerta 0, decisión A.

**Punto de no retorno:** siembra permisos y asignaciones. Revertir exige migración de reversa,
no borrar la existente (regla 7).

**Archivos:**
- `SystemPermissions` — declarar el permiso nuevo.
- Migración/semilla con la asignación a roles, conservando **intención y mínimo alcance** (D4).
  **No** asignación indiscriminada a todos los usuarios.
- `InventoryEndpoints.cs` en `/feed-consumptions` y la ruta de push equivalente — aplicar el
  permiso nuevo.

**Verificación:** quien solo alimenta animales puede registrar consumo **sin** recibir
administración de catálogo. La fila pendiente de la matriz de `spec.md` sec. 4 queda cerrada.

## Orden, dependencias y puntos de no retorno

```
Compuerta 0 (permiso de consumo + revocación)
  │      └─ bloquea Commit 4 (decisión B) y Commit 5 (decisión A)
  │
Commit 1 (push)  ── no bloqueado, va primero
  └─ Commit 2 (REST)
       └─ Commit 3 (alcance de /sync/operations)
            └─ Commit 4 (cliente conserva lo denegado)
                 └─ Commit 5 (permiso de consumo)  ◄── PUNTO DE NO RETORNO
```

- **Commit 5 es el punto de no retorno**: siembra permisos y asignaciones.
- **Commits 1, 2 y 3 no están bloqueados** por la compuerta y se entregan aunque siga abierta.
- **El commit 1 va antes que el 2** a propósito: cerrar REST primero daría sensación de
  seguridad con el push todavía abierto.

## Cómo se prueba

1. `dotnet test` completo contra PostgreSQL real (regla 5). **Nunca InMemory** para verificar
   comportamiento de autorización.
2. `npm test` completo en `clients/field-app` para el commit 4.
3. [`test-e2e.md`](./test-e2e.md), con **cuatro identidades**: admin, empleado autorizado,
   empleado sin permiso y anónimo (criterio 1).

## Descripción del PR

**Título:** `feat(people): enforce permissions consistently across REST and sync push`

**Cuerpo:**

- **Qué:** mapa de permisos en el despachador de push, controles finos en las rutas REST que
  los tenían previstos y no aplicados, alcance propio en `/sync/operations`, conservación de
  las operaciones denegadas en el cliente y permiso propio para el consumo de alimento.
- **Por qué:** tener sesión iniciada no equivale a tener permiso. Es un defecto de control
  **confirmado por lectura**, no una afirmación de explotación en producción
  ([`spec.md` sec. 1](./spec.md#1-problema)).
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas-para-la-propuesta), D1–D4.
- **Qué NO incluye:** migración a cookies, limitación de login, rediseño de refresh tokens,
  aislamiento por finca, scopes por grupo ni cambios de despliegue. Siguen en `BACKLOG.md`.
- **Riesgo declarado:** cerrar permisos puede revelar operaciones pendientes creadas con
  versiones anteriores. **Quedan revisables y atribuibles; la actualización no las borra**
  (spec sec. 5).
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
- **Resultado de Compuerta 0:** `<código del permiso de consumo; semántica de revocación; si
  hizo falta ADR>`.
