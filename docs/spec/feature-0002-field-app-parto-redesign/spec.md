# spec.md — Rediseño del registro de partos en `field-app`

> **Qué es este documento.** La especificación de diseño de la rama
> `feature/field-app-parto-redesign`: qué se construye, por qué, y qué decisiones quedan
> fijadas. El *cómo* día a día vive en [`plan.md`](./plan.md), el desglose ejecutable en
> [`tasks.md`](./tasks.md) y la verificación manual en [`test-e2e.md`](./test-e2e.md).
>
> **Por qué esta subcarpeta.** Los cuatro documentos son un solo entregable de una sola
> rama; separarlos en `docs/spec/` los dejaría huérfanos entre sí. Esta carpeta es el
> **precedente** del que salieron las de `reestructura-documentacion/` y
> `admin-web-animal-groups/`, y la fuente de la que se calcaron las plantillas de
> `docs/plantillas/` (ver `reestructura-documentacion/spec.md` sec. 6).

> **Estado y procedencia.** Plan **no ejecutado**: solo la tarea 0 está cerrada (sec. 2.4).
> Los cuatro documentos nacieron en la rama `feature/field-app-parto-redesign`, que se
> **borró el 2026-08-17** tras traerlos a `docs/reestructura-documentacion` — la rama no
> contenía otra cosa que estos cuatro archivos. La rama se vuelve a crear desde `develop`
> cuando el trabajo arranque.

- **Rama Git:** `feature/field-app-parto-redesign` (desde `develop`, en `2988946`).
- **Fecha:** 2026-08-15.
- **Fase del ROADMAP:** 3.5 — Adaptación porcina (en curso). Este trabajo corrige el
  registro de partos entregado en 3.5a.0 y 3.5a.4; no abre fase nueva.
- **ADRs vigentes que respalda o respeta:** ADR-0006 (padre dual: animal o pajuela),
  ADR-0008 (protocolo de sincronización), ADR-0012 (adaptador WatermelonDB),
  ADR-0019 (visibilidad de módulos).
