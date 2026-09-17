# plan.md — Ejecución de la rama `feature/sync-field-app-reliability`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> El desglose ejecutable está en [`tasks.md`](./tasks.md) y la verificación en
> [`test-e2e.md`](./test-e2e.md). Las decisiones no se relitigan acá — si algo no cuadra,
> se corrige el spec primero.

**Objetivo:** que enviar, recibir y mostrar datos produzcan un estado consistente y
comprobable, sin perder registros de campo y sin declarar éxito donde no lo hubo.

**Enfoque:** una rama, **ocho commits secuenciales** por propósito, más **dos compuertas**
que detienen el trabajo si su verificación falla. El commit 1 escribe pruebas que **deben
fallar** al escribirse: es la reproducción del defecto S8, no una regresión. El punto de no
retorno es el commit 2, que añade migración EF sobre `production`.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

Aplican a **todos** los commits. Copiadas de las decisiones de `spec.md` sec. 3.

- **El outbox no se toca nunca** (regla dura 10, D1). Ni al resetear espejos, ni al migrar,
  ni al limpiar. Lo registrado y no enviado es la única copia que existe.
- **Ningún éxito falso** (D3). `hasMore`, errores de aplicación, rechazos y operaciones
  pendientes impiden afirmar que todo quedó actualizado.
- **No se relaja `UnmappedMemberHandling.Disallow`** (D3, spec sec. 1.1). El contrato
  incompatible se corrige declarando el campo, nunca tolerando campos desconocidos.
- **No se elimina la confirmación del operador para que pase una prueba** (D6).
- **Protocolo incremental y UUID intactos** (D1). No se sustituye por descargas completas
  rutinarias ni se limpia el dispositivo para ocultar divergencias.
- **Pruebas de backend contra PostgreSQL real**, nunca InMemory (`AGENTS.md` regla 5).
- **Nada se marca completo** sin `dotnet test` y `npm test` en `clients/field-app` en verde,
  la suite entera.
- **Compatibilidad con las versiones instaladas** (spec sec. 5): no se fuerza reinstalación
  como mecanismo de migración.
- **Fuera de alcance en esta rama:** el protocolo de recuperación de reservas interrumpidas
  (D4). Exige ADR previo si modifica ADR-0008. Ver Compuerta 1.

---

## Índice

