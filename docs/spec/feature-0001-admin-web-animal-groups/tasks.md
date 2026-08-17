# tasks.md — Desglose ejecutable

> **Documento archivado.** El trabajo está terminado (PRs #80–#83, cerrados el 2026-08-08).
> Las casillas se marcaron el 2026-08-17, al convertir
> `docs/spec/PLAN-ADMIN-WEB-ANIMAL-GROUPS.md` a esta carpeta, **verificando cada una contra
> el código actual** — nunca contra lo que el plan original afirmara. Cada `[x]` cita archivo
> y línea. Lo que no se pudo verificar quedó `[ ]` con la explicación debajo: son cuatro
> casillas, y las cuatro son cobertura de pruebas que el plan prometió y que no está donde
> decía.
>
> Checklist de la serie `admin-web-animal-groups`. Agrupada **por PR**, que es la unidad de
> entrega de este trabajo — no por commit, porque fueron tres ramas y tres revisiones
> separadas.
>
> Convención: `[ ]` pendiente · `[x]` hecho · `[!]` bloqueada.

---

## PR1 — Dominio y aplicación

- [x] **T1.1** Agregar `Activate()` a `AnimalGroup`, idempotente.
      Evidencia: `src/Modules/Livestock/Hato.Modules.Livestock.Domain/AnimalGroup.cs:61`.
      Pruebas: `AnimalGroupTests.cs:158` (`Activate_OnInactiveGroup_SetsIsActiveTrue`) y `:170`
      (`Activate_OnActiveGroup_IsNoOp`).
- [x] **T1.2** Agregar `ChangeTrackingMode(newMode)`: no-op si el modo no cambia, lanza si el
      grupo está inactivo.
      Evidencia: `AnimalGroup.cs:74`. Pruebas: `AnimalGroupTests.cs:191`
      (`ChangeTrackingMode_SameMode_IsNoOp`), `:201` (`_OnInactive_Throws`), `:213`
      (`_OnActive_UpdatesMode`).
- [x] **T1.3** Verificar con prueba nueva que `Deactivate()` sigue siendo idempotente.
      Evidencia: `AnimalGroupTests.cs:181` (`Deactivate_Twice_IsIdempotent`); el método vive en
      `AnimalGroup.cs:52`.
- [x] **T1.4** Crear `UpdateAnimalGroupCommand` con su validador (nombre obligatorio ≤100,
      descripción ≤500).
      Evidencia: `…/Application/AnimalGroups/UpdateAnimalGroupCommand.cs`. Validador ejercido
      en `AnimalGroupApiTests.cs:121` (`UpdateAnimalGroupValidator_RejectsEmptyName`).
- [x] **T1.5** Crear `DeactivateAnimalGroupCommand` y `ActivateAnimalGroupCommand`.
      Evidencia: los dos archivos existen en `…/Application/AnimalGroups/`. Pruebas:
      `AnimalGroupApiTests.cs:131` y `:145`.
- [x] **T1.6** Crear `ChangeAnimalGroupTrackingModeCommand` con las guardas externas de
      membresías activas y de eventos.
      Evidencia: `ChangeAnimalGroupTrackingModeCommand.cs:51` (`GroupMemberships.AnyAsync` con
      `LeftAt == null`) y `:57` (`AnimalEvents.AnyAsync`). El comentario de `:63` deja
      explícito que la invariante de `IsActive` la tira el dominio como `DomainException` → 400,
      mientras que estas dos guardas responden 409.
- [x] **T1.7** Extraer `LiveHeadCountCalculator` y usarlo desde el summary **sin cambiar su
      comportamiento**.
      Evidencia: `LiveHeadCountCalculator.cs:12` — `Compute(activeMemberships, disposed)`,
      estático y puro, con `Math.Max(0, …)`.
- [x] **T1.8** Extender `AnimalGroupDto` con `LiveHeadCount` y `SpeciesName`.
      Evidencia: `GetAnimalGroupQueries.cs:15-24`; el comentario de `:11-14` atribuye la
      extensión al ADR-0025 y explica que existe para que el panel no haga fan-out.
      **Terminado:** `GetAnimalGroupsQuery_PopulatesLiveHeadCountAndSpeciesName`
      (`AnimalGroupApiTests.cs:236`) los verifica poblados.
- [x] **T1.9** Reescribir `GetAnimalGroupsHandler` con la agregación server-side y resolver
      `SpeciesName` con un `IN (...)` aparte (D6).
      Evidencia: `GetAnimalGroupQueries.cs`, handler de la lista; verificado por la prueba de
      T1.8.
- [ ] **T1.10** Prueba de integración que cuenta consultas con `DbCommandInterceptor`: 50
      grupos, ≤4 consultas.
      **No se hizo.** `grep -rn "DbCommandInterceptor" tests/ src/` no devuelve nada. Era la
      mitigación declarada del riesgo de N+1 de `spec.md` sec. 8, así que ese riesgo hoy no
      tiene red: si el batching se rompe, ninguna prueba lo detecta. Anotado como deuda en
      `docs/BACKLOG.md`.
- [ ] **T1.11** Pruebas unitarias de dominio `Update_WithNullDescription_AllowsNull` y
      `Update_WithEmptyName_Throws`.
      **No están.** `grep -rn "Update_With" tests/` no devuelve nada. La regla equivalente sí
      está cubierta, pero un nivel más arriba: el validador del comando
      (`AnimalGroupApiTests.cs:121`) y el endpoint (`:354`,
      `PutGroup_WithEmptyName_Returns400_ProblemDetails`). Queda `[ ]` porque la cobertura de
      dominio que el plan pedía no existe, no porque el comportamiento esté sin probar.
- [ ] **T1.12** Archivos de prueba unitaria nuevos en
      `tests/Hato.Modules.Livestock.UnitTests/Application/AnimalGroups/`.
      **No se hizo así.** Ese directorio no existe. Los comandos se prueban contra Postgres
      real en `AnimalGroupApiTests.cs:94-235` (doce escenarios que invocan los handlers
      directamente). Es una desviación de forma, no de cobertura: el plan pedía pruebas
      unitarias con dobles y se optó por integración con base real.
- [x] **T1.13** Ninguna migración EF Core.
      **Terminado:** no hay migración con `AnimalGroup` en el nombre posterior al 2026-08-08;
      `DeletedAt`/`UpdatedAt` los hereda `AuditableEntity`.

---

## PR2 — Endpoints REST y autorización

- [x] **T2.1** `PUT /{id}` con `livestock.animals.write`.
      Evidencia: `src/Hato.Api/Endpoints/AnimalGroupsEndpoints.cs:52`, permiso en `:56`.
      Pruebas: `AnimalGroupApiTests.cs:333` (204), `:354` (400), `:369` (404).
      Desviación menor: el endpoint recibe un `UpdateAnimalGroupRequest` y arma el comando
      adentro, en vez de aceptar el comando directo como esbozaba el plan.
- [x] **T2.2** `DELETE /{id}`, idempotente.
      Evidencia: `AnimalGroupsEndpoints.cs:60`, permiso en `:64`. Pruebas:
      `AnimalGroupApiTests.cs:382` (204), `:395` (404), `:403` (idempotente).
- [x] **T2.3** `POST /{id}/activate`, idempotente.
      Evidencia: `AnimalGroupsEndpoints.cs:66`, permiso en `:70`. Pruebas:
      `AnimalGroupApiTests.cs:415`, `:429` (404), `:437` (idempotente).
- [x] **T2.4** `PATCH /{id}/tracking-mode`.
      Evidencia: `AnimalGroupsEndpoints.cs:76`, permiso en `:80`. Pruebas:
      `AnimalGroupApiTests.cs:447` (204), `:463` (409 con evento), `:487` (400 con grupo
      inactivo).
- [ ] **T2.5** Prueba HTTP `PatchTrackingMode_OnGroupWithMembership_Returns409`.
      **No está a nivel HTTP.** El caso sí está cubierto un nivel más abajo, en el comando:
      `AnimalGroupApiTests.cs:201`
      (`ChangeAnimalGroupTrackingModeCommand_OnGroupWithActiveMember_ThrowsStateException`). El
      hermano con evento sí llega hasta HTTP (`:463`), así que la asimetría es un olvido, no
      una decisión.
- [x] **T2.6** Endurecer el `POST /` de creación, que no exigía permiso.
      Evidencia: `AnimalGroupsEndpoints.cs:46` con el permiso en `:50`. Prueba:
      `AnimalGroupAuthApiTests.cs:52`
      (`PostCreateGroup_WithoutLivestockAnimalsWritePermission_Returns403`).
      El endurecimiento alcanzó además a `POST /events` (`:16`, permiso en `:27`),
      `POST /members` (`:103`, permiso en `:107`) y `DELETE /members/{animalId}` (`:109`,
      permiso en `:113`).
- [x] **T2.7** Prueba de 403 para los verbos de escritura con un usuario sin el permiso.
      Evidencia: `AnimalGroupAuthApiTests.cs:26`
      (`WriteEndpoints_WithoutLivestockAnimalsWritePermission_Returns403`).
      Desviación: viven en un archivo propio, `AnimalGroupAuthApiTests.cs`, no dentro de
      `AnimalGroupApiTests.cs` como decía el plan. Es mejor así — el archivo de auth necesita
      su propio montaje de usuario sin permiso.
- [x] **T2.8** Verificar que las lecturas siguen abiertas a un autenticado sin el permiso de
      escritura.
      Evidencia: `AnimalGroupAuthApiTests.cs:66`
      (`GetEndpoints_StillOpen_ForAuthenticatedWithoutPermission`). No estaba en el plan; se
      agregó al implementar, y es la prueba que impide que el endurecimiento se pase de rosca.
- [x] **T2.9** Ninguna migración EF Core en este PR.

---

## PR3 — Pantalla admin-web

- [x] **T3.1** DTOs y los ocho métodos nuevos en `ApiService`.
      Evidencia: `clients/admin-web/src/app/services/api.service.ts:435` (`AnimalGroupDto`),
      `:447` (`AnimalGroupSummaryDto`), `:456` (`CreateAnimalGroupRequest`), `:463`
      (`UpdateAnimalGroupRequest`); métodos desde `:842`. Los comentarios de `:423` y `:838`
      apuntan al origen del contrato en PR1 y PR2.
- [x] **T3.2** Tres rutas bajo `permissionGuard('livestock.animals.write')`.
      Evidencia: `app.routes.ts:31` (`animal-groups`), `:36` (`/new`), `:41` (`/:id`).
      Desviación prevista por el plan: quedaron **eager** (`import` en `:14`), no con
      `loadComponent`, porque las rutas existentes del panel son eager. El plan lo autorizaba
      explícitamente.
- [x] **T3.3** Entrada "Lotes y Grupos" en el sidebar, condicionada al permiso.
      Evidencia: `app.component.html:99` (`routerLink="/animal-groups"`), con el icono
      placeholder `'tag'` en `:107`.
- [x] **T3.4** Componente de lista.
      Evidencia: `components/animal-groups-list/` con su
      `animal-groups-list.component.spec.ts`.
- [x] **T3.5** Componente de creación, con el radio de `TrackingMode` y su texto de ayuda.
      Evidencia: `components/animal-group-create/` con su `.spec.ts`. El texto se conserva
      literal en [`spec.md`](./spec.md) sec. 7.1.
- [x] **T3.6** Componente de detalle: encabezado, resumen, miembros, eventos, edición inline y
      flujo separado de cambio de modo.
      Evidencia: `components/animal-group-detail/` con su `.spec.ts`.
- [x] **T3.7** Usar `'tag'` como icono placeholder y anotar la deuda del icono dedicado.
      Evidencia: `app.component.html:107`; deuda en `docs/BACKLOG.md:351`.

---

## Cierre

- [x] **TC.1** Ejecutar [`test-e2e.md`](./test-e2e.md).
      Los escenarios se ejecutaron durante los PRs #80–#83. El guion se conserva como registro
      reproducible; no se volvió a correr al convertir el documento.
- [x] **TC.2** `dotnet test` en verde.
      Reverificado el 2026-08-17 durante la conversión: 0 fallos.
- [x] **TC.3** `npm test` en verde en `clients/admin-web`.
      Reverificado el 2026-08-17: 17 archivos, 102 pruebas, todas en verde.
- [x] **TC.4** Anotar en `docs/BACKLOG.md` las tres deudas que este trabajo declara.
      Evidencia: `docs/BACKLOG.md:339` (`ConfirmDialogComponent`), `:351` (icono dedicado),
      `:361` (índice en `AnimalGroup.IsActive`).
- [x] **TC.5** Cerrar el ítem del BACKLOG que este trabajo paga, dejando claro qué queda
      pendiente.
      Evidencia: `docs/BACKLOG.md:110-117` — marca `animalGroups` como satisfecho y mantiene el
      disparador original para los catálogos puros.
