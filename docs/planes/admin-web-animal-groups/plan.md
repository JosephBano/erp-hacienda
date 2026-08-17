# plan.md — Ejecución de la serie `admin-web-animal-groups`

> **Documento archivado.** La secuencia de abajo se ejecutó completa entre el 2026-08-08 y el
> cierre de la serie: PR #78 (ADR), #80 (dominio), #81 (endpoints), #82 (frontend) y #83 (fix
> post-review). Se conserva como registro de en qué orden se hizo y por qué ese orden.
>
> **Qué es este documento.** El orden de ejecución de lo que [`spec.md`](./spec.md) decidió.
> El desglose con casillas está en [`tasks.md`](./tasks.md) y la verificación manual en
> [`test-e2e.md`](./test-e2e.md).

**Objetivo:** cerrar el hueco "no hay UI para configurar lotes en el panel", ejecutando el
ADR-0025 sin que ningún PR mezcle capas.

**Enfoque:** **tres PRs secuenciales**, uno por capa, más el PR del ADR que los precede. No es
una rama con tres commits: son tres ramas y tres revisiones, porque la regla 9 de `AGENTS.md`
—un PR, un propósito— aplica con más fuerza cuando el trabajo cruza dominio, API y frontend.
Cada PR queda con la suite en verde antes de que empiece el siguiente.

**Spec:** [`spec.md`](./spec.md).

---

## Restricciones globales

Copiadas de las decisiones de `spec.md` que aplican a los tres PRs:

- **Ningún PR agrega una migración EF Core.** No cambia el esquema (sec. 4, "No entra").
- **Ningún PR toca `SyncAnimalGroupDto`** ni el pull de sincronización (D8).
- **Ningún PR toca `develop` directamente.** Los tres van por PR con revisión.
- **TDD en PR1 y PR2:** todo handler o comando nuevo tiene su prueba escrita primero, en rojo,
  y después la implementación. Nada de escribir pruebas al final para confirmar lo que ya se
  hizo.
- **Ningún PR se marca completo** sin `dotnet build` y `dotnet test` en exit 0, sin warnings de
  compilación nuevos, y —en el PR3— `ng test --watch=false` y `ng build` también en 0.

---

## Índice

