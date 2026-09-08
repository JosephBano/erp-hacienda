# spec.md — Aplicar permisos de forma consistente en API y sincronización

> **Estado:** propuesta investigada, **no implementada**. La carpeta tiene los cuatro
> documentos de la convención: `spec.md` (qué se decidió), [`plan.md`](./plan.md) (en qué
> orden y en qué commits), [`tasks.md`](./tasks.md) (el desglose con casillas) y
> [`test-e2e.md`](./test-e2e.md) (la verificación manual). Ningún criterio de producción
> queda cerrado por existir estos documentos.
>
> **Propósito único de la carpeta:** la deuda de autorización explicada durante el
> diagnóstico. No es una auditoría general de seguridad ni un cambio de autenticación.

- **Rama documental:** `feature/field-app-production-specs`, base `0faf483`.
- **Fecha:** 2026-09-07. **Fase:** transversal, suelo de Fase 3.
- **Reglas:** AGENTS.md 2, 5, 7 y 10; Constitución arts. 6, 8 y 14.
- **Referencias:** [ADR-0007](../../adr/0007-modelo-permisos-bd.md),
  [SEGURIDAD](../../SEGURIDAD.md), [BACKLOG](../../BACKLOG.md).

## 1. Problema

Tener sesión iniciada no equivale a tener permiso para registrar ordeño, cambiar
animales o administrar catálogos. El dueño pidió aclarar esta deuda; no reportó un
incidente de acceso indebido. Es un defecto de control confirmado por lectura, no
una afirmación de explotación en producción.

## 2. Investigación previa

| Evidencia en `0faf483` | Hallazgo |
|---|---|
| `src/Hato.Api/Endpoints/MilkingEndpoints.cs:10`, `BreedingEndpoints.cs` y `TasksEndpoints.cs`. | Exigen autenticación del grupo de rutas; no aplican los permisos específicos previstos en `SystemPermissions`. |
| `AnimalsEndpoints.cs:14` y `:32`, comparados con `:43` y `:61`. | Alta e identificadores carecen del control `livestock.animals.write` que sí tienen borrado y edición. |
| `AnimalEventsEndpoints.cs`, `PlausibilityRangesEndpoints.cs`, `InventoryEndpoints.cs` en `/feed-consumptions`. | Hay escrituras sin el control fino aplicado en otras rutas del módulo. |
| `src/Hato.Api/Sync/PushSyncCommands.cs:92`, `:206`. | Verifica usuario y despacha por tipo de operación; no hay mapa de permisos en ese despachador. Añadir políticas solo a REST no protege el push. |
| `src/Hato.Api/Sync/SyncPullQueries.cs:366`. | El pull sí calcula colecciones visibles por permisos. Este control debe conservarse. |
| `src/Hato.Api/Endpoints/SyncEndpoints.cs:26`, `SyncPullQueries.cs:632`. | `/sync/operations` consulta las últimas operaciones sin filtro de usuario; `/sync/conflicts` sí exige `people.users.manage`. |
| `src/Modules/People/Hato.Modules.People.Infrastructure/Authorization/PermissionAuthorization.cs:27` y `:35`. | El autorizador concede por rol/claim antes de consultar BD. La semántica de revocación con JWT ya emitido necesita quedar explícita. |

Se buscaron políticas y `IPipelineBehavior` en `src`; los behaviors encontrados son
de validación, no un mecanismo transversal de autorización que cierre estos huecos.
No se hicieron peticiones con credenciales de producción ni se ejecutó la suite backend.

## 3. Decisiones fijadas para la propuesta

- **D1 — Una decisión de permiso equivalente para REST y push.** Usar la infraestructura
  existente, sin roles/especies hardcodeados nuevos ni permisos basados solo en visibilidad.
- **D2 — Conservación del trabajo offline.** Si el servidor deniega una operación,
  conserva el payload local y explica el rechazo; no descarta trabajo ni finge éxito.
- **D3 — Alcance propio por defecto en operaciones de sincronización.** Consulta personal
  para el empleado; `people.users.manage` permite supervisión global, coherente con conflictos.
- **D4 — No ampliar privilegios para que el piloto funcione.** Las asignaciones a roles
  deben conservar intención y mínimo alcance; todo permiso nuevo requiere definición
  y semilla/migración, no asignación indiscriminada a todos los usuarios.

## 4. Matriz normativa

| Acción | Permiso o alcance requerido |
|---|---|
| Consultar animales y su historial | `livestock.animals.read` |
| Alta/edición/borrado lógico, identificación, eventos, tratamiento, movimiento y corrección de animal/grupo | `livestock.animals.write`, más restricciones existentes de la operación |
| Registrar/consultar reproducción | `breeding.events.record` / `breeding.events.read` |
| Registrar/consultar ordeño | `production.milking.record` / `production.milking.read` |
| Administrar/consultar tareas | `tasks.manage` / `tasks.read` |
| Consultar usuarios / administrarlos | `people.users.read` / `people.users.manage` |
| Modificar rangos de plausibilidad | `livestock.animals.write`, alineado con la intención ya documentada del endpoint |
| Inventario, etapas de alimentación y recepciones | Conservar permisos específicos existentes |
| Registrar consumo de alimento | `inventory.feed-consumptions.record` |
| Consultar operaciones sync | Propias por usuario; global con `people.users.manage` |

La decisión sobre consumo se resolvió con `inventory.feed-consumptions.record` (Commit 5, D4).
No se reutiliza automáticamente `inventory.items.manage` para evitar dar administración
de catálogo a quien solo alimenta animales. Todas las operaciones de push tienen
una correspondencia explícita, incluidas las que producen varios comandos internos.

Sin autenticación, REST/push devuelve 401. Sin permiso, REST devuelve 403; un lote de
push autenticado conserva respuesta por operación y rechaza la no autorizada sin impedir
las otras válidas. Una denegación no produce efectos de dominio ni reserva engañosa.
Los replays respetan el estado real descrito en [0004](../feature-0004-field-app-sync-reliability/spec.md).

La revocación debe definir desde qué momento deja de aceptar nuevas escrituras online
y cómo se actualiza la copia de permisos del dispositivo. No se promete revocación
instantánea en un teléfono sin red. Es una decisión de seguridad pendiente de ADR si
modifica la política JWT actual; no se resuelve implícitamente con ocultar botones.

## 5. Alcance excluido y riesgos

No entra migración a cookies, limitación de login, rediseño de refresh tokens, aislamiento
por finca, scopes por grupo ni cambios de despliegue. Esos temas continúan en BACKLOG.
Las pantallas actuales que llaman `/sync/operations` deben tolerar el alcance propio.
Cerrar permisos puede revelar operaciones pendientes creadas con versiones anteriores:
deben quedar revisables y atribuibles, nunca ser borradas por la actualización.

## 6. Criterios de aceptación

1. Matriz de pruebas con admin, empleado autorizado, empleado sin permiso y anónimo,
   cubriendo rutas y operaciones push equivalentes contra PostgreSQL real.
2. Cada escritura denegada deja datos de dominio sin cambios; en un push mixto se
   procesan las autorizadas y se informa el motivo individual de las denegadas.
3. Pedir explícitamente una colección no permitida no evita el filtro de pull.
4. Un empleado solo ve sus operaciones; un supervisor autorizado puede ver las globales.
5. Operaciones offline rechazadas conservan contenido y autor, y el teléfono no las
   convierte en aceptadas al reintentar ni al refrescar permisos.
6. El permiso de consumo y la semántica de revocación están resueltos y documentados
   antes de implementar sus cambios; los comentarios de seguridad describen el código real.