1. [Compuerta 0 — Evidencia de producción](#compuerta-0--evidencia-de-producción)
2. [Commit 1 — Arnés de contrato de push](#commit-1--arnés-de-contrato-de-push)
3. [Commit 2 — Confirmación de plausibilidad en ordeño](#commit-2--confirmación-de-plausibilidad-en-ordeño)
4. [Commit 3 — El pull deja de mentir](#commit-3--el-pull-deja-de-mentir)
5. [Commit 4 — `Duplicate` deja de blanquear rechazos](#commit-4--duplicate-deja-de-blanquear-rechazos)
6. [Compuerta 1 — Reservas interrumpidas](#compuerta-1--reservas-interrumpidas)
7. [Commit 5 — Ejecución única y reintento programado](#commit-5--ejecución-única-y-reintento-programado)
8. [Commit 6 — Las vistas reflejan la base](#commit-6--las-vistas-reflejan-la-base)
9. [Commit 7 — Recuperación manual segura](#commit-7--recuperación-manual-segura)
10. [Commit 8 — Diagnóstico persistente](#commit-8--diagnóstico-persistente)
11. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
12. [Cómo se prueba](#cómo-se-prueba)
13. [Descripción del PR](#descripción-del-pr)

---

## Compuerta 0 — Evidencia de producción

**Nada de lo que sigue se escribe hasta cerrar esto.** Cuatro specs de la serie repiten
esta misma laguna; es lo más barato de resolver y lo primero.

Recoger y registrar en el PR:

1. Versión/build del APK instalado en cada teléfono en uso, y versión del backend desplegado.
2. Qué significó «eliminar» en el reporte: baja (`DisposedAt`), borrado lógico
   (`DeletedAt`), desactivación de membresía o SQL manual. Entidad, hora aproximada y quién.
3. Si el fallo de sincronización aparece siempre o de forma intermitente, y en qué pantalla.

- **Si el ordeño individual figura entre lo que falla** → confirma la hipótesis de
  `spec.md` sec. 1.1 y el orden de commits procede tal cual.
- **Si el ordeño no se usa en esta finca** → S8 sigue siendo un defecto real y el commit 2
  se mantiene, pero la causa del reporte queda abierta: **detenerse** y reevaluar antes del
  commit 3, porque el diagnóstico apuntaría a otro de los diez hallazgos.

## Commit 1 — Arnés de contrato de push

`test(sync): cover every push operation type with client-generated payload fixtures`

**Por qué primero:** es la reproducción del defecto, y va antes de la corrección para que
quede evidencia de que el arnés lo detecta. Escrito después del arreglo, no probaría nada.
Implementa D7.

**Archivos:**
- Crear: `clients/field-app/src/services/__tests__/pushPayloadContracts.test.ts` — ejercita
  cada servicio que llama a `outbox.enqueue`, captura el payload encolado y lo escribe en el
  fixture. Falla si un servicio encola un tipo que no está en el fixture.
- Crear: `docs/contracts/push-payloads.json` — fixture versionado. Es un artefacto generado,
  no editable a mano; el encabezado del archivo lo dice.
- Crear: `tests/Hato.Sync.IntegrationTests/SyncPushContractTests.cs` — lee el fixture y
  afirma, por cada entrada, que deserializa contra su comando con `JsonOptions` reales.
- Crear: `tests/Hato.Sync.IntegrationTests/SyncPushMilkingTests.cs` — la cobertura de push
  de ordeño que hoy no existe, con confirmación `true` y `false`.

**Verificación:** `dotnet test --filter SyncPushContractTests` **falla** en la entrada
`recordMilking` con el `JsonException` de miembro no mapeado. Los otros nueve tipos pasan.
Ese fallo se copia al PR: es la prueba de que S8 es real y no una lectura equivocada.

**Completitud, en ambos sentidos:** el test afirma que todo `case` de
`PushSyncCommands.ExecuteAsync` tiene entrada en el fixture, y que todo `operationType`
encolado por el cliente tiene `case`. Un tipo nuevo sin cobertura rompe la suite.

## Commit 2 — Confirmación de plausibilidad en ordeño

`feat(production): persist the operator's plausibility confirmation on milking sessions`

**Por qué acá:** cierra el fallo que el commit 1 dejó en rojo. Implementa D6, calcado del
precedente `TreatmentCourseApplication` y su migración `20260809165838`.

**Punto de no retorno:** añade migración EF. Un `git revert` posterior exige migración de
reversa, no borrar la existente (regla 7).

**Archivos:**
- `src/Modules/Production/Hato.Modules.Production.Domain/MilkingSession.cs` — propiedad
  `IsPlausibilityConfirmed`, parámetro con default `false` en `Create` para no romper
  llamadores existentes.
- `src/Modules/Production/Hato.Modules.Production.Application/Milking/RecordMilkingSessionCommand.cs`
  — declarar `bool IsPlausibilityConfirmed = false` en el record y pasarlo a `Create`.
- Configuración EF de `MilkingSession` — `IsRequired().HasDefaultValue(false)`, igual que
  `TreatmentCourseConfiguration.cs:80`.
- Nueva migración en `src/Modules/Production/.../Persistence/Migrations/`.
- `src/Hato.Api/Endpoints/MilkingEndpoints.cs` — exponer el campo en la ruta REST, para que
  REST y push produzcan el mismo registro.

**Verificación:** `dotnet test --filter "SyncPushContractTests|SyncPushMilkingTests"` en
verde. Un ordeño empujado con `isPlausibilityConfirmed: true` persiste `true` y se puede
leer de vuelta; con `false`, persiste `false`. Ninguno se rechaza por deriva de JSON.

## Commit 3 — El pull deja de mentir

`fix(sync): stop reporting success when the pull page budget is exhausted`

**Por qué acá:** independiente de los anteriores, pero va antes que el reintento porque el
planificador necesita distinguir «terminé» de «me quedé sin presupuesto».

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts:242` — agotar `MAX_PULL_PAGES` deja de
  retornar `{ ok: true }`. Devuelve un resultado que expresa trabajo pendiente y permite
  continuar desde el cursor guardado, sin reiniciar la descarga.
- El cursor solo avanza después de aplicar su página (ya es el comportamiento; se cubre con
  prueba para que no regrese).

**Verificación:** la prueba de investigación descrita en `spec.md` sec. 2.1 (a) —API falsa
que responde siempre `hasMore: true` con colecciones vacías— pasa a esperar `ok: false` con
motivo de trabajo pendiente. Antes afirmaba `ok: true`.

## Commit 4 — `Duplicate` deja de blanquear rechazos

`fix(sync): keep a rejected operation rejected when the server answers Duplicate`

**Por qué acá:** cierra el agravante de S8 descrito en `spec.md` sec. 1.1. Sin esto, un
ordeño rechazado puede mostrarse como enviado al reintentar.

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts:171` — `Duplicate` deja de tratarse como
  envío correcto sin mirar el contenido. Si la respuesta trae `errorDetails`, la operación
  conserva su estado de rechazo con el motivo original.
- `src/Hato.Api/Sync/PushSyncCommands.cs:116` — la respuesta `Duplicate` debe distinguir
  una operación previamente aceptada de una previamente rechazada, de forma que el cliente
  pueda decidir sin adivinar.

**Verificación:** empujar una operación que el servidor rechaza, perder la respuesta y
reintentar la misma `clientOperationId`: el teléfono la sigue mostrando como rechazada, con
el motivo. Cubierto en `SyncPushProtocolTests` y en las pruebas del motor.

## Compuerta 1 — Reservas interrumpidas

**Antes del commit 5.** D4 exige conservar el resultado real de cada operación al
reintentar, incluida una reserva pendiente que quizá materializó su efecto. Determinar si
la solución modifica ADR-0008.

- **No lo modifica** → se implementa dentro del commit 5.
- **Lo modifica** → **detenerse**, escribir el ADR y sacarlo de esta rama. El commit 5
  entrega reintento y coordinación, y el spec registra la reserva como pendiente. No se
  implementa un protocolo nuevo de sincronización sin ADR aprobado (regla 2 y art. 14).

## Commit 5 — Ejecución única y reintento programado

`feat(sync): schedule bounded retries and serialise sync triggers`

**Por qué acá:** necesita el commit 3 para saber cuándo un pull quedó incompleto, y el
commit 4 para no reintentar en bucle algo que fue rechazado por negocio.

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts:38,73,87` — `retryDelayMs` pasa a tener
  consumidor real: planificación de reintento mientras la app está activa. Una sola
  ejecución coordinada compartida por todos los disparadores.
- Disparadores: recuperación de conexión (ya existe en `start()`), vuelta a primer plano
  (`AppState`) y sincronización manual, todos sobre la misma ejecución.
- Un rechazo de negocio no entra en el bucle de reintentos.

**Verificación:** con red estable y un error transitorio, el motor vuelve a intentar sin
intervención. Dos disparadores simultáneos producen **una** ejecución, no dos. Una operación
rechazada no se reintenta sola.

## Commit 6 — Las vistas reflejan la base

`fix(field-app): refresh open screens when sync applies changes`

**Por qué acá:** depende de que el motor informe con verdad cuándo terminó y qué aplicó
(commits 3 y 5). Resuelve S1 y S2.

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts` — notificar al contenedor cuando una
  sincronización aplica cambios, no solo al cambiar módulos.
- `clients/field-app/src/App.tsx:130,178` — el hato y demás estado se refrescan con esa
  notificación. La BD confirmada es la fuente de las vistas.
- `clients/field-app/src/screens/SyncStatusScreen.tsx:63,72` — coherente con lo anterior.
- Si desaparece el animal seleccionado, se informa y se impide enviar contra una selección
  obsoleta; el formulario ya escrito **no** se descarta (D2 de 0006, sec. 4 de este spec).

**Verificación:** con la pantalla abierta, aplicar un cambio en web y sincronizar: la
pantalla lo refleja sin reiniciar, sin cambiar de pestaña y sin un segundo botón.

## Commit 7 — Recuperación manual segura

`fix(sync): make mirror recovery exclusive with sync and safe to interrupt`

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts:188` — `resetMirror` se coordina con
  `running`: exclusión mutua con la sincronización, no compite con ella.
- `clients/field-app/src/screens/SyncStatusScreen.tsx:77` — no invoca la recuperación antes
  de comprobar conectividad; no vacía el único catálogo utilizable sin conexión.
- Tolerar corte de red o cierre de app a mitad, sin pérdida del trabajo local.

**Verificación:** la prueba existente de que `sync_outbox` sobrevive intacto sigue en verde
(regla dura 10). Se añade: recuperación interrumpida a mitad no deja el dispositivo sin
catálogo ni pierde formularios pendientes.

## Commit 8 — Diagnóstico persistente

`feat(field-app): persist the diagnostic log across restarts`

**Archivos:**
- `clients/field-app/src/services/loggerService.ts:59,65` — persistir en almacenamiento
  nativo disponible en lugar de depender de `localStorage`. **Ninguna dependencia nueva**
  (D5): si la única solución exige uno, se detiene y se propone ADR (regla 2).
- Registro con fecha UTC, versión de app y esquema, identificador de intento, etapa fallida,
  colección, conteos y operación correlacionable. Retención acotada.
- Compartir deliberadamente por acción del usuario. **No** registra JWT, contraseñas ni
  payloads completos; no envía nada a terceros automáticamente.

**Verificación:** la prueba de investigación `spec.md` sec. 2.1 (b) —sin `localStorage`,
registrar un error y construir otro logger— pasa a recuperar el error en vez de cero.
Revisión manual de que ningún campo del registro contiene secretos.

## Orden, dependencias y puntos de no retorno

```
Compuerta 0 (evidencia)
  └─ Commit 1 (arnés, en rojo por S8)
       └─ Commit 2 (ordeño + migración)  ◄── PUNTO DE NO RETORNO
       └─ Commit 3 (pull sin éxito falso)
            └─ Commit 4 (Duplicate)
                 └─ Compuerta 1 (¿ADR de reservas?)
                      └─ Commit 5 (ejecución única + reintento)
                           └─ Commit 6 (vistas)
  Commit 7 (recuperación)  ── independiente, tras el 5 por exclusión mutua
  Commit 8 (diagnóstico)   ── independiente, puede ir en paralelo
```

- **Commit 2 es el punto de no retorno**: migración EF sobre `production`.
- **Commits 1→2 no se pueden invertir** sin perder la evidencia de la reproducción.
- **Commits 7 y 8 son independientes** y pueden adelantarse si la compuerta 1 se alarga.

## Cómo se prueba

1. `dotnet test` completo, contra PostgreSQL real (regla 5).
2. `npm test` completo en `clients/field-app`. Las seis omisiones existentes
   (`MilkingScreen.test.tsx`, `AnimalEditScreen.test.tsx`) siguen documentadas; esta rama no
   las cierra ni las aumenta.
3. [`test-e2e.md`](./test-e2e.md) completo, sobre **SQLite nativo** en dispositivo real.
   Las pruebas del motor corren sobre LokiJS: no bastan para aceptar en campo (spec sec. 2.1).

## Descripción del PR

**Título:** `fix(sync): make field-app sync report the truth and stop losing milking records`

**Cuerpo:**

- **Qué:** corrige el contrato de push de ordeño, añade cobertura de contrato para los diez
  tipos de operación, elimina el éxito falso del pull, impide que un rechazo se muestre como
  enviado, coordina los disparadores de sincronización, refresca las vistas abiertas, hace
  segura la recuperación manual y persiste el diagnóstico.
- **Por qué:** tres semanas de datos en producción con fallos intermitentes al sincronizar.
  El commit 1 documenta la reproducción del defecto principal (`spec.md` sec. 1.1).
- **Decisiones:** [`spec.md` sec. 3](./spec.md#3-decisiones-fijadas-para-la-propuesta),
  D1–D7.
- **Qué NO incluye:** protocolo de recuperación de reservas interrumpidas (D4, requiere ADR);
  ejecución en segundo plano con la app suspendida; otro motor de sincronización; permisos
  (van en 0008); validaciones de aptitud (van en 0005).
- **Riesgo declarado:** el commit 2 añade migración. La compatibilidad con outboxes de
  versiones instaladas se verifica en `test-e2e.md` E2E-9, no se asume.
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
- **Resultado de Compuerta 0:** `<pegar aquí build, backend y qué significó «eliminar»>`.
