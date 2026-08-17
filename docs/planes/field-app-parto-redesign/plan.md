# plan.md — Ejecución de la rama `feature/field-app-parto-redesign`

> **Qué es este documento.** Cómo se hace el trabajo que [`spec.md`](./spec.md) decidió:
> orden de los commits, qué archivos toca cada uno, qué pruebas exige y cómo se mergea.
> Las decisiones no se relitigan acá — si algo no cuadra, se corrige el spec primero.

- **Rama:** `feature/field-app-parto-redesign` desde `develop`.
- **Estrategia:** una rama, **seis commits secuenciales** por propósito. No se abren PRs
  intermedios: el conjunto entra como un PR único, pero los commits permiten revisarlo por
  partes (mitigación de la decisión D9 del spec).

---

## Índice

1. [Compuerta: tarea 0](#1-compuerta-tarea-0)
2. [Commit 1 — Saneamiento del pull en el servidor](#commit-1--saneamiento-del-pull-en-el-servidor)
3. [Commit 2 — Saneamiento del motor de sync en el cliente](#commit-2--saneamiento-del-motor-de-sync-en-el-cliente)
4. [Commit 3 — Preñeces y servicios en el pull](#commit-3--preñeces-y-servicios-en-el-pull)
5. [Commit 4 — Espejo local y `loadPregnantDams`](#commit-4--espejo-local-y-loadpregnantdams)
6. [Commit 5 — Asistente de parto en cuatro pasos](#commit-5--asistente-de-parto-en-cuatro-pasos)
7. [Commit 6 — Inicio y limpieza](#commit-6--inicio-y-limpieza)
8. [Orden, dependencias y puntos de no retorno](#8-orden-dependencias-y-puntos-de-no-retorno)
9. [Cómo se prueba](#9-cómo-se-prueba)
10. [Descripción del PR](#10-descripción-del-pr)

---

## 1. Compuerta: tarea 0

**Nada de lo que sigue se escribe hasta cerrar esto.**

Verificar por SQL contra la base real si `101` / `102` / `103` existen como identificadores:

```sql
SELECT ai.value, ai.type, ai.is_active, ai.animal_id, a.deleted_at
FROM animal_identifiers ai
LEFT JOIN animals a ON a.id = ai.animal_id
WHERE ai.value IN ('101','102','103');
```

- **Sin filas** → hipótesis del spec sec. 2.4 confirmada (datos huérfanos en el
  dispositivo). El plan procede tal cual.
- **Con filas** → la hipótesis cae. **Detenerse**, reportar y reevaluar el spec: el
  problema sería de presentación o de estado, no de deriva de la base local.

Registrar el resultado en el PR.

## Commit 1 — Saneamiento del pull en el servidor

`chore(sync): drop unconsumed health-plan collections from the mobile pull`

**Por qué primero:** deja el contrato del pull limpio antes de agregarle colecciones nuevas.
Agregar `pregnancies` sobre un contrato con tres colecciones muertas invita a copiar el
error.

**Archivos:**
- `src/Hato.Api/Sync/SyncPullQueries.cs` — quitar `healthPlans`, `healthPlanItems` y
  `healthPlanAssignments` de `RequiredPermissionByCollection`, de `SyncCollectionsDto` y de
  las lecturas del handler; borrar sus DTOs si no los usa nadie más.
- Pruebas de integración de sync que afirmen sobre esas colecciones.

**Pruebas:** integración contra PostgreSQL real (`TestDatabase`). Un pull completo ya no
devuelve las tres colecciones; el resto sigue igual y el cursor avanza como antes.

**Cuidado:** verificar con `grep` que ningún otro consumidor (admin-web incluido) lea esos
campos del DTO de pull antes de borrarlos.

## Commit 2 — Saneamiento del motor de sync en el cliente

`fix(field-app): surface unmapped sync collections and honour server tombstones`

**Archivos:**
- `clients/field-app/src/services/syncEngine.ts`:
  - `applyCollections`: reemplazar el `continue` mudo ante colección desconocida por un
    registro vía `loggerService` y un conteo de error visible.
  - `applyRow`: eliminar la asignación incondicional `record.isDeleted = false`.
- `clients/field-app/src/screens/SyncStatusScreen.tsx`: acción **"Rehacer descarga"** con
  confirmación explícita — borra tablas espejo, borra `pull_cursor`, dispara `syncNow()`.
- `clients/field-app/tests/syncEngine.test.ts`, `tests/SyncStatusScreen.test.tsx`.

**Pruebas (las tres son obligatorias):**
1. Una colección desconocida en la respuesta produce error visible, no silencio.
2. Una fila con `isDeleted: true` del servidor no queda con `isDeleted` en `false`.
3. **Rehacer descarga vacía las tablas espejo y deja `sync_outbox` intacto.** Ésta es la
   prueba que protege la regla dura 10 — sin ella el commit no entra.

**Cuidado:** la confirmación en pantalla debe estar en español llano y decir explícitamente
qué se conserva (lo registrado y no enviado) y qué se vuelve a bajar (el hato).

## Commit 3 — Preñeces y servicios en el pull

`feat(sync): expose pregnancies and breeding services to the field app`

**Archivos:**
- `src/Hato.Api/Sync/SyncPullQueries.cs`:
  - `SyncPregnancyDto(Id, DamId, ServiceId, Status, ExpectedBirthDate, CreatedAt, UpdatedAt, IsDeleted)`
  - `SyncBreedingServiceDto(Id, DamId, ServiceType, SireAnimalId, StrawId, CreatedAt, UpdatedAt, IsDeleted)`
  - Ambas en `SyncCollectionsDto`, en `RequiredPermissionByCollection` bajo
    `SystemPermissions.BreedingEventsRead`, y con su `ReadAsync` correspondiente.
  - Inyectar `IBreedingDbContext` en `GetSyncPullQueryHandler`.

**Cero cambios en `Hato.Modules.Breeding.Domain`.** Si el commit necesita tocar el dominio,
algo se entendió mal — volver al spec sec. 2.3.

**Pruebas de integración contra PostgreSQL real:**
1. Un usuario con `breeding.events.read` recibe ambas colecciones.
2. Un usuario **sin** ese permiso no las recibe, ni siquiera pidiéndolas por nombre en
   `collections=` (el pull no debe filtrar por permiso *o* por nombre, sino por ambos).
3. Registrar un parto mueve la preñez a `Completed` y el cambio viaja en el siguiente pull —
   esto es lo que hace desaparecer sola a la hembra que ya parió.
4. El cursor avanza correctamente con las colecciones nuevas presentes.

## Commit 4 — Espejo local y `loadPregnantDams`

`feat(field-app): mirror pregnancies locally and query pregnant dams`

**Archivos:**
- `src/database/schema.ts` — dos `tableSchema`, `SCHEMA_VERSION` de 10 a **11**.
- `src/database/migrations.ts` — paso `toVersion: 11` con dos `createTable`.
  **Sin este paso WatermelonDB no abre la base y el empleado pierde la cola.**
- `src/database/models.ts` — modelos `Pregnancy` y `BreedingService` + `modelClasses`.
- `src/services/syncEngine.ts` — entradas en `TABLE_BY_COLLECTION`.
- `src/services/herdQueries.ts` — `loadPregnantDams`.
- `src/services/birthService.ts` — enviar `pregnancyId`; dejar de rellenar el padre.
- `tests/schema.test.ts`, `tests/herdQueries.test.ts`, `tests/births.test.ts`.

**Pruebas:** un caso por cada fila de la tabla de `sireLabel` del spec sec. 6.3 — incluido
el caso del semental que no existe localmente, que debe dar `sin registrar` y no un id
crudo. Más: preñez `Completed` y `Aborted` quedan fuera de la lista; el orden es por fecha
probable de parto ascendente; `schema.test.ts` verifica que la migración a 11 conserva los
datos existentes.

## Commit 5 — Asistente de parto en cuatro pasos

`feat(field-app): redesign birth recording as a four-step wizard`

**Archivos:**
- Nuevo directorio `src/screens/birth/` con el contenedor y un componente por paso.
  Objetivo: ningún archivo por encima de ~150 líneas.
- `src/screens/BirthScreen.tsx` — pasa a ser el contenedor, o se retira en favor de
  `src/screens/birth/BirthScreen.tsx` (decisión de implementación; mantener el `testID`
  `birth-screen` en cualquier caso).
- `src/App.tsx` — deja de pasar `dams`/`sires`; pasa las preñadas de `loadPregnantDams`.
- `tests/BirthScreen.test.tsx` + un test por paso.

**Pruebas:**
1. El paso 1 lista solo preñadas y muestra `EmptyState` útil cuando no hay ninguna.
2. Ningún paso ofrece elegir padre. Prueba explícita: no existe `testID` `sire-*`.
3. La camada sigue siendo editable — agregar, quitar, cambiar sexo, arete y peso.
4. El peso rechaza valores no positivos, como hoy.
5. Retroceder de paso conserva las crías ya cargadas.
6. Confirmar encola una operación con `pregnancyId` y **sin** padre.
7. Camada de 20: el contador y los botones `+Hembra`/`+Macho` siguen accesibles.

**Conservar íntegro** lo ganado en 3.5a.0 #2 (camada editable) y 3.5a.4 (peso al nacer).
Perder cualquiera de las dos es una regresión sobre correcciones que el cliente ya pidió.

## Commit 6 — Inicio y limpieza

`fix(field-app): let the activities hub scroll and drop the dead home screen`

**Archivos:**
- `src/screens/ActivitiesHub.tsx` — `scrollable`, revisión de espaciado. **Sin reordenar
  los sujetos** (spec sec. 8).
- Eliminar `src/screens/HomeScreen.tsx`, su import en `src/App.tsx` y
  `tests/HomeScreen.test.tsx`.
- `docs/BACKLOG.md` — anotar la reconciliación automática de existencia y las demás
  pantallas con el mismo problema de espacio.

**Pruebas:** `tests/ActivitiesHub.test.tsx` sigue pasando sin cambios en el orden esperado;
la suite completa pasa sin el test eliminado.

## 8. Orden, dependencias y puntos de no retorno

```
Tarea 0 (compuerta)
   └─> Commit 1 (servidor: limpiar pull)
          └─> Commit 3 (servidor: agregar colecciones)   ─┐
   └─> Commit 2 (cliente: motor de sync + reset)          ├─> Commit 4 (cliente: espejo + query)
                                                          ┘        └─> Commit 5 (asistente)
Commit 6 (independiente, puede ir en cualquier momento tras el 2)
```

- **Commit 3 depende del 1**: agregar colecciones sobre el contrato sucio duplica el error.
- **Commit 4 depende del 2 y del 3**: necesita el guard funcionando y el dato viajando.
- **Commit 5 depende del 4**: sin `loadPregnantDams` no hay paso 1.
- **Punto de no retorno:** el commit 4 sube `SCHEMA_VERSION` a 11. Una vez que un
  dispositivo abre la base en v11, revertir el commit sin una migración de bajada deja ese
  teléfono sin poder abrir la app. Si hay que revertir después de instalar en un dispositivo
  real, se revierte hacia adelante (nueva migración), nunca borrando el paso.

## 9. Cómo se prueba

**Backend** (regla dura 5 — integración contra PostgreSQL real, nunca InMemory):

```bash
dotnet test
# Si Docker no levanta contenedores:
HATO_TEST_POSTGRES="Host=...;Database=...;Username=...;Password=..." dotnet test
```

**App de campo:**

```bash
cd clients/field-app && npm test
```

La suite completa, no solo los archivos tocados, antes de cada push.

**Verificación manual:** [`test-e2e.md`](./test-e2e.md). El E2E-1 (rehacer descarga sobre un
teléfono con datos huérfanos) es el que valida el pedido original del cliente sobre los
`101/102/103` y no puede omitirse.

## 10. Descripción del PR

Debe incluir, según el protocolo del AGENTS.md:

- **Propósito:** los cuatro problemas reportados por el cliente, enlazando al spec.
- **Resultado de la tarea 0**, con la consulta y lo que devolvió.
- **Por qué la rama mezcla saneamiento de sync con el rediseño** (decisión D9), reconociendo
  la tensión con el "un PR = un propósito" y la lista de commits que permite revisarlo por
  partes.
- **Cómo probarlo manualmente:** referencia a `test-e2e.md`.
- **Qué NO incluye:** registro de preñeces desde la app, reconciliación automática, rediseño
  de las demás pantallas.
- **Advertencia de migración:** `SCHEMA_VERSION` 10 → 11.
