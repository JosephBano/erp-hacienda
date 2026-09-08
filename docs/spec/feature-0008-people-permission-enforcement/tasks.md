# tasks.md — Desglose ejecutable

> Checklist de la rama `feature/people-permission-enforcement`. Cada tarea es una unidad de
> trabajo con criterio de terminado verificable. Agrupadas por el commit de
> [`plan.md`](./plan.md) al que pertenecen.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## Compuerta 0 — Permiso de consumo y revocación

> Bloquea los commits 4 y 5, no la rama entera.

- [x] **TG0.1** Definir código, descripción y roles del permiso de consumo de alimento.
      **Terminado:** **no** se reutiliza `inventory.items.manage` si eso obliga a dar
      administración de catálogo a quien solo alimenta animales (spec sec. 4).
- [ ] **TG0.2** Definir desde qué momento una revocación deja de aceptar nuevas escrituras
      online. **Terminado:** respuesta explícita, no «cuando expire el token» por omisión.
- [ ] **TG0.3** Definir cómo se actualiza la copia de permisos del dispositivo.
      **Terminado:** no se promete revocación instantánea en un teléfono sin red.
- [ ] **TG0.4** Determinar si la decisión modifica la política JWT actual.
      **Terminado:** si la modifica, **detenerse** y escribir ADR. No se resuelve ocultando
      botones.

---

## Commit 1 — Mapa de permisos del despachador de push

- [x] **T1.1** Enumerar los tipos de operación de `PushSyncCommands.cs` y el permiso que le
      corresponde a cada uno, según la matriz de `spec.md` sec. 4.
      **Terminado:** la lista no tiene huecos; hoy son diez tipos.
- [x] **T1.2** Implementar el mapa y aplicarlo **antes** de ejecutar el comando
      (`PushSyncCommands.cs:92,206`).
- [x] **T1.3** Las operaciones que producen **varios comandos internos** tienen correspondencia
      explícita. **Terminado:** ninguna queda cubierta por el permiso de otra.
- [x] **T1.4** Una operación sin entrada en el mapa **se rechaza**.
      **Terminado:** no pasa por omisión; cubierto por prueba con un tipo inventado.
- [x] **T1.5** **Prueba: escritura denegada deja los datos de dominio sin cambios.**
      **Terminado:** criterio 2; ninguna denegación produce efecto ni reserva engañosa.
- [x] **T1.6** Prueba: push mixto: las autorizadas se procesan, la denegada se rechaza con su
      motivo individual y **no impide** las válidas.
- [x] **T1.7** Prueba: sin autenticación, el push devuelve 401.
- [x] **T1.8** `dotnet test` completo en verde, contra PostgreSQL real.

---

## Commit 2 — Huecos en las rutas REST

- [x] **T2.1** `AnimalsEndpoints.cs:14,32` — aplicar `livestock.animals.write` a alta e
      identificadores, como ya lo tienen borrado y edición en `:43` y `:61`.
- [x] **T2.2** `MilkingEndpoints.cs:10` — aplicar `production.milking.record` /
      `production.milking.read`.
- [x] **T2.3** `BreedingEndpoints.cs` — aplicar `breeding.events.record` / `breeding.events.read`.
- [x] **T2.4** `TasksEndpoints.cs` — aplicar `tasks.manage` / `tasks.read`.
- [x] **T2.5** `AnimalEventsEndpoints.cs` — aplicar el control fino de escritura.
- [x] **T2.6** `PlausibilityRangesEndpoints.cs` — `livestock.animals.write`, alineado con la
      intención ya documentada del endpoint.
- [x] **T2.7** `InventoryEndpoints.cs` — conservar los permisos específicos existentes de
      inventario, etapas de alimentación y recepciones. **Terminado:** la fila de consumo de
      alimento **espera al commit 5**.
- [x] **T2.8** Prueba de la matriz completa de `spec.md` sec. 4, fila por fila: sin
      autenticación 401, sin permiso 403. Contra PostgreSQL real.
- [x] **T2.9** Verificar que **no se introdujo ningún rol ni especie hardcodeada** (regla dura
      3, D1). **Terminado:** revisado en `git diff`.
- [x] **T2.10** `dotnet test` completo en verde.

---

## Commit 3 — Alcance propio en operaciones de sync

