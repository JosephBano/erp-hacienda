# spec.md — Pantalla `/animal-groups`: CRUD, resumen y `TrackingMode` mutable

> **Documento archivado.** El trabajo está terminado: el ADR-0025 se mergeó en el PR #78 y el
> plan se ejecutó en los PRs #80 (dominio), #81 (endpoints), #82 (frontend) y #83 (fix
> post-review), cerrados el 2026-08-08. Esta carpeta es el registro histórico de lo que se
> construyó, no un plan activo. Se marca aquí, en el encabezado, según la regla de archivado
> de `docs/DOCUMENTACION.md` sec. 4 — no se borra ni se vacía.
>
> **Qué es este documento.** La especificación de la pantalla de administración de lotes del
> panel admin-web, tal como quedó ejecutada: qué se construyó, qué decisiones quedaron fijadas
> y qué se dejó deliberadamente afuera. El *cómo se ejecutó* vive en [`plan.md`](./plan.md),
> el desglose con casillas en [`tasks.md`](./tasks.md) y la verificación manual en
> [`test-e2e.md`](./test-e2e.md).
>
> **Por qué esta subcarpeta.** Los cuatro documentos son un solo entregable de una sola serie
> de PRs; separarlos en `docs/planes/` los dejaría huérfanos entre sí. Es el mismo motivo y el
> mismo precedente que `reestructura-documentacion/`.
>
> **Procedencia exacta del contenido.** Este documento y los otros tres de la carpeta son la
> conversión de `docs/planes/PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` (556 líneas, un solo archivo que
> mezclaba decisión, secuencia, checklist y verificación) a la convención de cuatro documentos
> de `docs/plantillas/`. **No se perdió contenido:** lo que era decisión quedó acá, la
> secuencia de PRs en `plan.md`, el desglose en `tasks.md` y los guiones manuales en
> `test-e2e.md`. Las citas del original a sus propias secciones (`§3.2`, `§5.4`, `§8`) se
> reubicaron a la sección equivalente de esta carpeta.

- **Rama Git del plan original:** `docs/plan-admin-web-animal-groups`.
- **Ramas de implementación:** `feature/livestock-animal-groups-domain`,
  `feature/livestock-animal-groups-endpoints`, `feature/admin-web-animal-groups-screen`.
- **Fecha del plan original:** 2026-08-08. **Fecha de conversión:** 2026-08-17.
- **Fase del ROADMAP:** transversal a la Fase 3.5. No abre ni cierra fase: cierra un hueco de
  UI administrativa. El desacople del piloto (ADR-0024) habilitó entrarle sin esperar a
  `3.5a.7`.
- **ADRs vigentes que respalda o respeta:** ADR-0025 (gestión administrativa de grupos, el que
  este trabajo ejecuta), ADR-0007 (modelo de permisos en BD), ADR-0015 (lote por conteo,
  origen de `TrackingMode`), ADR-0024 (desacople del piloto).
- **Reglas duras que gobiernan este trabajo:** `AGENTS.md` regla 4 (rama desde `develop`) y
  regla 9 (un PR, un propósito) — que es la razón de que sean tres PRs secuenciales y no uno.

---

## Índice