1. [PR1 — Dominio y aplicación](#pr1--dominio-y-aplicación)
2. [PR2 — Endpoints REST y autorización](#pr2--endpoints-rest-y-autorización)
3. [PR3 — Pantalla admin-web](#pr3--pantalla-admin-web)
4. [Orden, dependencias y puntos de no retorno](#orden-dependencias-y-puntos-de-no-retorno)
5. [Descripción del PR](#descripción-del-pr)

---

## PR1 — Dominio y aplicación

`feat(livestock): complete the animal-group CRUD in domain and application`

- **Rama:** `feature/livestock-animal-groups-domain`
- **Origen:** `develop`, con el PR #78 (ADR-0025) ya mergeado.

**Por qué acá:** los otros dos PRs no compilan sin los comandos y el DTO que este crea. Y va
después del ADR porque las invariantes y la forma del DTO lo referencian: sin el ADR aceptado,
un revisor podría pedir cambios incompatibles con lo ya construido.

**Archivos:**
- Modificar `…/Livestock.Domain/AnimalGroup.cs` — agregar `Activate()` y
  `ChangeTrackingMode(newMode)`; verificar con una prueba nueva que `Deactivate()` sigue siendo
  idempotente.
- Crear `…/Application/AnimalGroups/UpdateAnimalGroupCommand.cs`,
  `DeactivateAnimalGroupCommand.cs`, `ActivateAnimalGroupCommand.cs` y
  `ChangeAnimalGroupTrackingModeCommand.cs` — este último con las guardas de membresías y
  eventos que devuelven 409.
- Crear `…/Application/AnimalGroups/LiveHeadCountCalculator.cs` — la fórmula, extraída.
- Modificar `…/Application/AnimalGroups/GetAnimalGroupQueries.cs` — extender `AnimalGroupDto`
  con `LiveHeadCount` y `SpeciesName`; reescribir `GetAnimalGroupsHandler` con la agregación
  server-side; refactorizar `GetAnimalGroupSummaryHandler` para que use el calculador
  **sin cambiar su comportamiento**.

**Verificación:** `dotnet test` en verde con los escenarios nuevos de dominio y de integración,
incluido el que cuenta consultas (50 grupos, ≤4 consultas). Sin endpoints y sin UI: el smoke de
este PR es la suite, no `curl` — los verbos todavía no están expuestos.

## PR2 — Endpoints REST y autorización

`feat(api): expose the animal-group CRUD verbs behind the write permission`

- **Rama:** `feature/livestock-animal-groups-endpoints`
- **Origen:** `develop`, con PR1 mergeado.

**Por qué acá:** usa los handlers y DTOs de PR1, y tiene que existir antes de que el frontend
tenga qué consumir.

**Archivos:**
- Modificar `src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs` — agregar `PUT /{id}`,
  `DELETE /{id}`, `POST /{id}/activate` y `PATCH /{id}/tracking-mode`, los cuatro exigiendo
  `SystemPermissions.LivestockAnimalsWrite`; y **endurecer el `POST /` existente**, que no
  exigía permiso.

**Verificación:** `dotnet test`, con los escenarios de código de estado de la tabla de
`spec.md` sec. 6 y los de 403 con un usuario sin el permiso. Después, el guion de `curl` de
[`test-e2e.md`](./test-e2e.md) V-1.

## PR3 — Pantalla admin-web

`feat(admin-web): add the animal-groups management screen`

- **Rama:** `feature/admin-web-animal-groups-screen`
- **Origen:** `develop`, con PR2 mergeado — el backend debe estar estable antes de cablear la
  UI.

**Por qué acá:** consume los endpoints de PR2. Puede desarrollarse contra un mock mientras PR2
está en revisión, pero **la decisión por defecto es secuencial**; si se paraleliza, PR3 se
mergea después de PR2 y con rebase.

**Archivos:**
- Modificar `clients/admin-web/src/app/services/api.service.ts` — DTOs y los ocho métodos.
- Modificar `app.routes.ts` — tres rutas bajo `permissionGuard('livestock.animals.write')`.
  Usar `loadComponent` sólo si las 13 rutas existentes ya son lazy; si son eager, seguir igual.
- Modificar `app.component.html` — entrada "Lotes y Grupos" en el sidebar, condicionada al
  permiso.
- Crear `components/animal-groups-list/`, `animal-group-create/` y `animal-group-detail/`,
  una carpeta por componente, imitando `catalogs/` (lista), `animal-detail/` (detalle) y
  `roles-management/` (edición inline).

**Verificación:** `ng test --watch=false` y `ng build` en 0, más los diez pasos del escenario
V-2 de [`test-e2e.md`](./test-e2e.md) con dos usuarios distintos.

## Cómo se prueba cada PR

| PR | Unitarias (xUnit) | Integración (xUnit + Testcontainers) | Vitest | Manual |
|---|---|---|---|---|
| PR1 | Dominio: `Activate`, `ChangeTrackingMode`, `Deactivate` idempotente | Los comandos nuevos contra Postgres real, más la agregación de la lista | — | — (sin verbos expuestos todavía) |
| PR2 | — | Códigos de estado de los cuatro verbos, más los 403 sin permiso | — | [`test-e2e.md`](./test-e2e.md) E2E-1, con `curl` |
| PR3 | — | — | Un spec por componente | E2E-2 y E2E-3, con dos usuarios |

**Postgres de las pruebas.** `Hato.TestSupport` resuelve con Testcontainers por defecto. Si
Docker no levanta contenedores en la máquina, se apunta a un servidor local con la variable
`HATO_TEST_POSTGRES`; `AGENTS.md` lo deja sentado. Sin una de las dos cosas, las pruebas de
integración de PR1 y PR2 no corren.

## Orden, dependencias y puntos de no retorno

```
PR #78 (ADR-0025, docs/)
  └─ PR1 (feature/livestock-animal-groups-domain)      ── bloqueante estricto
       └─ PR2 (feature/livestock-animal-groups-endpoints) ── bloqueante estricto
            └─ PR3 (feature/admin-web-animal-groups-screen)
```

Las tres dependencias son **bloqueantes estrictas**: cada PR no compila sin el anterior.

**Punto de no retorno: PR2**, por el endurecimiento del `POST /`. Los verbos nuevos se pueden
revertir sin consecuencias —nadie los usaba—, pero cerrar un endpoint que estaba abierto puede
romper un consumidor no inventariado, y revertirlo significa reabrir un hueco de autorización a
sabiendas. Se avanza con la prueba de 403 escrita, no se retrocede.

## Descripción del PR

**Título:** `feat: animal-groups admin screen with CRUD and mutable tracking mode`

**Cuerpo:**

- **Qué:** cierra el CRUD de lotes de punta a punta — dominio, endpoints y pantalla de
  administración con resumen y cambio de modo de seguimiento.
- **Por qué:** `docs/BACKLOG.md` sec. `[UI]` lo listaba como hueco; sin pantalla, corregir el
  `TrackingMode` de un lote mal creado exigía un `UPDATE` manual contra la base.
- **Decisiones:** las ocho de [`spec.md`](./spec.md) sec. 3. Las cinco primeras las fija
  ADR-0025; D6, D7 y D8 las cerró este plan.
- **Qué NO incluye:** migración de esquema, sincronización de los campos derivados, modal de
  confirmación reutilizable, icono dedicado, índice en `IsActive` y los catálogos puros. El
  detalle y el porqué, en `spec.md` sec. 4.
- **Riesgo declarado:** el endurecimiento del `POST /` es el único cambio que puede afectar a
  un consumidor existente. Cubierto por prueba de 403.
- **Cómo probarlo:** ejecutar [`test-e2e.md`](./test-e2e.md).