- [x] **T3.1** `/sync/operations` (`SyncEndpoints.cs:26`, `SyncPullQueries.cs:632`) filtra por
      usuario. **Terminado:** hoy consulta las últimas operaciones sin filtro.
- [x] **T3.2** `people.users.manage` permite supervisión global, coherente con
      `/sync/conflicts` (D3).
- [x] **T3.3** Las pantallas que llaman `/sync/operations` toleran el alcance propio.
      **Terminado:** ninguna se rompe por recibir menos filas (spec sec. 5).
- [x] **T3.4** **Prueba: pedir explícitamente una colección no permitida no evita el filtro de
      pull.** **Terminado:** criterio 3; el control de `SyncPullQueries.cs:366` se conserva y
      se refuerza, no se toca a la baja.
- [x] **T3.5** Prueba: empleado ve solo sus operaciones; supervisor autorizado ve las globales.
- [x] **T3.6** `dotnet test` completo en verde.

---

## Commit 4 — El cliente conserva lo denegado

> Bloqueado por la Compuerta 0, decisión B.

- [x] **T4.1** Una denegación conserva **payload, contenido y autoría** en el teléfono (D2,
      regla dura 10).
- [x] **T4.2** El motivo del rechazo se muestra de forma legible.
      **Terminado:** el empleado entiende qué pasó sin abrir una herramienta técnica.
- [x] **T4.3** **Prueba: la operación denegada no se convierte en aceptada al reintentar.**
      **Terminado:** criterio 5.
- [x] **T4.4** Prueba: tampoco al refrescar permisos.
- [x] **T4.5** Coherencia con [0004](../feature-0004-field-app-sync-reliability/spec.md): los
      replays respetan el estado real. **Terminado:** si 0004 ya está mergeado, el arreglo de
      `Duplicate` cubre este caso y no se duplica la lógica.
- [x] **T4.6** `npm test` completo en verde.

---

## Commit 5 — Permiso de consumo: definición y semilla

> Bloqueado por la Compuerta 0, decisión A.

- [x] **T5.1** Declarar el permiso nuevo en `SystemPermissions`, con el código decidido en TG0.1.
- [x] **T5.2** Migración/semilla con la asignación a roles (regla 7).
      **Terminado:** conserva **intención y mínimo alcance**; **no** asignación indiscriminada
      a todos los usuarios (D4).
- [x] **T5.3** Aplicar el permiso en `InventoryEndpoints.cs` `/feed-consumptions`.
- [x] **T5.4** Aplicar el permiso en la ruta de push equivalente.
      **Terminado:** REST y push exigen lo mismo (D1).
- [x] **T5.5** **Prueba: quien solo alimenta animales registra consumo SIN recibir
      administración de catálogo.** **Terminado:** es la razón por la que este permiso existe
      en vez de reutilizar `inventory.items.manage`.
- [x] **T5.6** La fila pendiente de la matriz de `spec.md` sec. 4 queda cerrada.
- [x] **T5.7** `dotnet test` completo en verde.

---

## Cierre

- [ ] **TC.1** `dotnet test` completo contra PostgreSQL real (regla 5).
      **Terminado:** nunca InMemory para verificar comportamiento de autorización.
- [ ] **TC.2** `npm test` completo en `clients/field-app`.
- [ ] **TC.3** Ejecutar [`test-e2e.md`](./test-e2e.md) con las **cuatro identidades**: admin,
      empleado autorizado, empleado sin permiso y anónimo (criterio 1).
- [ ] **TC.4** Revisar las operaciones pendientes creadas con versiones anteriores que el
      cierre de permisos haya revelado. **Terminado:** quedan **revisables y atribuibles**;
      la actualización **no las borra** (spec sec. 5, regla dura 1).
- [ ] **TC.5** Verificar que los comentarios de seguridad describen el código real.
      **Terminado:** criterio 6; un comentario que afirme una protección inexistente se
      corrige, no se deja.
- [ ] **TC.6** Actualizar `docs/SEGURIDAD.md` con lo que esta rama cierra y lo que sigue abierto.
- [ ] **TC.7** Anotar en `docs/BACKLOG.md` lo excluido: cookies, limitación de login, refresh
      tokens, aislamiento por finca, scopes por grupo (regla 9).
- [ ] **TC.8** Abrir el PR con la descripción de [`plan.md`](./plan.md), incluido el resultado
      de la Compuerta 0.