1. [Por qué existe este spec](#1-por-qué-existe-este-spec)
2. [Hallazgos verificados](#2-hallazgos-verificados)
3. [Decisiones fijadas](#3-decisiones-fijadas)
4. [Alcance](#4-alcance)
5. [Diseño: dominio y aplicación](#5-diseño-dominio-y-aplicación)
6. [Diseño: superficie REST y autorización](#6-diseño-superficie-rest-y-autorización)
7. [Diseño: pantalla admin-web](#7-diseño-pantalla-admin-web)
8. [Riesgos y deuda](#8-riesgos-y-deuda)
9. [Criterios de aceptación](#9-criterios-de-aceptación)

---

## 1. Por qué existe este spec

`docs/BACKLOG.md` sec. `[UI]` declaraba un hueco concreto: **no había forma de configurar
lotes desde el panel**. El backend sabía crear grupos y registrarles eventos, pero un
administrador no podía renombrar uno, corregirle la especie, desactivarlo ni cambiarle el modo
de seguimiento sin entrar a la base de datos a mano.

El costo no era teórico. `TrackingMode` decide si un lote se maneja por animal identificado o
por conteo de cabezas (ADR-0015), y es la clase de campo que se elige mal la primera vez:
alguien crea el lote de lechones como `Individual` porque es el valor por defecto y después no
tiene cómo corregirlo. Sin pantalla, la única salida era un `UPDATE` manual.

## 2. Hallazgos verificados

Los tres hechos que motivaron el plan, comprobados leyendo el código el **2026-08-08**. Se
conservan como quedaron escritos —son el estado *antes* del trabajo— y cada uno lleva al lado
el estado actual, verificado el 2026-08-17.

### 2.1 El dominio tenía la mitad del CRUD

`AnimalGroup` tenía `Create`/`Get*`/`AddMember`/`RemoveMember`/`RecordGroupEvent`, pero **no**
`Update`/`Deactivate`/`Activate`/`ChangeTrackingMode` ni los endpoints REST correspondientes
(`src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs:1-98` en aquel momento).

**Hoy:** los cuatro existen —
`src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalGroup.cs:42` (`Update`), `:52`
(`Deactivate`), `:61` (`Activate`), `:74` (`ChangeTrackingMode`).

### 2.2 El panel no tenía pantalla de grupos

El admin-web tenía 13 pantallas en `clients/admin-web/`, ninguna para grupos
(`src/app/app.routes.ts:17-46`). `services/api.service.ts:1-597` no consumía
`/api/v1/animal-groups`.

**Hoy:** tres rutas (`app.routes.ts:31`, `:36`, `:41`), tres componentes
(`components/animal-groups-list/`, `animal-group-create/`, `animal-group-detail/`) y los ocho
métodos de `ApiService` (`api.service.ts:842-853` y siguientes).

### 2.3 El DTO de lista obligaba a la UI a hacer fan-out

`AnimalGroupDto` (`GetAnimalGroupQueries.cs:11-18` en aquel momento) no incluía
`LiveHeadCount` ni `SpeciesName`. La fórmula de cabezas vivas ya existía dentro del summary
(`:91-149`) pero estaba duplicada en intención: si la lista la quería, tenía que pedir el
summary de cada fila.

**Hoy:** ambos campos son parte del DTO
(`GetAnimalGroupQueries.cs:15-24`) y la fórmula está extraída en un solo lugar,
`LiveHeadCountCalculator.Compute` (`LiveHeadCountCalculator.cs:12`), usada por la lista y por
el summary.

## 3. Decisiones fijadas

Las cinco primeras las fija el [ADR-0025](../../adr/0025-gestion-administrativa-grupos.md) y
este trabajo las ejecuta; las tres últimas las cerró el plan, no el ADR.

- **D1 — El permiso es `livestock.animals.write`, sin permiso nuevo.**
  No hay migración RBAC. Se descartó `livestock.animal-groups.manage`: un permiso más por cada
  pantalla administrativa multiplica la matriz de roles sin comprar seguridad real. Si la
  condición de reversa dispara, se reabre el ADR.

- **D2 — El borrado es lógico y reversible: `IsActive = false`.**
  No se asigna `DeletedAt`. Un lote desactivado se puede reactivar; un lote borrado se lleva
  su historia de eventos por delante, y esa historia es el registro de la finca.

- **D3 — `TrackingMode` es mutable, pero sólo mientras el lote esté vacío.**
  Cambiarlo con membresías activas o eventos registrados reinterpretaría datos ya capturados:
  los mismos eventos significan cosas distintas según el modo. La guarda vive en el handler
  (409), no en la entidad, porque necesita consultar la base.

- **D4 — `LiveHeadCount` y `SpeciesName` se calculan en el servidor.**
  La alternativa —que la UI pida el summary de cada fila— es un fan-out de N peticiones para
  pintar una tabla.

- **D5 — Sin `HasQueryFilter` en `AnimalGroupConfiguration`.**
  Un filtro global de `IsActive` esconde los inactivos de *todas* las consultas, incluidas las
  del sync y las de administración, que son precisamente las que necesitan verlos.

- **D6 — `SpeciesName` se resuelve con un `IN (...)` aparte, no con un `LEFT JOIN`.**
  Decidido en el plan, no en el ADR. `GetAnimalGroupByIdHandler` ya cargaba `Species` por
  separado; mantener la misma forma en la lista es más fácil de leer y es lo que el
  `DbContext` hace en otros casos.

- **D7 — Sin LWW para `AnimalGroup`.**
  La pantalla hace `PUT` directo, síncrono y en línea; no declara `knownUpdatedAt`. Es
  consistente con el patrón documentado en `ARCHITECTURE.md` sec. "Conflictos LWW en entidades
  editables" para `Animal`. La deuda sigue rastreada en `docs/BACKLOG.md`.

- **D8 — Los campos derivados no se sincronizan.**
  `SyncAnimalGroupDto` queda igual. `LiveHeadCount` y `SpeciesName` son proyecciones de
  lectura del panel; replicarlos al móvil los volvería un dato que puede quedar viejo.

## 4. Alcance

### Entra

- `Activate()` y `ChangeTrackingMode()` en la entidad `AnimalGroup`.
- Cuatro comandos de aplicación: `Update`, `Deactivate`, `Activate`,
  `ChangeAnimalGroupTrackingMode`.
- `AnimalGroupDto` extendido con `LiveHeadCount` y `SpeciesName`, con la agregación
  server-side.
- `LiveHeadCountCalculator` extraído y reusado por el summary.
- Cuatro verbos REST nuevos (`PUT`, `DELETE`, `POST /activate`, `PATCH /tracking-mode`) con su
  policy de autorización.
- Endurecimiento del `POST /` existente, que no exigía permiso (hueco preexistente).
- Tres pantallas en admin-web: lista, creación y detalle; entrada en el sidebar; ocho métodos
  nuevos en `ApiService`.

### No entra

- **Migración EF Core.** Ninguno de los tres PRs cambia el esquema: `DeletedAt`/`UpdatedAt`
  los hereda `AuditableEntity`.
- **Sincronización.** Los campos derivados no bajan al móvil (D8), y el pull sigue entregando
  `SyncAnimalGroupDto` igual que antes.
- **Modal reutilizable de confirmación.** El botón de dos pasos cubre la única acción
  destructiva de esta serie. Queda anotado en `docs/BACKLOG.md` con su disparador: la segunda
  acción destructiva que aparezca en admin-web.
- **Icono dedicado de grupo/lote.** Se usa `'tag'` como placeholder
  (`app.component.html:107`). Anotado en `docs/BACKLOG.md`.
- **Índice en `AnimalGroup.IsActive`.** Con menos de 100 grupos es invisible. Anotado en
  `docs/BACKLOG.md` con disparador de >500 grupos activos.
- **Sub-página `/animal-groups/:id/edit`.** La edición ocurre inline dentro del detalle.
- **Los catálogos puros** (especies, causas de mortalidad, ítems de inventario). El ítem
  hermano del BACKLOG mantiene su disparador original.

## 5. Diseño: dominio y aplicación

Dos métodos nuevos en la entidad, y las invariantes repartidas según qué información necesitan:

- `Activate()` — pone `IsActive = true`, idempotente. `Deactivate()` ya existía y se mantiene
  idempotente.
- `ChangeTrackingMode(newMode)` — no-op si el modo no cambia; lanza `DomainException` si el
  grupo está inactivo; asigna en cualquier otro caso.

**Dónde vive cada guarda, y por qué.** La invariante "no se cambia el modo de un grupo
inactivo" es puramente de estado propio, así que vive en la entidad. Las guardas "no tiene
membresías activas" y "no tiene eventos" necesitan consultar la base, así que viven en el
handler (`ChangeAnimalGroupTrackingModeCommand.cs:51` y `:57`) y responden 409. Meterlas en la
entidad la obligaría a conocer el `DbContext`.

**Agregación de la lista.** `GetAnimalGroupsHandler` resuelve la tabla con consultas agregadas
en vez de una por fila: los grupos con sus membresías, el conteo de membresías activas
agrupado por grupo, la suma de bajas por grupo, y las especies por `IN (...)` (D6). Se combinan
en memoria.

## 6. Diseño: superficie REST y autorización

Cuatro verbos nuevos sobre `/api/v1/animal-groups`, todos exigiendo
`SystemPermissions.LivestockAnimalsWrite` (D1):

| Verbo | Ruta | Respuesta |
|---|---|---|
| `PUT` | `/{id}` | 204; 400 si el nombre es vacío; 404 si no existe |
| `DELETE` | `/{id}` | 204, idempotente |
| `POST` | `/{id}/activate` | 204, idempotente |
| `PATCH` | `/{id}/tracking-mode` | 204; 409 con membresías o eventos; 400 si está inactivo |

**Endurecimiento colateral.** El `POST /` de creación no exigía permiso: cualquier usuario
autenticado podía crear grupos. Era un hueco preexistente, no introducido por este trabajo, y
se cerró en el mismo PR que agregó los otros verbos porque dejarlo abierto mientras se
endurecía todo lo demás no tenía defensa. El mismo endurecimiento alcanzó a `POST /events`,
`POST /members` y `DELETE /members/{id}`.

## 7. Diseño: pantalla admin-web

Tres componentes, siguiendo las convenciones ya establecidas del panel: signals,
`CatalogTableComponent` para la tabla, formularios con `[(ngModel)]` y `FormsModule` —**no**
Reactive Forms, que no es convención del proyecto— y errores con
`err?.error?.detail || 'No se pudo X.'`.

- **Lista** — columnas Nombre, Especie, Modo, Cabezas vivas, Activo. Acciones: editar
  (navega al detalle) y desactivar/reactivar con confirmación de dos pasos.
- **Creación** — nombre, descripción, especie desde `getSpecies()`, y el radio de
  `TrackingMode` con el texto de ayuda de abajo.
- **Detalle** — encabezado, resumen de cinco tarjetas, miembros y eventos. Edición inline de
  nombre/descripción/especie; el cambio de `TrackingMode` va por un flujo separado con su
  propia advertencia, precisamente porque puede fallar con 409.

### 7.1 Texto de ayuda del modo de seguimiento

Se conserva literal: es la explicación que ve el administrador y la única defensa contra
elegir mal el modo.

> **Modo de seguimiento**
>
> Elegí cómo el sistema va a tratar a los miembros de este lote. Esto define qué clase de
> eventos podés registrar y cómo se atribuyen.
>
> ◯ **Individual**
> El sistema conoce a cada miembro por separado (arete, SIFAE, RFID). Use este modo para
> ganado identificado: los eventos pueden ser por animal o por lote, y el linaje se mantiene.
>
> ◯ **Por conteo (sin identificar)**
> El sistema solo sabe cuántas cabezas hay, no cuáles. Use este modo para lotes sin
> identificación (lechones al destete, pollos de engorde, terneros sin arete). Los eventos se
> registran sobre el lote completo.
>
> **Atención:** una vez que el grupo tenga miembros activos o eventos registrados, no se puede
> cambiar el modo. Si te equivocaste, desactivá este grupo y creá uno nuevo con el modo
> correcto.

## 8. Riesgos y deuda

| Riesgo | Mitigación |
|---|---|
| **La agregación de `GetAnimalGroupsHandler` introduce un N+1 si se rompe el batching.** No falla ninguna prueba funcional: sólo se pone lento. | Prueba de integración que cuenta consultas con `DbCommandInterceptor`: 50 grupos, ≤4 consultas. |
| **El endurecimiento del `POST /` rompe consumidores existentes.** | Prueba que verifica el 403 sin permiso. El único llamador conocido es `admin`, que tiene el permiso; el riesgo real es bajo. |
| **El formulario de `TrackingMode` excede la promesa de "tres toques" del plan porcino.** | No aplica: esa promesa es del árbol de la app de campo, no del panel administrativo. |
| **El set de iconos está cerrado y no tiene "grupo".** | `'tag'` como placeholder con TODO, y entrada en `docs/BACKLOG.md`. |

**Deuda que este trabajo declara**, toda anotada en `docs/BACKLOG.md`: el
`ConfirmDialogComponent` reutilizable, el icono dedicado y el índice en `AnimalGroup.IsActive`.

**Deuda que este trabajo paga:** el ítem `[UI] admin-web: pantallas de catálogos que faltan`
queda **parcialmente** satisfecho — `animalGroups` tiene pantalla; los catálogos puros siguen
pendientes con su disparador original.

## 9. Criterios de aceptación

1. `AnimalGroup` expone `Update`, `Deactivate`, `Activate` y `ChangeTrackingMode`, y los dos
   últimos son idempotentes ante el mismo valor.
2. Los cuatro verbos REST responden los códigos de la tabla de la sec. 6, incluidos el 409 con
   actividad y el 400 con el grupo inactivo.
3. Ningún endpoint de `/api/v1/animal-groups` responde sin exigir
   `livestock.animals.write` — incluido el `POST /` de creación.
4. `AnimalGroupDto` incluye `LiveHeadCount` y `SpeciesName`, y la fórmula de cabezas vivas
   existe en un solo lugar del código.
5. `ls clients/admin-web/src/app/components/animal-group*` devuelve los tres componentes, cada
   uno con su `.spec.ts`.
6. El sidebar muestra "Lotes y Grupos" sólo a quien tiene el permiso, y la ruta redirige a
   quien no lo tiene.
7. Ninguno de los tres PRs agrega una migración EF Core.
8. `dotnet test` y `npm test` en verde.
9. Las tres deudas declaradas en la sec. 8 tienen entrada propia en `docs/BACKLOG.md`.