- **Reglas duras que gobiernan este trabajo:** `AGENTS.md` regla 4 (rama desde `develop`),
  regla 5 (integración contra PostgreSQL real, nunca InMemory), regla 9 (un PR, un
  propósito — la tensión que D9 reconoce y mitiga) y regla 10 (no se destruye dato del
  usuario: el reset nunca toca `sync_outbox`, D8).

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: capa de sincronización](#5-diseño-capa-de-sincronización)
6. [Diseño: datos de preñez en el dispositivo](#6-diseño-datos-de-preñez-en-el-dispositivo)
7. [Diseño: asistente de parto en cuatro pasos](#7-diseño-asistente-de-parto-en-cuatro-pasos)
8. [Diseño: Inicio (`ActivitiesHub`)](#8-diseño-inicio-activitieshub)
9. [Riesgos y deuda](#9-riesgos-y-deuda)
10. [Criterios de aceptación](#10-criterios-de-aceptación)

---

## 1. Por qué existe este spec

El cliente reportó cuatro problemas contra la pantalla de parto de la app de campo:

1. **La pantalla no respira.** Secciones comprimidas con scroll interno; nada se despliega
   hacia abajo con holgura.
2. **Se puede registrar un parto de cualquier hembra**, esté preñada o no; y una hembra que
   ya parió sigue apareciendo en la lista.
3. **Aparecen animales con nombre `101`, `102`, `103`** que no existen en la base de datos
   de Postgres.
4. **El parto pide elegir el padre**, cuando el padre ya quedó determinado por el servicio
   de monta o inseminación registrado antes en admin-web.

Los cuatro son síntomas reales. Dos de ellos (2 y 3) no se pueden resolver dentro de la app
tal como está hoy: el dato de preñez nunca llega al teléfono, y la base local del dispositivo
puede quedar desalineada del servidor sin forma de repararla.

## 2. Hallazgos verificados

Todo lo que sigue se comprobó leyendo el código, no por suposición.

### 2.1 La pantalla apila tres listas con scroll en un contenedor sin scroll

`clients/field-app/src/screens/BirthScreen.tsx` (285 líneas) renderiza un `Screen` con
`scrollable = false` (el valor por defecto de `src/ui/components.tsx`) que contiene tres
`ScrollView` anidados — `dam-list`, `offspring-list` y `sire-list` — compitiendo por el
mismo espacio flex. El resultado es exactamente lo que el cliente describe.

### 2.2 El filtro de madres no consulta preñez alguna

`src/App.tsx:375` construye la lista así:

```ts
dams={herd.filter((member) => member.sex === 'Female')}
```

No hay condición de preñez porque **no existe el dato en el dispositivo**:
`src/database/schema.ts` no tiene tabla de preñeces, y `src/Hato.Api/Sync/SyncPullQueries.cs`
no expone la colección.

### 2.3 El backend ya resuelve el padre y ya cierra la preñez

`src/Modules/Breeding/Hato.Modules.Breeding.Application/Birthings/RecordBirthingCommand.cs`:

- Si el comando llega sin `SireAnimalId` ni `SireStrawId`, el handler busca la preñez activa
  de la madre y toma el padre del `BreedingService` asociado.
- Si hay preñez activa, llama a `pregnancy.MarkCompleted()`.

`Hato.Modules.Breeding.Domain.BreedingService` ya impone el invariante
`SireAnimalId XOR StrawId` y prohíbe pajuela en monta natural.

**Consecuencia:** el pedido 4 del cliente no requiere ningún cambio de dominio. Basta con
que la app deje de mandar un padre elegido a mano y en su lugar muestre el que ya se deduce.
Y el pedido "la que ya parió no debe salir" se cumple solo al filtrar por
`PregnancyStatus.Active`, porque el propio handler pasa la preñez a `Completed`.

### 2.4 El pull nunca reconcilia existencia — origen de los `101/102/103`

`SyncPullQueries.cs:588` (método `ReadAsync`) entrega únicamente filas con
`(UpdatedAt ?? CreatedAt) > cursor`. El borrado **lógico** sí viaja correctamente: el
`AuditTimestampInterceptor` (`src/Shared/Hato.SharedKernel.Persistence/`) estampa `UpdatedAt`
en toda entidad modificada, así que un `Animal.Delete()` mueve la fila delante del cursor, y
`syncEngine.ts:230` la destruye localmente con `prepareDestroyPermanently()`.

Lo que **no** existe es un mecanismo para filas que desaparecen de Postgres sin pasar por
borrado lógico: reset de base de desarrollo, `ef database drop`, restauración de backup, o
un `DELETE` manual. El servidor jamás dice "estos ids ya no existen", así que el teléfono los
conserva indefinidamente.

Esa es la explicación de los `101/102/103`: son filas huérfanas en el WatermelonDB del
dispositivo, apuntando a ids que ya no existen en el servidor. No hay ningún generador de
aretes automático ni dato semilla con esos valores — se buscó en `clients/field-app`,
`clients/admin-web` y en las migraciones semilla del backend, sin coincidencias fuera de
constantes de prueba (`F-101` en specs de admin-web).

> **Tarea 0 — ejecutada el 2026-08-16 contra la base real. Hipótesis CONFIRMADA.**
>
> La consulta sobre `livestock.animal_identifiers` devolvió **0 filas** para `101`, `102` y
> `103`. Se amplió a un barrido de **toda columna de texto de los seis esquemas**
> (`livestock`, `breeding`, `inventory`, `people`, `production`, `tasks`): **0 coincidencias**.
>
> La base contiene 67 animales, ninguno borrado, y solo 14 identificadores, todos de tipo
> `Name` y todos nombres propios (`Alfonsina`, `Carlota`, `Caya`, `Gringa`, `Imelda`,
> `Ines`, `Kiara`, `Kika`, `Luna`, `Marisol`, `Maya`, `Mocha`, `Muñeca`, `Susi`). No existe
> ni un solo `FarmTag`.
>
> Los `101/102/103` no están en Postgres bajo ninguna forma. Son filas huérfanas en el
> WatermelonDB del dispositivo. El plan procede tal cual.

### 2.5 No hay forma de reparar una base local desalineada

No existe `unsafeResetDatabase`, purga, ni acción equivalente en
`src/screens/SyncStatusScreen.tsx`, `src/services/syncEngine.ts` o `src/database/index.ts`.
Un teléfono con datos huérfanos no tiene remedio salvo desinstalar la app — lo que también
destruiría el outbox. Esto convierte al hallazgo 2.4 en permanente.

### 2.6 Tres colecciones se descartan en silencio

El servidor lee y envía `healthPlans`, `healthPlanItems` y `healthPlanAssignments` en cada
pull (`SyncPullQueries.cs:375-377`), pero ninguna existe en `TABLE_BY_COLLECTION`
(`syncEngine.ts:298`) ni en `schema.ts`. `applyCollections` hace `continue` sin registrar
nada.

Es ancho de banda desperdiciado, pero lo grave es el precedente: **si se agrega
`pregnancies` al servidor y se olvida el mapeo del cliente, el filtro de preñadas falla
exactamente igual, sin un solo error visible.**

### 2.7 `applyRow` fuerza `isDeleted = false`

`syncEngine.ts:365` sobrescribe `record.isDeleted = false` en toda fila entrante, sin
importar lo que traiga el servidor. Hoy es inocuo porque los tombstones se manejan antes,
en `applyCollections`. Es una bomba de tiempo: cualquier colección futura que use borrado
lógico local en vez de destrucción física quedará resucitada en cada pull.

### 2.8 La etiqueta de respaldo colapsa: 53 de 67 animales se llaman igual

Hallazgo nuevo, surgido de la tarea 0. Los ids de animales en la base real tienen todos la
forma `00000000-0000-5000-8000-XXXXXXXX0000` — son UUID deterministas de carga, no
aleatorios. Y `loadHerd` (`src/services/herdQueries.ts`) construye la etiqueta de respaldo
para un animal sin identificador así:

```ts
label = `Sin arete · ${animal.id.slice(0, 6)}`;
```

Los primeros seis caracteres de **todos** esos ids son `000000`. Como 53 de los 67 animales
no tienen ningún identificador vigente, la app los muestra a los 53 con la etiqueta
idéntica **`Sin arete · 000000`**: indistinguibles en todo selector de la app.

El propósito de ese `slice` era dar algo con que distinguir un animal sin arete de otro. Con
los ids reales de esta finca no distingue nada. Afecta a `loadPregnantDams`, que reutiliza
la misma regla de etiqueta.

**Mitigación del riesgo inmediato:** las seis hembras preñadas de hoy sí tienen nombre, así
que el paso 1 del asistente no se ve afectado por ahora. Pero la regla es frágil y la
comparte toda la app.

### 2.9 `HomeScreen.tsx` es código muerto

`src/App.tsx:209` renderiza `ActivitiesHub` para `tab === 'home'`. `HomeScreen.tsx` está
importado (`App.tsx:29`) pero nunca se renderiza. Su test `tests/HomeScreen.test.tsx` sigue
pasando y da una falsa sensación de cobertura.

## 3. Decisiones fijadas

| # | Decisión | Justificación |
|---|---|---|
| D1 | Una sola rama para sync + partos + Inicio | El filtro de preñadas depende de la capa de sync; separarlos dejaría la mitad del pedido sin cumplir. |
| D2 | **Bloqueo estricto**: solo hembras con preñez activa | Decisión del cliente. La preñez se puede registrar retroactivamente en admin-web con fechas antiguas, así que el caso "parió y no estaba registrada" se gestiona allá. Sin botón de escape en la app. |
| D3 | El padre es de **solo lectura** en el parto | El backend ya lo deduce de la preñez (2.3). El operario no debe poder contradecirlo desde el potrero. |
| D4 | En inseminación artificial se muestra **etiqueta genérica** | Decisión del cliente: `"Padre: Inseminación artificial"`, sin lote de pajuela. El detalle del semen le sirve al administrador, no al empleado en el potrero. |
| D5 | Asistente de **cuatro pasos**, un paso una decisión | Resuelve el problema de espacio de raíz en vez de recortar márgenes. Validado con mockups. |
| D6 | El paso de crías tiene header y footer **fijos**, y solo la lista scrollea | Una camada porcina puede traer 20 lechones; el contador y los botones `+Hembra`/`+Macho` no pueden desplazarse fuera de vista. |
| D7 | **"Rehacer descarga"** manual en Sincronización, no reconciliación automática | Resuelve el caso real hoy a costo bajo. La reconciliación por checksum es un protocolo nuevo (endpoint, paginación, tests) que agrandaría la rama sin resolver nada que el botón no resuelva. |
| D8 | El reset **nunca** toca `sync_outbox` | Regla dura 10 y Art. 9: lo no enviado es la única copia que existe. |
| D9 | Los cuatro hallazgos de sync se corrigen en esta rama | Decisión del cliente, tomada tras señalarle que roza el "un PR = un propósito" del AGENTS.md. Se acepta porque los cuatro pertenecen a la misma capa (`syncEngine.ts` + `SyncPullQueries.cs`) y el filtro de preñadas se apoya en ella: entregarlo sobre una capa que puede mentir reproduce el mismo síntoma que el cliente reportó. Se mitiga con commits separados por propósito dentro de la rama. |
| D10 | Se **elimina** `HomeScreen.tsx` y su test | Código muerto (2.9). Borrado de archivo sin uso, no de datos — la regla dura 1 no aplica. |
| D11 | La etiqueta de respaldo usa los **últimos** caracteres distintivos del id, no los primeros seis | El hallazgo 2.8 muestra que los primeros seis son constantes en la base real. `loadPregnantDams` hereda esta regla, así que corregirla es parte del trabajo, no deuda ajena. |

## 4. Alcance

### Entra

- Colecciones `pregnancies` y `breedingServices` en el pull, con su espejo local.
- Guard de colecciones desconocidas en `applyCollections`.
- Retiro de `healthPlans` / `healthPlanItems` / `healthPlanAssignments` del pull.
- Corrección de `applyRow` (`isDeleted`).
- Acción "Rehacer descarga" en `SyncStatusScreen`.
- `loadPregnantDams` en `herdQueries.ts`.
- Corrección de la etiqueta de respaldo de `loadHerd` (D11).
- Rediseño de `BirthScreen` a asistente de cuatro pasos.
- Rediseño de espaciado y scroll de `ActivitiesHub`.
- Eliminación de `HomeScreen.tsx` y su test.

### No entra

- Cualquier cambio en `Hato.Modules.Breeding.Domain` — el dominio ya hace lo correcto (2.3).
- Registrar preñeces o servicios **desde** la app. Sigue siendo tarea de admin-web.
- Reconciliación automática de existencia por checksum → `BACKLOG.md`.
- Rediseño de otras pantallas de la app (`MilkingScreen`, `EventsScreen`, `TreatScreen`,
  `LotEventsScreen`). Comparten la misma raíz de problema pero no están en el pedido.
- Cambios en el tema (`src/ui/theme.ts`). Los tokens actuales — 64pt de objetivo táctil,
  contraste alto — responden a restricciones de campo, no a gusto estético, y siguen siendo
  correctos.

## 5. Diseño: capa de sincronización

### 5.1 Guard de colecciones desconocidas

`applyCollections` deja de saltarse en silencio una colección sin mapear. Ante un nombre
desconocido registra el hecho por `loggerService` y lo cuenta como error de sincronización
visible en `SyncStatusScreen`, en vez de un `continue` mudo.

El criterio: una colección que el servidor envía y el cliente no consume es siempre un error
de programación — o falta el mapeo, o sobra el envío. Ninguna de las dos debe ser silenciosa.

### 5.2 Colecciones sobrantes fuera del pull

`healthPlans`, `healthPlanItems` y `healthPlanAssignments` se retiran de
`RequiredPermissionByCollection`, de `SyncCollectionsDto` y de las lecturas del handler. Se
retiran en vez de crear tablas locales espejo porque **ninguna pantalla de la app las
consume**; crear tablas muertas para satisfacer al guard sería invertir la causalidad.

### 5.3 `applyRow` respeta el `isDeleted` del servidor

Se elimina la asignación incondicional `record.isDeleted = false`. La fila entrante escribe
el valor que el servidor manda; los tombstones se siguen manejando antes, en
`applyCollections`, sin cambios.

### 5.4 "Rehacer descarga"

Acción en `SyncStatusScreen`, con confirmación explícita antes de ejecutar:

1. Borra todas las tablas **espejo** (las que se listan en `TABLE_BY_COLLECTION`).
2. Borra la fila `pull_cursor` de `sync_meta`.
3. Dispara un `syncNow()`.

**No toca `sync_outbox`, `milk_yields` ni el resto de tablas locales** (D8). La confirmación
debe decir con qué se queda el empleado y con qué no, en español y sin jerga: lo que registró
y no se ha enviado se conserva; el hato se vuelve a bajar del servidor.

Como `syncNow()` empuja el outbox **antes** de bajar (`syncEngine.ts`, `pushOutbox` precede a
`pullChanges`), el orden natural ya protege lo pendiente.

## 6. Diseño: datos de preñez en el dispositivo

### 6.1 Servidor

Dos colecciones nuevas en el pull, siguiendo el patrón existente de `ReadAsync` + DTO
+ permiso. Ambas bajo `SystemPermissions.BreedingEventsRead`, que ya existe
(`src/Modules/People/Hato.Modules.People.Domain/UserRole.cs:79`).

```
pregnancies       → id, damId, serviceId, status, expectedBirthDate, createdAt, updatedAt, isDeleted
breedingServices  → id, damId, serviceType, sireAnimalId, strawId, createdAt, updatedAt, isDeleted
```

`status` viaja como cadena (`Active` / `Aborted` / `Completed`), igual que
`PregnancyStatus` ya se serializa por su `JsonStringEnumConverter`. Lo mismo `serviceType`
(`Natural` / `ArtificialInsemination`).

Requiere inyectar `IBreedingDbContext` en `GetSyncPullQueryHandler`, que hoy recibe
Livestock, Inventory y People.

**No se transporta el detalle del lote de pajuela** (D4): el `strawId` viaja solo para poder
distinguir "hay padre por IA" de "no hay padre", no para mostrarse.

### 6.2 Dispositivo

- `schema.ts`: dos `tableSchema` nuevos, `SCHEMA_VERSION` sube de 10 a **11**.
- `migrations.ts`: paso `toVersion: 11` con dos `createTable`. Obligatorio — sin él
  WatermelonDB se niega a abrir la base y el empleado pierde la cola (regla dura 7 en
  espíritu; el comentario de cabecera de `migrations.ts` ya lo advierte).
- `models.ts`: modelos `Pregnancy` y `BreedingService`, registrados en `modelClasses`.
- `syncEngine.ts`: entradas `pregnancies` y `breedingServices` en `TABLE_BY_COLLECTION`.

### 6.3 `loadPregnantDams`

Nueva función en `src/services/herdQueries.ts`, junto a `loadHerd`:

```ts
export interface PregnantDam {
  animalId: string;
  label: string;            // misma regla de etiqueta que loadHerd
  expectedBirthDate?: string;
  sireLabel: string;        // ver tabla abajo
  pregnancyId: string;
}
```

`sireLabel` se resuelve así, y esta tabla es la definición completa:

| Situación | Etiqueta |
|---|---|
| Preñez sin `serviceId` | `Padre: sin registrar` |
| Servicio `ArtificialInsemination` | `Padre: Inseminación artificial` |
| Servicio `Natural` con `sireAnimalId` que existe local | `Padre: <etiqueta del animal>` |
| Servicio `Natural` con `sireAnimalId` que no existe local | `Padre: sin registrar` |

El último caso importa: el semental pudo no haberse sincronizado todavía, o pudo ser una
fila huérfana. Mostrar `sin registrar` es honesto; inventar un identificador no lo es.

Filtra por `status === 'Active'` y `!isDeleted`, cruza contra `loadHerd` para excluir
animales borrados, y ordena por `expectedBirthDate` ascendente — la más próxima a parir
primero, que es el orden en que el empleado las va a buscar.

### 6.4 Envío del parto

`birthService.ts` deja de aceptar `sireAnimalId` / `sireStrawId` desde la pantalla y pasa a
enviar `pregnancyId`. El servidor deduce el padre (2.3). Los campos de padre se mantienen en
la interfaz `RecordBirthInput` porque el contrato del servidor los sigue aceptando y otros
consumidores futuros podrían usarlos, pero `BirthScreen` ya no los rellena.

## 7. Diseño: asistente de parto en cuatro pasos

`BirthScreen.tsx` se parte en un contenedor de pasos más un componente por paso, cada uno en
su archivo bajo `src/screens/birth/`. Ningún archivo del conjunto debería pasar de ~150
líneas.

### Paso 1 — Elegir madre
Lista de `loadPregnantDams`, una fila por madre con su fecha probable de parto. Ocupa la
pantalla completa. Si la lista está vacía, `EmptyState` explicando que solo aparecen hembras
con preñez activa confirmada y que la preñez se registra en el panel — no un vacío mudo.

### Paso 2 — Confirmar datos
Tarjetas de solo lectura: padre (según la tabla de 6.3), fecha del parto (hoy por defecto,
editable) y dificultad (`Normal` por defecto). Sin lista de sementales.

### Paso 3 — Registrar crías
Header fijo (contador total y desglose M/F), lista de crías scrolleable que crece hasta
ocupar el alto disponible, footer fijo con `+Hembra` / `+Macho` y avanzar. Se conserva
íntegra la funcionalidad actual por cría: arete opcional, peso al nacer opcional con
validación de decimal positivo, cambio de sexo y quitar. La corrección de 3.5a.0 #2 (camada
editable, no solo-agregar) se mantiene.

### Paso 4 — Revisar y confirmar
Resumen de madre, padre y crías. Confirmar encola en el outbox y vuelve a Inicio.

### Navegación
Indicador de progreso de cuatro puntos en todos los pasos. Atrás conserva lo ya cargado —
retroceder no puede costarle al empleado los cinco lechones que ya tecleó.

## 8. Diseño: Inicio (`ActivitiesHub`)

Misma raíz que el problema de partos: `Screen` sin scroll con dos tarjetas que apilan hasta
nueve botones de 64pt mínimo. Se activa `scrollable`, se revisa el espaciado entre grupos y
se conserva el orden actual de sujetos — el orden está fijado por
`tests/ActivitiesHub.test.tsx` justamente para que un reordenamiento sea deliberado
([`fase-3-5/spec.md`](../fase-3-5/spec.md#7-lo-que-sólo-el-cliente-puede-responder) sec. 7-C
lo marca como pregunta abierta para el cliente). **Este spec
no lo reordena.**

Se elimina `HomeScreen.tsx`, su import en `App.tsx` y `tests/HomeScreen.test.tsx` (D10).

## 9. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| La hipótesis de los `101/102/103` es falsa | Tarea 0 bloqueante la verifica por SQL antes de escribir código. |
| El bloqueo estricto deja al empleado sin poder registrar un parto real | Aceptado por el cliente (D2): la preñez se registra retroactivamente en admin-web. El `EmptyState` del paso 1 debe decirlo con claridad. |
| Rehacer descarga sobre una conexión mala deja el teléfono sin hato | La confirmación advierte que requiere señal. El outbox sobrevive en cualquier caso (D8). |
| La rama mezcla propósitos (D9) | Commits separados por propósito; el PR los enumera en orden. |

**A registrar en `BACKLOG.md`:**

- Reconciliación automática de existencia entre servidor y dispositivo (checksum o lista de
  ids vigentes por colección), que haría innecesario el botón manual.
- Las demás pantallas de la app comparten la raíz del problema de espacio: `MilkingScreen`,
  `EventsScreen`, `TreatScreen`, `LotEventsScreen`.

## 10. Criterios de aceptación

1. En el paso 1 solo aparecen hembras con preñez activa confirmada en el servidor.
2. Una hembra cuyo parto ya se registró y sincronizó desaparece de la lista tras el
   siguiente pull, sin intervención manual.
3. La pantalla de parto no ofrece en ningún paso elegir el padre.
4. El padre mostrado coincide con el servicio registrado en admin-web, según la tabla de 6.3.
5. Una camada de 20 crías es completamente navegable: contador y botones siempre visibles.
6. "Rehacer descarga" elimina las filas huérfanas y conserva íntegro el outbox.
7. Una colección enviada por el servidor sin mapeo en el cliente produce un error visible.
8. El Inicio es navegable completo en un teléfono chico sin contenido cortado.
9. La suite completa pasa: `dotnet test` y `npm test` en `clients/field-app`.
